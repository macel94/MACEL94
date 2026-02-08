#!/usr/bin/env dotnet run
// Fetch LinkedIn profile data via the DMA Member Data Portability API
// and generate a GitHub profile README.md.
//
// Requires:
//   - .NET 10+ (file-based app, no .csproj needed)
//   - An access token generated with scope r_dma_portability_self_serve
//     via the LinkedIn Developer Portal OAuth Token Generator Tool.
//   - The token must be from an EU/EEA/Switzerland-based LinkedIn member.
//
// Usage:
//   export LINKEDIN_ACCESS_TOKEN="your_token"
//   dotnet run scripts/fetch_linkedin.cs
//
// API Reference:
//   https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/

#:property PublishAot=false

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// ── Configuration ────────────────────────────────────────────────────

const string BaseUrl = "https://api.linkedin.com/rest/memberSnapshotData";
const string ApiVersion = "202312";

string[] relevantDomains =
[
    "PROFILE",
    "POSITIONS",
    "EDUCATION",
    "SKILLS",
    "CERTIFICATIONS",
    "COURSES",
    "LANGUAGES",
    "HONORS",
    "PUBLICATIONS",
    "PATENTS",
    "PROJECTS",
    "ORGANIZATIONS",
    "VOLUNTEERING_EXPERIENCES",
    "RECOMMENDATIONS",
    "ENDORSEMENTS",
    "CAUSES_YOU_CARE_ABOUT",
    "PROFILE_SUMMARY",
];

// ── Main ─────────────────────────────────────────────────────────────

var token = Environment.GetEnvironmentVariable("LINKEDIN_ACCESS_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("❌  Set LINKEDIN_ACCESS_TOKEN environment variable.");
    return 1;
}

Console.Error.WriteLine("🔄  Fetching LinkedIn data via DMA Portability API...");

using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
http.DefaultRequestHeaders.Add("Linkedin-Version", ApiVersion);
http.Timeout = TimeSpan.FromSeconds(30);

var allData = new Dictionary<string, List<JsonElement>>();

foreach (var domain in relevantDomains)
{
    Console.Error.Write($"  Fetching {domain}...");
    var records = await FetchDomain(http, domain);
    if (records.Count > 0)
    {
        allData[domain] = records;
        Console.Error.WriteLine($" ✓ {records.Count} record(s)");
    }
    else
    {
        Console.Error.WriteLine(" – no data");
    }
}

if (allData.Count == 0)
{
    // Fallback: try loading cached raw JSON if available
    var cachedPath = Path.Combine(GetScriptDirectory(), "linkedin_raw.json");
    if (File.Exists(cachedPath))
    {
        Console.Error.WriteLine("⚠️  No live data. Falling back to cached linkedin_raw.json...");
        var cachedJson = File.ReadAllText(cachedPath);
        allData = JsonSerializer.Deserialize<Dictionary<string, List<JsonElement>>>(cachedJson)
                  ?? new Dictionary<string, List<JsonElement>>();
    }

    if (allData.Count == 0)
    {
        Console.Error.WriteLine("❌  No data returned. Check your token and that snapshot processing is complete.");
        return 1;
    }
}

