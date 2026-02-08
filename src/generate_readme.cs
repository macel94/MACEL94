#!/usr/bin/env dotnet run
// Generate a GitHub profile README.md from cached LinkedIn data.
//
// Reads: src/linkedin_raw.json  (produced by fetch_linkedin.cs)
// Writes: README.md
//
// Usage:
//   dotnet run src/generate_readme.cs

#:property PublishAot=false

using System.Text;
using System.Text.Json;

// ── Main ─────────────────────────────────────────────────────────────

var csFilePath = GetScriptDirectory();
var rawPath = Path.Combine(csFilePath, "linkedin_raw.json");

if (!File.Exists(rawPath))
{
    Console.Error.WriteLine($"❌  {rawPath} not found. Run fetch_linkedin.cs first.");
    return 1;
}

var rawJson = File.ReadAllText(rawPath);
var allData = JsonSerializer.Deserialize<Dictionary<string, List<JsonElement>>>(rawJson)
              ?? new Dictionary<string, List<JsonElement>>();

if (allData.Count == 0)
{
    Console.Error.WriteLine("❌  linkedin_raw.json is empty.");
    return 1;
}

var readme = GenerateReadme(allData);
var readmePath = Path.Combine(csFilePath, "..", "README.md");
readmePath = Path.GetFullPath(readmePath);
File.WriteAllText(readmePath, readme);
Console.Error.WriteLine($"✅  README.md written to {readmePath}");

return 0;

// ── README Generation ────────────────────────────────────────────────