// Save raw JSON for debugging
var scriptDir = Path.GetDirectoryName(Path.GetFullPath(args.Length > 0 ? args[0] : ".")) ?? ".";
// Determine script location: use the directory where this .cs file lives
var csFilePath = GetScriptDirectory();
var rawPath = Path.Combine(csFilePath, "linkedin_raw.json");
var rawJson = JsonSerializer.Serialize(allData, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(rawPath, rawJson);
Console.Error.WriteLine($"💾  Raw data saved to {rawPath}");

// Generate README
var readme = GenerateReadme(allData);
var readmePath = Path.Combine(csFilePath, "..", "README.md");
readmePath = Path.GetFullPath(readmePath);
File.WriteAllText(readmePath, readme);
Console.Error.WriteLine($"✅  README.md written to {readmePath}");

// Generate Europass CV XML
var europassXml = GenerateEuropassXml(allData);
var europassPath = Path.Combine(csFilePath, "..", "europass_cv.xml");
europassPath = Path.GetFullPath(europassPath);
File.WriteAllText(europassPath, europassXml);
Console.Error.WriteLine($"✅  Europass CV XML written to {europassPath}");

return 0;

// ── API Fetching ─────────────────────────────────────────────────────

async Task<List<JsonElement>> FetchDomain(HttpClient client, string domain)
{
    var allRecords = new List<JsonElement>();
    var start = 0;

    while (true)
    {
        var url = $"{BaseUrl}?q=criteria&domain={domain}&start={start}&count=10";
        HttpResponseMessage resp;
        try
        {
            resp = await client.GetAsync(url);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n  ⚠  {domain}: {ex.Message}");
            break;
        }

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            break;

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"\n  ⚠  {domain}: HTTP {(int)resp.StatusCode} – {body[..Math.Min(body.Length, 200)]}");
            break;
        }

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("elements", out var elements) || elements.GetArrayLength() == 0)
            break;

        foreach (var element in elements.EnumerateArray())
        {
            if (element.TryGetProperty("snapshotData", out var snapshotData))
            {
                foreach (var record in snapshotData.EnumerateArray())
                {
                    allRecords.Add(record.Clone());
                }
            }
        }

        // Check for next page
        var hasNext = false;
        if (root.TryGetProperty("paging", out var paging) &&
            paging.TryGetProperty("links", out var links))
        {
            foreach (var link in links.EnumerateArray())
            {
                if (link.TryGetProperty("rel", out var rel) && rel.GetString() == "next")
                {
                    hasNext = true;
                    break;
                }
            }
        }

        if (!hasNext) break;
        start++;
    }

    return allRecords;
}

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
        // Split continuous text into readable chunks by sentence or by section keywords
        var formattedSummary = FormatSummary(summary);
        sb.AppendLine(formattedSummary);
        sb.AppendLine();
    }

    // ── GitHub Stats (generated as local SVGs by GitHub Actions) ────
    sb.AppendLine("## 📊 GitHub Stats");
    sb.AppendLine();
    sb.AppendLine("<p>");
    sb.AppendLine("  <img src=\"./profile/stats.svg\" height=\"170\" alt=\"GitHub Stats\"/>");
    sb.AppendLine("  <img src=\"./profile/top-langs.svg\" height=\"170\" alt=\"Top Languages\"/>");
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

        // Map LinkedIn skills to skillicons.dev icon IDs and group by category
        var (cloudOps, backend, devops, frontend, tools) = CategorizeSkills(skillNames);

        // Visual skill icons row via skillicons.dev
        sb.AppendLine($"<img src=\"https://skillicons.dev/icons?i=azure,dotnet,cs,docker,kubernetes,powershell,bash,github,githubactions,git,js,vscode,visualstudio&perline=8\" alt=\"Tech Stack\"/>");
        sb.AppendLine();

        // Grouped badges for detail
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

        // Only prominent certs with Shields.io badges
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
                // Keep Udemy courses, Kubernetes, Speexx, MENSA — skip LinkedIn Learning micro-courses
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
                if (!string.IsNullOrEmpty(authority)) header += $" – {authority}";
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
                // Convert raw text to bullet points
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
                    // Truncated summary for older roles
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
            if (degreeParts.Any()) header += $" – {string.Join(", ", degreeParts)}";
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
            // Normalize proficiency to English
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
    // Split the raw LinkedIn summary into structured, readable paragraphs
    // Look for known section headers within the text
    var sb = new StringBuilder();
    var text = summary.Trim();

    // Try to split on common LinkedIn summary patterns
    // "Key Competencies:" or similar headers embedded in text
    var knownHeaders = new[] {
        "Key Competencies:", "My Mission:", "Focus areas",
        "Core Competencies:", "What I do:", "Areas of expertise:"
    };

    // First, try splitting by double-space which LinkedIn often uses as paragraph separator
    var paragraphs = text.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrEmpty(p))
        .ToList();

    if (paragraphs.Count <= 1)
    {
        // Fallback: split on period + space for sentences
        paragraphs = [text];
    }

    var competenciesStarted = false;

    foreach (var para in paragraphs)
    {
        var trimmed = para.Trim();
        if (string.IsNullOrEmpty(trimmed)) continue;

        // Detect embedded headers
        if (trimmed.StartsWith("Key Competencies:") || trimmed.StartsWith("Core Competencies:"))
        {
            competenciesStarted = true;
            sb.AppendLine();
            sb.AppendLine("**Key Competencies:**");
            sb.AppendLine();
            var rest = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
            if (!string.IsNullOrEmpty(rest))
            {
                // Split competencies on double-space or sentences that start with a keyword
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
    // Split on known competency labels followed by colons
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
        // Split on these patterns
        var remaining = current;
        var bullets = new List<(string label, string desc)>();

        while (remaining.Length > 0)
        {
            // Find the next competency header
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
                // No more headers found — remaining is trailing text
                if (remaining.Trim().Length > 0 && bullets.Count > 0)
                {
                    var last = bullets[^1];
                    bullets[^1] = (last.label, (last.desc + " " + remaining.Trim()).Trim());
                }
                break;
            }

            // Text before this header belongs to previous bullet or is skipped
            if (bestIdx > 0 && bullets.Count > 0)
            {
                var last = bullets[^1];
                bullets[^1] = (last.label, (last.desc + " " + remaining[..bestIdx].Trim()).Trim());
            }

            remaining = remaining[(bestIdx + bestPattern.Length)..];

            // Find where the description for this competency ends (next competency or end)
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
        // No known headers — just output the text
        sb.AppendLine(text);
    }
}

List<string> FormatExperienceDescription(string description)
{
    // Convert a raw LinkedIn description into clean bullet points
    var bullets = new List<string>();
    var text = description.Trim();

    // Split on double-space (LinkedIn's internal separator) or newlines
    var parts = text.Split(new[] { "  ", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrEmpty(p) && p.Length > 5)
        .ToList();

    if (parts.Count <= 1)
    {
        // Single block — try splitting on sentence boundaries
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

            // Clean up: remove "Focus areas" prefix-style lines and make them a sub-header if needed
            var cleaned = part.TrimStart('-', '•', '*', ' ');
            if (cleaned.StartsWith("Focus areas", StringComparison.OrdinalIgnoreCase))
            {
                bullets.Add($"**{cleaned}**");
            }
            else if (cleaned.Length > 200)
            {
                // Long block — sub-split on sentence boundaries
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
    // Curated mapping from LinkedIn skill names to categories
    // Only keep English-language, high-signal skills
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

    // Skills to exclude (Italian duplicates, too generic, noise)
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
        // Skip unlisted skills to keep the list curated
    }

    return (cloud, backend, devops, frontend, tools);
}

string NormalizeLanguageName(string name)
{
    // Convert Italian language names to English
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
    // Already English proficiency levels — pass through
    return proficiency;
}

// ── Europass CV XML Generation ───────────────────────────────────────
// Generates an XML file conforming to Europass CV XML Schema v3.4.0
// Reference: https://europass.europa.eu/system/files/2020-07/europass-xml-schema-doc-v3.4.0_0.pdf

string GenerateEuropassXml(Dictionary<string, List<JsonElement>> data)
{
    var profile = GetFirst(data, "PROFILE");
    var first = Safe(profile, "First Name");
    var last = Safe(profile, "Last Name");
    var headline = Safe(profile, "Headline");
    var geo = Safe(profile, "Geo Location");

    var summaryBlock = GetFirst(data, "PROFILE_SUMMARY");
    var summary = Safe(summaryBlock, "Summary");
    if (string.IsNullOrEmpty(summary))
        summary = Safe(summaryBlock, "summary");
    if (string.IsNullOrEmpty(summary))
        summary = Safe(profile, "Summary");

    // Parse location into city/country
    var locationParts = geo.Split(',').Select(p => p.Trim()).ToArray();
    var city = locationParts.Length > 0 ? locationParts[0] : "";
    var country = locationParts.Length > 1 ? locationParts[^1] : "";
    var countryCode = country switch
    {
        "Italy" => "IT",
        "Germany" => "DE",
        "France" => "FR",
        "Spain" => "ES",
        "United Kingdom" => "GB",
        "United States" => "US",
        _ => country.Length == 2 ? country : ""
    };

    var now = DateTime.UtcNow;
    var sb = new StringBuilder();

    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<SkillsPassport xmlns=\"http://europass.cedefop.europa.eu/Europass\"");
    sb.AppendLine("  xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
    sb.AppendLine("  xsi:schemaLocation=\"http://europass.cedefop.europa.eu/Europass\"");
    sb.AppendLine($"  locale=\"en\">");
    sb.AppendLine();

    // DocumentInfo
    sb.AppendLine("  <DocumentInfo>");
    sb.AppendLine("    <DocumentType>ECV</DocumentType>");
    sb.AppendLine($"    <CreationDate>{now:yyyy-MM-dd}T{now:HH:mm:ss.fff}Z</CreationDate>");
    sb.AppendLine($"    <LastUpdateDate>{now:yyyy-MM-dd}T{now:HH:mm:ss.fff}Z</LastUpdateDate>");
    sb.AppendLine("    <XSDVersion>V3.4</XSDVersion>");
    sb.AppendLine("    <Generator>LinkedIn DMA Portability API + C# Script</Generator>");
    sb.AppendLine("    <Comment>Auto-generated from LinkedIn profile data</Comment>");
    sb.AppendLine("  </DocumentInfo>");
    sb.AppendLine();

    sb.AppendLine("  <LearnerInfo>");

    // Identification
    sb.AppendLine("    <Identification>");
    sb.AppendLine("      <PersonName>");
    sb.AppendLine($"        <FirstName>{EscapeXml(first)}</FirstName>");
    sb.AppendLine($"        <Surname>{EscapeXml(last)}</Surname>");
    sb.AppendLine("      </PersonName>");
    sb.AppendLine("      <ContactInfo>");
    sb.AppendLine("        <Address>");
    sb.AppendLine("          <Contact>");
    if (!string.IsNullOrEmpty(city))
        sb.AppendLine($"            <Municipality>{EscapeXml(city)}</Municipality>");
    if (!string.IsNullOrEmpty(countryCode))
        sb.AppendLine($"            <Country><Code>{countryCode}</Code><Label>{EscapeXml(country)}</Label></Country>");
    sb.AppendLine("          </Contact>");
    sb.AppendLine("        </Address>");
    sb.AppendLine("        <Email>");
    sb.AppendLine("          <Contact>francesco.belacca@outlook.it</Contact>");
    sb.AppendLine("        </Email>");
    sb.AppendLine("        <Website>");
    sb.AppendLine("          <Contact>https://github.com/macel94</Contact>");
    sb.AppendLine("        </Website>");
    sb.AppendLine("        <Website>");
    sb.AppendLine("          <Contact>https://www.linkedin.com/in/fbelacca/</Contact>");
    sb.AppendLine("        </Website>");
    sb.AppendLine("      </ContactInfo>");
    sb.AppendLine("    </Identification>");
    sb.AppendLine();

    // Headline
    if (!string.IsNullOrEmpty(headline))
    {
        sb.AppendLine("    <Headline>");
        sb.AppendLine("      <Type><Code>position</Code><Label>Desired employment / Occupational field</Label></Type>");
        sb.AppendLine($"      <Description><Label>{EscapeXml(headline)}</Label></Description>");
        sb.AppendLine("    </Headline>");
        sb.AppendLine();
    }

    // Work Experience
    if (data.TryGetValue("POSITIONS", out var positions) && positions.Count > 0)
    {
        sb.AppendLine("    <WorkExperienceList>");
        foreach (var pos in positions)
        {
            var title = Safe(pos, "Title");
            var company = Safe(pos, "Company Name");
            var location = Safe(pos, "Location");
            var description = Safe(pos, "Description");

            sb.AppendLine("      <WorkExperience>");

            // Period
            var started = Safe(pos, "Started On");
            var finished = Safe(pos, "Finished On");
            if (!string.IsNullOrEmpty(started))
            {
                sb.AppendLine("        <Period>");
                sb.AppendLine($"          <From>{FormatEuropassDate(started)}</From>");
                if (!string.IsNullOrEmpty(finished))
                    sb.AppendLine($"          <To>{FormatEuropassDate(finished)}</To>");
                else
                    sb.AppendLine("          <Current>true</Current>");
                sb.AppendLine("        </Period>");
            }

            sb.AppendLine("        <Position>");
            sb.AppendLine($"          <Label>{EscapeXml(title)}</Label>");
            sb.AppendLine("        </Position>");

            if (!string.IsNullOrEmpty(description))
            {
                var cleanDesc = description.Replace("  ", " ").Replace("\n", " ").Trim();
                sb.AppendLine($"        <Activities>{EscapeXml(cleanDesc)}</Activities>");
            }

            sb.AppendLine("        <Employer>");
            sb.AppendLine($"          <Name>{EscapeXml(company)}</Name>");
            if (!string.IsNullOrEmpty(location))
            {
                sb.AppendLine("          <ContactInfo>");
                sb.AppendLine("            <Address>");
                sb.AppendLine("              <Contact>");
                sb.AppendLine($"                <Municipality>{EscapeXml(location)}</Municipality>");
                sb.AppendLine("              </Contact>");
                sb.AppendLine("            </Address>");
                sb.AppendLine("          </ContactInfo>");
            }
            sb.AppendLine("        </Employer>");

            sb.AppendLine("      </WorkExperience>");
        }
        sb.AppendLine("    </WorkExperienceList>");
        sb.AppendLine();
    }

    // Education
    if (data.TryGetValue("EDUCATION", out var education) && education.Count > 0)
    {
        sb.AppendLine("    <EducationList>");
        foreach (var edu in education)
        {
            var school = Safe(edu, "School Name");
            var degree = Safe(edu, "Degree Name");
            var field = Safe(edu, "Fields of Study");
            if (string.IsNullOrEmpty(field)) field = Safe(edu, "Field Of Study");
            var notes = Safe(edu, "Notes");

            sb.AppendLine("      <Education>");

            var started = Safe(edu, "Started On");
            if (string.IsNullOrEmpty(started)) started = Safe(edu, "Start Date");
            var finished = Safe(edu, "Finished On");
            if (string.IsNullOrEmpty(finished)) finished = Safe(edu, "End Date");

            if (!string.IsNullOrEmpty(started))
            {
                sb.AppendLine("        <Period>");
                sb.AppendLine($"          <From>{FormatEuropassDate(started)}</From>");
                if (!string.IsNullOrEmpty(finished))
                    sb.AppendLine($"          <To>{FormatEuropassDate(finished)}</To>");
                sb.AppendLine("        </Period>");
            }

            var titleParts = new[] { degree, field }.Where(x => !string.IsNullOrEmpty(x));
            var eduTitle = titleParts.Any() ? string.Join(" – ", titleParts) : "Education";
            sb.AppendLine($"        <Title>{EscapeXml(eduTitle)}</Title>");

            if (!string.IsNullOrEmpty(notes))
                sb.AppendLine($"        <Activities>{EscapeXml(notes)}</Activities>");

            sb.AppendLine("        <Organisation>");
            sb.AppendLine($"          <Name>{EscapeXml(school)}</Name>");
            sb.AppendLine("        </Organisation>");

            sb.AppendLine("      </Education>");
        }
        sb.AppendLine("    </EducationList>");
        sb.AppendLine();
    }

    // Skills
    sb.AppendLine("    <Skills>");

    // Mother tongue and foreign languages
    if (data.TryGetValue("LANGUAGES", out var languages) && languages.Count > 0)
    {
        sb.AppendLine("      <Linguistic>");
        var motherTongues = languages.Where(l =>
        {
            var prof = Safe(l, "Proficiency").ToLowerInvariant();
            return prof.Contains("native") || prof.Contains("bilingual");
        }).ToList();

        var foreignLangs = languages.Where(l =>
        {
            var prof = Safe(l, "Proficiency").ToLowerInvariant();
            return !prof.Contains("native") && !prof.Contains("bilingual");
        }).ToList();

        if (motherTongues.Count > 0)
        {
            sb.AppendLine("        <MotherTongueList>");
            foreach (var mt in motherTongues)
            {
                var langName = NormalizeLanguageName(Safe(mt, "Name"));
                var langCode = LanguageToCode(langName);
                sb.AppendLine("          <MotherTongue>");
                sb.AppendLine($"            <Description><Code>{langCode}</Code><Label>{EscapeXml(langName)}</Label></Description>");
                sb.AppendLine("          </MotherTongue>");
            }
            sb.AppendLine("        </MotherTongueList>");
        }

        if (foreignLangs.Count > 0)
        {
            sb.AppendLine("        <ForeignLanguageList>");
            foreach (var fl in foreignLangs)
            {
                var langName = NormalizeLanguageName(Safe(fl, "Name"));
                var langCode = LanguageToCode(langName);
                var proficiency = Safe(fl, "Proficiency").ToLowerInvariant();
                var cefrLevel = ProficiencyToCefr(proficiency);

                sb.AppendLine("          <ForeignLanguage>");
                sb.AppendLine($"            <Description><Code>{langCode}</Code><Label>{EscapeXml(langName)}</Label></Description>");
                sb.AppendLine($"            <ProficiencyLevel>");
                sb.AppendLine($"              <Listening>{cefrLevel}</Listening>");
                sb.AppendLine($"              <Reading>{cefrLevel}</Reading>");
                sb.AppendLine($"              <SpokenInteraction>{cefrLevel}</SpokenInteraction>");
                sb.AppendLine($"              <SpokenProduction>{cefrLevel}</SpokenProduction>");
                sb.AppendLine($"              <Writing>{cefrLevel}</Writing>");
                sb.AppendLine($"            </ProficiencyLevel>");
                sb.AppendLine("          </ForeignLanguage>");
            }
            sb.AppendLine("        </ForeignLanguageList>");
        }
        sb.AppendLine("      </Linguistic>");
    }

    // Other skills — include summary as a catch-all
    if (!string.IsNullOrEmpty(summary))
    {
        var cleanSummary = summary.Replace("  ", " ").Replace("\n", " ").Trim();
        if (cleanSummary.Length > 2000) cleanSummary = cleanSummary[..2000];
        sb.AppendLine($"      <Other><Description>{EscapeXml(cleanSummary)}</Description></Other>");
    }

    // Digital / Computer skills from Skills domain
    if (data.TryGetValue("SKILLS", out var europassSkills) && europassSkills.Count > 0)
    {
        var skillNames = europassSkills
            .Select(s => Safe(s, "Name").Length > 0 ? Safe(s, "Name") : Safe(s, "Skill"))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        var excludeSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Lingua inglese", "Pianificazione delle capacità", "Lavoro di squadra",
            "Analisi dei dati", "Sviluppo di prodotto", "Infrastrutture",
            "Applicazioni Web", "Sviluppo di software", "Integrazione continua",
            "Social media", "Time management", "Customer Requirements", "Computer Ethics"
        };
        var filtered = skillNames.Where(s => !excludeSkills.Contains(s)).ToList();

        if (filtered.Count > 0)
        {
            sb.AppendLine($"      <Computer><Description>{EscapeXml(string.Join(", ", filtered))}</Description></Computer>");
        }
    }

    sb.AppendLine("    </Skills>");
    sb.AppendLine();

    // Achievement list (Certifications)
    if (data.TryGetValue("CERTIFICATIONS", out var euroCerts) && euroCerts.Count > 0)
    {
        sb.AppendLine("    <AchievementList>");
        foreach (var c in euroCerts)
        {
            var name = Safe(c, "Name");
            var authority = Safe(c, "Authority");
            var url = Safe(c, "Url");
            var startedOn = Safe(c, "Started On");

            sb.AppendLine("      <Achievement>");
            sb.AppendLine("        <Title>");
            sb.AppendLine($"          <Label>{EscapeXml(name)}</Label>");
            sb.AppendLine("        </Title>");
            if (!string.IsNullOrEmpty(authority))
                sb.AppendLine($"        <Description>{EscapeXml($"Issued by {authority}")}</Description>");
            if (!string.IsNullOrEmpty(startedOn))
                sb.AppendLine($"        <Date>{FormatEuropassDate(startedOn)}</Date>");
            if (!string.IsNullOrEmpty(url))
                sb.AppendLine($"        <ReferenceTo><Label>{EscapeXml(url)}</Label></ReferenceTo>");
            sb.AppendLine("      </Achievement>");
        }
        sb.AppendLine("    </AchievementList>");
        sb.AppendLine();
    }

    sb.AppendLine("  </LearnerInfo>");
    sb.AppendLine("</SkillsPassport>");

    return sb.ToString();
}

string FormatEuropassDate(string dateStr)
{
    // Input: "Jan 2024", "Sep 2017", etc.
    // Output: <Year>2024</Year><Month>1</Month> or similar
    if (DateTime.TryParseExact(dateStr, "MMM yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
    {
        return $"<Year>{dt.Year}</Year><Month>--{dt.Month:D2}</Month>";
    }
    // Try just year
    if (int.TryParse(dateStr, out var year))
        return $"<Year>{year}</Year>";
    return $"<Year>{dateStr}</Year>";
}

string EscapeXml(string text)
{
    if (string.IsNullOrEmpty(text)) return "";
    return text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");
}

string LanguageToCode(string language)
{
    return language.ToLowerInvariant() switch
    {
        "italian" => "it",
        "english" => "en",
        "french" => "fr",
        "spanish" => "es",
        "german" => "de",
        "portuguese" => "pt",
        _ => language.Length >= 2 ? language[..2].ToLowerInvariant() : "xx"
    };
}

string ProficiencyToCefr(string proficiency)
{
    // Map LinkedIn proficiency levels to CEFR
    if (proficiency.Contains("native") || proficiency.Contains("bilingual")) return "C2";
    if (proficiency.Contains("full professional")) return "C1";
    if (proficiency.Contains("professional working")) return "B2";
    if (proficiency.Contains("limited working")) return "B1";
    if (proficiency.Contains("elementary")) return "A2";
    return "B2"; // Default for unrecognized
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
        return $"{started} – {finished}";
    if (!string.IsNullOrEmpty(started))
        return $"{started} – Present";
    return "";
}

DateTime ParseDate(string dateStr)
{
    // Handles formats like "Jan 2024", "Sep 2017", "Feb 2014"
    if (DateTime.TryParseExact(dateStr, "MMM yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
        return dt;
    return DateTime.MinValue;
}

string GetScriptDirectory()
{
    // In a dotnet run file-based app the working directory is used.
    // We look for the .cs file relative to the current directory or use CWD/scripts
    var candidates = new[]
    {
        Path.Combine(Environment.CurrentDirectory, "scripts"),
        Environment.CurrentDirectory
    };

    foreach (var dir in candidates)
    {
        if (File.Exists(Path.Combine(dir, "fetch_linkedin.cs")))
            return Path.GetFullPath(dir);
    }

    return Path.GetFullPath("scripts");
}