string GenerateReadme(Dictionary<string, List<JsonElement>> data)
{
    var sb = new StringBuilder();
    const string GH_USER = "macel94";

    // ── Profile data extraction ──────────────────────────────────────
    var profile = GetFirst(data, "PROFILE");
    var first = Safe(profile, "First Name");
    var last = Safe(profile, "Last Name");
    var headline = Safe(profile, "Headline");

    var summaryBlock = GetFirst(data, "PROFILE_SUMMARY");
    var summary = Safe(summaryBlock, "Summary");
    if (string.IsNullOrEmpty(summary))
        summary = Safe(summaryBlock, "summary");
    if (string.IsNullOrEmpty(summary))
        summary = Safe(profile, "Summary");

    var fullName = $"{first} {last}".Trim();
    if (string.IsNullOrEmpty(fullName)) fullName = "My Profile";

    var geo = Safe(profile, "Geo Location");

    // ── Hero Section ─────────────────────────────────────────────────
    sb.AppendLine($"# Hi, I'm {fullName} 👋");
    sb.AppendLine();

    // Tagline — punchy, scannable
    if (!string.IsNullOrEmpty(headline))
    {
        sb.AppendLine($"### {headline}");
        sb.AppendLine();
    }

    // Shields.io contact & social badges
    sb.AppendLine("<p>");
    sb.AppendLine($"  <a href=\"https://www.linkedin.com/in/fbelacca/\"><img src=\"https://img.shields.io/badge/LinkedIn-0A66C2?style=for-the-badge&logo=linkedin&logoColor=white\" alt=\"LinkedIn\"/></a>");
    sb.AppendLine($"  <a href=\"mailto:francesco.belacca@outlook.it\"><img src=\"https://img.shields.io/badge/Email-D14836?style=for-the-badge&logo=microsoft-outlook&logoColor=white\" alt=\"Email\"/></a>");
    sb.AppendLine($"  <a href=\"https://github.com/{GH_USER}\"><img src=\"https://img.shields.io/badge/GitHub-181717?style=for-the-badge&logo=github&logoColor=white\" alt=\"GitHub\"/></a>");
    if (!string.IsNullOrEmpty(geo))
        sb.AppendLine($"  <img src=\"https://img.shields.io/badge/📍_{geo.Replace(" ", "_").Replace(",", "%2C")}-grey?style=for-the-badge\" alt=\"Location\"/>");
    sb.AppendLine("</p>");
    sb.AppendLine();

    // ── About Me — formatted summary ─────────────────────────────────
    if (!string.IsNullOrEmpty(summary))
    {
        sb.AppendLine("## 🧑‍💻 About Me");
        sb.AppendLine();
        var formattedSummary = FormatSummary(summary);
        sb.AppendLine(formattedSummary);
        sb.AppendLine();
    }

    // ── GitHub Stats (generated as local SVGs by GitHub Actions) ────
    sb.AppendLine("## 📊 GitHub Stats");
    sb.AppendLine();
    sb.AppendLine("<p>");
    sb.AppendLine("  <img src=\"./artifacts/profile/stats.svg\" height=\"170\" alt=\"GitHub Stats\"/>");
    sb.AppendLine("  <img src=\"./artifacts/profile/top-langs.svg\" height=\"170\" alt=\"Top Languages\"/>");
    sb.AppendLine("</p>");
    sb.AppendLine();

    // ── Tech Stack ───────────────────────────────────────────────────
    sb.AppendLine("## 🛠 Tech Stack");
    sb.AppendLine();

    if (data.TryGetValue("SKILLS", out var skills) && skills.Count > 0)
    {
        var skillNames = skills
            .Select(s => Safe(s, "Name").Length > 0 ? Safe(s, "Name") : Safe(s, "Skill"))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        var (cloudOps, backend, devops, frontend, tools) = CategorizeSkills(skillNames);

        sb.AppendLine($"<img src=\"https://skillicons.dev/icons?i=azure,dotnet,cs,docker,kubernetes,powershell,bash,github,githubactions,git,js,vscode&perline=6\" alt=\"Tech Stack\"/>");
        sb.AppendLine();

        void WriteCategory(string label, List<string> items)
        {
            if (items.Count == 0) return;
            sb.AppendLine($"**{label}:** {string.Join(" · ", items)}");
            sb.AppendLine();
        }

        WriteCategory("Cloud & Infrastructure", cloudOps);
        WriteCategory("Backend & Languages", backend);
        WriteCategory("DevOps & CI/CD", devops);
        WriteCategory("Frontend", frontend);
        WriteCategory("Methods & Tools", tools);
    }

    // ── Microsoft Certifications (prominent) ─────────────────────────
    if (data.TryGetValue("CERTIFICATIONS", out var certs) && certs.Count > 0)
    {
        sb.AppendLine("## 📜 Certifications");
        sb.AppendLine();

        var microsoftCerts = certs
            .Where(c => Safe(c, "Authority").Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
                     && Safe(c, "Name").Contains("Certified", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => ParseDate(Safe(c, "Started On")))
            .ToList();

        var otherNotable = certs
            .Where(c => !Safe(c, "Authority").Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
                     || !Safe(c, "Name").Contains("Certified", StringComparison.OrdinalIgnoreCase))
            .Where(c =>
            {
                var name = Safe(c, "Name").ToLowerInvariant();
                var authority = Safe(c, "Authority").ToLowerInvariant();
                return !authority.Contains("linkedin") ||
                       name.Contains("career essentials", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(c => ParseDate(Safe(c, "Started On")))
            .ToList();

        if (microsoftCerts.Count > 0)
        {
            sb.AppendLine("<p>");
            foreach (var c in microsoftCerts)
            {
                var name = Safe(c, "Name");
                var url = Safe(c, "Url");
                var badgeLabel = name.Replace("Microsoft Certified: ", "");
                var badgeName = badgeLabel.Replace(" ", "_").Replace("-", "--");
                if (!string.IsNullOrEmpty(url))
                    sb.AppendLine($"  <a href=\"{url}\"><img src=\"https://img.shields.io/badge/{Uri.EscapeDataString(badgeName)}-0078D4?style=for-the-badge&logo=microsoft&logoColor=white\" alt=\"{badgeLabel}\"/></a>");
                else
                    sb.AppendLine($"  <img src=\"https://img.shields.io/badge/{Uri.EscapeDataString(badgeName)}-0078D4?style=for-the-badge&logo=microsoft&logoColor=white\" alt=\"{badgeLabel}\"/>");
            }
            sb.AppendLine("</p>");
            sb.AppendLine();
        }

        if (otherNotable.Count > 0)
        {
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Other certifications & courses</summary>");
            sb.AppendLine();
            foreach (var c in otherNotable)
            {
                var name = Safe(c, "Name");
                var authority = Safe(c, "Authority");
                var url = Safe(c, "Url");
                var dates = DateStr(c);

                var header = !string.IsNullOrEmpty(name) ? $"**{name}**" : "";
                if (!string.IsNullOrEmpty(authority)) header += $" - {authority}";
                if (!string.IsNullOrEmpty(dates)) header += $" ({dates})";
                if (!string.IsNullOrEmpty(url)) header = $"[{header}]({url})";
                sb.AppendLine($"- {header}");
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }
    }

    // ── Experience (top 3 only) ──────────────────────────────────────
    if (data.TryGetValue("POSITIONS", out var positions) && positions.Count > 0)
    {
        sb.AppendLine("## 💼 Experience");
        sb.AppendLine();

        var topPositions = positions.Take(3).ToList();

        foreach (var pos in topPositions)
        {
            var title = Safe(pos, "Title");
            var company = Safe(pos, "Company Name");
            var location = Safe(pos, "Location");
            var description = Safe(pos, "Description");
            var dates = DateStr(pos);

            var header = !string.IsNullOrEmpty(title) ? $"**{title}**" : "";
            if (!string.IsNullOrEmpty(company)) header += $" @ {company}";
            if (!string.IsNullOrEmpty(dates)) header += $" ({dates})";
            sb.AppendLine($"### {header}");
            if (!string.IsNullOrEmpty(location))
                sb.AppendLine($"📍 {location}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(description))
            {
                var bullets = FormatExperienceDescription(description);
                foreach (var bullet in bullets)
                    sb.AppendLine($"- {bullet}");
                sb.AppendLine();
            }
        }

        // Collapsed earlier roles
        if (positions.Count > 3)
        {
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Earlier roles</summary>");
            sb.AppendLine();
            foreach (var pos in positions.Skip(3))
            {
                var title = Safe(pos, "Title");
                var company = Safe(pos, "Company Name");
                var location = Safe(pos, "Location");
                var dates = DateStr(pos);
                var description = Safe(pos, "Description");

                var header = !string.IsNullOrEmpty(title) ? $"**{title}**" : "";
                if (!string.IsNullOrEmpty(company)) header += $" @ {company}";
                if (!string.IsNullOrEmpty(location)) header += $" · {location}";
                if (!string.IsNullOrEmpty(dates)) header += $" ({dates})";
                sb.AppendLine($"- {header}");
                if (!string.IsNullOrEmpty(description))
                {
                    var truncated = description.Length > 200 ? description[..200].TrimEnd() + "..." : description;
                    truncated = truncated.Replace("\n", " ").Replace("  ", " ");
                    sb.AppendLine($"  > {truncated}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        sb.AppendLine($"> 📄 [Full career history on LinkedIn](https://www.linkedin.com/in/fbelacca/)");
        sb.AppendLine();
    }

    // ── Featured Projects ────────────────────────────────────────────
    sb.AppendLine("## 🚀 Featured Projects");
    sb.AppendLine();
    sb.AppendLine($"- [**{GH_USER}**](https://github.com/{GH_USER}/{GH_USER}) — This profile auto-updates from LinkedIn using the [EU DMA Data Portability API](https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/), a C# script, and GitHub Actions.");
    sb.AppendLine();

    // LinkedIn projects section if available
    if (data.TryGetValue("PROJECTS", out var projects) && projects.Count > 0)
    {
        foreach (var proj in projects)
        {
            var pTitle = Safe(proj, "Title");
            if (string.IsNullOrEmpty(pTitle)) pTitle = Safe(proj, "Name");
            var pDesc = Safe(proj, "Description");
            var pUrl = Safe(proj, "Url");
            var pDates = DateStr(proj);

            var pHeader = !string.IsNullOrEmpty(pTitle) ? $"**{pTitle}**" : "";
            if (!string.IsNullOrEmpty(pDates)) pHeader += $" ({pDates})";
            if (!string.IsNullOrEmpty(pUrl)) pHeader = $"[{pHeader}]({pUrl})";
            sb.AppendLine($"- {pHeader}");
            if (!string.IsNullOrEmpty(pDesc)) sb.AppendLine($"  > {pDesc}");
        }
        sb.AppendLine();
    }

    // ── Education ─────────────────────────────────────────────────────
    if (data.TryGetValue("EDUCATION", out var education) && education.Count > 0)
    {
        sb.AppendLine("## 🎓 Education");
        sb.AppendLine();
        foreach (var edu in education)
        {
            var school = Safe(edu, "School Name");
            var degree = Safe(edu, "Degree Name");
            var field = Safe(edu, "Fields of Study");
            if (string.IsNullOrEmpty(field)) field = Safe(edu, "Field Of Study");
            var notes = Safe(edu, "Notes");
            var activities = Safe(edu, "Activities");
            var dates = DateStr(edu);

            var header = !string.IsNullOrEmpty(school) ? $"**{school}**" : "**School**";
            var degreeParts = new[] { degree, field }.Where(x => !string.IsNullOrEmpty(x));
            if (degreeParts.Any()) header += $" - {string.Join(", ", degreeParts)}";
            if (!string.IsNullOrEmpty(dates)) header += $" ({dates})";
            sb.AppendLine($"- {header}");
            if (!string.IsNullOrEmpty(notes)) sb.AppendLine($"  > {notes}");
            if (!string.IsNullOrEmpty(activities)) sb.AppendLine($"  > Activities: {activities}");
        }
        sb.AppendLine();
    }

    // ── Languages ─────────────────────────────────────────────────────
    if (data.TryGetValue("LANGUAGES", out var languages) && languages.Count > 0)
    {
        sb.AppendLine("## 🌐 Languages");
        sb.AppendLine();
        foreach (var lang in languages)
        {
            var name = Safe(lang, "Name");
            var proficiency = Safe(lang, "Proficiency");
            var normalizedProficiency = NormalizeProficiency(proficiency);
            var entry = !string.IsNullOrEmpty(name) ? $"**{NormalizeLanguageName(name)}**" : "";
            if (!string.IsNullOrEmpty(normalizedProficiency)) entry += $" — {normalizedProficiency}";
            sb.AppendLine($"- {entry}");
        }
        sb.AppendLine();
    }

    // ── Volunteering ──────────────────────────────────────────────────
    if (data.TryGetValue("VOLUNTEERING_EXPERIENCES", out var volunteering) && volunteering.Count > 0)
    {
        sb.AppendLine("## 🤝 Volunteering");
        sb.AppendLine();
        foreach (var vol in volunteering)
        {
            var role = Safe(vol, "Role");
            var orgName = Safe(vol, "Organization");
            if (string.IsNullOrEmpty(orgName)) orgName = Safe(vol, "Company");
            var cause = Safe(vol, "Cause");
            var description = Safe(vol, "Description");
            var dates = DateStr(vol);

            var header = !string.IsNullOrEmpty(role) ? $"**{role}**" : "";
            if (!string.IsNullOrEmpty(orgName)) header += $" @ {orgName}";
            if (!string.IsNullOrEmpty(cause)) header += $" · {cause}";
            if (!string.IsNullOrEmpty(dates)) header += $" ({dates})";
            sb.AppendLine($"- {header}");
            if (!string.IsNullOrEmpty(description)) sb.AppendLine($"  > {description}");
        }
        sb.AppendLine();
    }

    // ── Recommendations ───────────────────────────────────────────────
    if (data.TryGetValue("RECOMMENDATIONS", out var recommendations) && recommendations.Count > 0)
    {
        var received = recommendations
            .Where(r => Safe(r, "Type").Equals("received", StringComparison.OrdinalIgnoreCase) ||
                        Safe(r, "Direction").Equals("received", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();

        if (received.Count > 0)
        {
            sb.AppendLine("## 💬 What People Say");
            sb.AppendLine();
            foreach (var rec in received)
            {
                var recommender = Safe(rec, "Recommender");
                if (string.IsNullOrEmpty(recommender))
                    recommender = $"{Safe(rec, "First Name")} {Safe(rec, "Last Name")}".Trim();
                var text = Safe(rec, "Recommendation");
                if (string.IsNullOrEmpty(text)) text = Safe(rec, "Text");

                if (!string.IsNullOrEmpty(text))
                {
                    var truncated = text.Length > 250 ? text[..250].TrimEnd() + "..." : text;
                    sb.AppendLine($"> *\"{truncated}\"*");
                    if (!string.IsNullOrEmpty(recommender))
                        sb.AppendLine($"> — **{recommender}**");
                    sb.AppendLine();
                }
            }
        }
    }

    // ── Footer ────────────────────────────────────────────────────────
    var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
    sb.AppendLine("---");
    sb.AppendLine();
    sb.AppendLine("### 📥 Download CV");
    sb.AppendLine();
    sb.AppendLine($"- [PDF Version](./Francesco_Belacca_CV.pdf)");
    sb.AppendLine($"- [Europass XML](./europass_cv.xml)");
    sb.AppendLine();
    sb.AppendLine(
        $"<sub>🔄 Auto-generated from LinkedIn via " +
        $"[DMA Data Portability API](https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/) " +
        $"· Last updated: {now}</sub>");
    sb.AppendLine();

    return sb.ToString();
}

// ── Formatting Helpers ───────────────────────────────────────────────

string FormatSummary(string summary)
{
    var sb = new StringBuilder();
    var text = summary.Trim();

    var knownHeaders = new[] {
        "Key Competencies:", "My Mission:", "Focus areas",
        "Core Competencies:", "What I do:", "Areas of expertise:"
    };

    var paragraphs = text.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrEmpty(p))
        .ToList();

    if (paragraphs.Count <= 1)
    {
        paragraphs = [text];
    }

    var competenciesStarted = false;

    foreach (var para in paragraphs)
    {
        var trimmed = para.Trim();
        if (string.IsNullOrEmpty(trimmed)) continue;

        if (trimmed.StartsWith("Key Competencies:") || trimmed.StartsWith("Core Competencies:"))
        {
            competenciesStarted = true;
            sb.AppendLine();
            sb.AppendLine("**Key Competencies:**");
            sb.AppendLine();
            var rest = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
            if (!string.IsNullOrEmpty(rest))
            {
                WriteCompetencyBullets(sb, rest);
            }
            continue;
        }

        if (trimmed.StartsWith("My Mission:"))
        {
            competenciesStarted = false;
            sb.AppendLine();
            sb.AppendLine($"**🎯 {trimmed}**");
            sb.AppendLine();
            continue;
        }

        if (trimmed.StartsWith("Based in"))
        {
            sb.AppendLine();
            sb.AppendLine($"*{trimmed}*");
            continue;
        }

        if (competenciesStarted)
        {
            WriteCompetencyBullets(sb, trimmed);
            continue;
        }

        sb.AppendLine(trimmed);
        sb.AppendLine();
    }

    return sb.ToString().TrimEnd();
}

void WriteCompetencyBullets(StringBuilder sb, string text)
{
    var competencyPattern = new[] {
        "Governance at Scale:", "Infrastructure as Code:", "Backend Engineering:",
        "Security Automation:", "Cloud Solutions Architecting:", "Power Platform Administration:",
        "Cloud Architecture:", "DevOps:", "Security:", "Automation:"
    };

    var current = text;
    var foundAny = false;

    foreach (var pattern in competencyPattern)
    {
        var idx = current.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            foundAny = true;
        }
    }

    if (foundAny)
    {
        var remaining = current;
        var bullets = new List<(string label, string desc)>();

        while (remaining.Length > 0)
        {
            var bestIdx = int.MaxValue;
            var bestPattern = "";

            foreach (var pattern in competencyPattern)
            {
                var idx = remaining.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && idx < bestIdx)
                {
                    bestIdx = idx;
                    bestPattern = pattern;
                }
            }

            if (bestIdx == int.MaxValue || string.IsNullOrEmpty(bestPattern))
            {
                if (remaining.Trim().Length > 0 && bullets.Count > 0)
                {
                    var last = bullets[^1];
                    bullets[^1] = (last.label, (last.desc + " " + remaining.Trim()).Trim());
                }
                break;
            }

            if (bestIdx > 0 && bullets.Count > 0)
            {
                var last = bullets[^1];
                bullets[^1] = (last.label, (last.desc + " " + remaining[..bestIdx].Trim()).Trim());
            }

            remaining = remaining[(bestIdx + bestPattern.Length)..];

            var nextIdx = int.MaxValue;
            foreach (var pattern in competencyPattern)
            {
                var idx = remaining.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && idx < nextIdx) nextIdx = idx;
            }

            var desc = nextIdx < int.MaxValue ? remaining[..nextIdx].Trim() : remaining.Trim();
            bullets.Add((bestPattern.TrimEnd(':'), desc));
            remaining = nextIdx < int.MaxValue ? remaining[nextIdx..] : "";
        }

        foreach (var (label, desc) in bullets)
        {
            sb.AppendLine($"- **{label}:** {desc}");
        }
    }
    else
    {
        sb.AppendLine(text);
    }
}

List<string> FormatExperienceDescription(string description)
{
    var bullets = new List<string>();
    var text = description.Trim();

    var parts = text.Split(new[] { "  ", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrEmpty(p) && p.Length > 5)
        .ToList();

    if (parts.Count <= 1)
    {
        var sentences = text.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().TrimEnd('.'))
            .Where(s => !string.IsNullOrEmpty(s) && s.Length > 10)
            .ToList();

        if (sentences.Count > 1)
        {
            foreach (var sentence in sentences.Take(6))
                bullets.Add(sentence);
        }
        else
        {
            bullets.Add(text.Length > 300 ? text[..300].TrimEnd() + "..." : text);
        }
    }
    else
    {
        foreach (var part in parts)
        {
            if (bullets.Count >= 6) break;

            var cleaned = part.TrimStart('-', '•', '*', ' ');
            if (cleaned.StartsWith("Focus areas", StringComparison.OrdinalIgnoreCase))
            {
                bullets.Add($"**{cleaned}**");
            }
            else if (cleaned.Length > 200)
            {
                var subSentences = cleaned.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().TrimEnd('.'))
                    .Where(s => !string.IsNullOrEmpty(s) && s.Length > 10)
                    .ToList();
                foreach (var sub in subSentences)
                {
                    if (bullets.Count >= 6) break;
                    bullets.Add(sub);
                }
            }
            else
            {
                bullets.Add(cleaned);
            }
        }
    }

    return bullets;
}

(List<string> cloudOps, List<string> backend, List<string> devops, List<string> frontend, List<string> tools)
    CategorizeSkills(List<string> allSkills)
{
    var cloudKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "Azure", "Windows Azure", "Cloud Infrastructure", "Cloud Security",
        "Cost Optimization", "Microsoft Entra ID", "Microsoft Dynamics 365",
        "Microsoft Power Platform", "Microsoft Fabric", "Business Central",
        "Software Infrastructure", "Containerization", "Distributed Systems",
        "Kubernetes"
    };
    var backendKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "C#", ".NET", ".NET Framework", "SQL", "T-SQL", "LINQ",
        "Microservices", "Server Microsoft SQL", "OOP", "JavaScript",
        "Database", "ETL"
    };
    var devopsKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "DevOps", "AZURE DEVOPS", "Git", "Version Control", "Automation",
        "Continuous improvement", "Github Enterprise", "Powershell Core",
        "Windows PowerShell", "Bash", "DSC", "Troubleshooting"
    };
    var frontendKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "HTML", "Kibana"
    };
    var toolKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "Visual Studio", "JIRA", "wsl", "Microsoft Office",
        "Agile Methodologies", "Agile Project Management", "Stakeholder Management",
        "Project management", "Project Leadership", "Communication",
        "Public speaking", "Attention to Detail", "Analytical Skills",
        "Generative AI", "Artificial Intelligence (AI)", "Artificial Intelligence for Business",
        "Chatbots", "Microsoft Search", "Search Engine Technology"
    };

    var excludeSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "Lingua inglese", "Pianificazione delle capacità", "Lavoro di squadra",
        "Analisi dei dati", "Sviluppo di prodotto", "Infrastrutture",
        "Applicazioni Web", "Sviluppo di software", "Integrazione continua",
        "Social media", "Time management", "Customer Requirements",
        "Computer Ethics", "Identity management", "Security Administration"
    };

    var cloud = new List<string>();
    var backend = new List<string>();
    var devops = new List<string>();
    var frontend = new List<string>();
    var tools = new List<string>();

    foreach (var skill in allSkills)
    {
        if (excludeSkills.Contains(skill)) continue;

        if (cloudKeywords.Contains(skill)) cloud.Add(skill);
        else if (backendKeywords.Contains(skill)) backend.Add(skill);
        else if (devopsKeywords.Contains(skill)) devops.Add(skill);
        else if (frontendKeywords.Contains(skill)) frontend.Add(skill);
        else if (toolKeywords.Contains(skill)) tools.Add(skill);
    }

    return (cloud, backend, devops, frontend, tools);
}

string NormalizeLanguageName(string name)
{
    return name switch
    {
        "Inglese" => "English",
        "Italiano" => "Italian",
        "Francese" => "French",
        "Spagnolo" => "Spanish",
        "Tedesco" => "German",
        _ => name
    };
}

string NormalizeProficiency(string proficiency)
{
    if (string.IsNullOrEmpty(proficiency)) return "";
    return proficiency;
}

// ── Helpers ──────────────────────────────────────────────────────────

string Safe(JsonElement element, string key)
{
    if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
        return "";
    if (element.TryGetProperty(key, out var val))
    {
        return val.ValueKind switch
        {
            JsonValueKind.String => val.GetString()?.Trim() ?? "",
            JsonValueKind.Number => val.ToString(),
            _ => val.ToString()
        };
    }
    return "";
}

JsonElement GetFirst(Dictionary<string, List<JsonElement>> data, string domain)
{
    if (data.TryGetValue(domain, out var list) && list.Count > 0)
        return list[0];
    return default;
}

string DateStr(JsonElement el)
{
    var started = Safe(el, "Started On");
    if (string.IsNullOrEmpty(started)) started = Safe(el, "Start Date");
    var finished = Safe(el, "Finished On");
    if (string.IsNullOrEmpty(finished)) finished = Safe(el, "End Date");

    if (!string.IsNullOrEmpty(started) && !string.IsNullOrEmpty(finished))
        return $"{started} - {finished}";
    if (!string.IsNullOrEmpty(started))
        return $"{started} - Present";
    return "";
}

DateTime ParseDate(string dateStr)
{
    if (DateTime.TryParseExact(dateStr, "MMM yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
        return dt;
    return DateTime.MinValue;
}

string GetScriptDirectory()
{
    var candidates = new[]
    {
        Path.Combine(Environment.CurrentDirectory, "src"),
        Environment.CurrentDirectory
    };

    foreach (var dir in candidates)
    {
        if (File.Exists(Path.Combine(dir, "generate_readme.cs")))
            return Path.GetFullPath(dir);
    }

    return Path.GetFullPath("src");
}
