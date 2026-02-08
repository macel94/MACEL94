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
    Console.Error.WriteLine("❌  No data returned. Check your token and that snapshot processing is complete.");
    return 1;
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

    // ── Header / Profile ─────────────────────────────────────────────
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

    sb.AppendLine($"# 👋 Hi, I'm {fullName}");
    sb.AppendLine();
    if (!string.IsNullOrEmpty(headline))
    {
        sb.AppendLine($"**{headline}**");
        sb.AppendLine();
    }
    if (!string.IsNullOrEmpty(summary))
    {
        sb.AppendLine(summary);
        sb.AppendLine();
    }

    var geo = Safe(profile, "Geo Location");
    var industry = Safe(profile, "Industry");
    var metaParts = new[] { geo, industry }.Where(x => !string.IsNullOrEmpty(x));
    if (metaParts.Any())
    {
        sb.AppendLine($"📍 {string.Join(" · ", metaParts)}");
        sb.AppendLine();
    }

    // ── Experience / Positions ────────────────────────────────────────
    if (data.TryGetValue("POSITIONS", out var positions) && positions.Count > 0)
    {
        sb.AppendLine("## 💼 Experience");
        sb.AppendLine();
        foreach (var pos in positions)
        {
            var title = Safe(pos, "Title");
            var company = Safe(pos, "Company Name");
            var location = Safe(pos, "Location");
            var description = Safe(pos, "Description");
            var dates = DateStr(pos);

            var header = !string.IsNullOrEmpty(title) ? $"**{title}**" : "";
            if (!string.IsNullOrEmpty(company)) header += $" @ {company}";
            if (!string.IsNullOrEmpty(location)) header += $" · {location}";
            if (!string.IsNullOrEmpty(dates)) header += $" ({dates})";
            sb.AppendLine($"- {header}");

            if (!string.IsNullOrEmpty(description))
            {
                foreach (var line in description.Split('\n'))
                    sb.AppendLine($"  > {line.Trim()}");
            }
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
            var parts = new[] { degree, field }.Where(x => !string.IsNullOrEmpty(x));
            if (parts.Any()) header += $" – {string.Join(", ", parts)}";
            if (!string.IsNullOrEmpty(dates)) header += $" ({dates})";
            sb.AppendLine($"- {header}");
            if (!string.IsNullOrEmpty(notes)) sb.AppendLine($"  > {notes}");
            if (!string.IsNullOrEmpty(activities)) sb.AppendLine($"  > Activities: {activities}");
        }
        sb.AppendLine();
    }

    // ── Skills ────────────────────────────────────────────────────────
    if (data.TryGetValue("SKILLS", out var skills) && skills.Count > 0)
    {
        sb.AppendLine("## 🛠 Skills");
        sb.AppendLine();
        var skillNames = skills
            .Select(s => Safe(s, "Name").Length > 0 ? Safe(s, "Name") : Safe(s, "Skill"))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
        if (skillNames.Count > 0)
            sb.AppendLine(string.Join(" · ", skillNames.Select(s => $"`{s}`")));
        sb.AppendLine();
    }

    // ── Certifications ────────────────────────────────────────────────
    if (data.TryGetValue("CERTIFICATIONS", out var certs) && certs.Count > 0)
    {
        sb.AppendLine("## 📜 Certifications");
        sb.AppendLine();
        foreach (var c in certs)
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
            var entry = !string.IsNullOrEmpty(name) ? $"**{name}**" : "";
            if (!string.IsNullOrEmpty(proficiency)) entry += $" ({proficiency})";
            sb.AppendLine($"- {entry}");
        }
        sb.AppendLine();
    }

    // ── Courses ───────────────────────────────────────────────────────
    if (data.TryGetValue("COURSES", out var courses) && courses.Count > 0)
    {
        sb.AppendLine("## 📚 Courses");
        sb.AppendLine();
        foreach (var c in courses)
        {
            var name = Safe(c, "Name");
            var number = Safe(c, "Number");
            var entry = !string.IsNullOrEmpty(name) ? $"**{name}**" : "";
            if (!string.IsNullOrEmpty(number)) entry += $" ({number})";
            sb.AppendLine($"- {entry}");
        }
        sb.AppendLine();
    }

    // ── Honors ────────────────────────────────────────────────────────
    if (data.TryGetValue("HONORS", out var honors) && honors.Count > 0)
    {
        sb.AppendLine("## 🏅 Honors & Awards");
        sb.AppendLine();
        foreach (var h in honors)
        {
            var title = Safe(h, "Title");
            var issuer = Safe(h, "Issuer");
            var entry = !string.IsNullOrEmpty(title) ? $"**{title}**" : "";
            if (!string.IsNullOrEmpty(issuer)) entry += $" – {issuer}";
            sb.AppendLine($"- {entry}");
        }
        sb.AppendLine();
    }

    // ── Publications ──────────────────────────────────────────────────
    if (data.TryGetValue("PUBLICATIONS", out var publications) && publications.Count > 0)
    {
        sb.AppendLine("## 📝 Publications");
        sb.AppendLine();
        foreach (var pub in publications)
        {
            var title = Safe(pub, "Name");
            if (string.IsNullOrEmpty(title)) title = Safe(pub, "Title");
            var publisher = Safe(pub, "Publisher");
            var url = Safe(pub, "Url");
            var entry = !string.IsNullOrEmpty(title) ? $"**{title}**" : "";
            if (!string.IsNullOrEmpty(publisher)) entry += $" – {publisher}";
            if (!string.IsNullOrEmpty(url)) entry = $"[{entry}]({url})";
            sb.AppendLine($"- {entry}");
        }
        sb.AppendLine();
    }

    // ── Patents ───────────────────────────────────────────────────────
    if (data.TryGetValue("PATENTS", out var patents) && patents.Count > 0)
    {
        sb.AppendLine("## 🔬 Patents");
        sb.AppendLine();
        foreach (var pat in patents)
        {
            var title = Safe(pat, "Title");
            if (string.IsNullOrEmpty(title)) title = Safe(pat, "Name");
            var number = Safe(pat, "Patent Number");
            if (string.IsNullOrEmpty(number)) number = Safe(pat, "Number");
            var entry = !string.IsNullOrEmpty(title) ? $"**{title}**" : "";
            if (!string.IsNullOrEmpty(number)) entry += $" (#{number})";
            sb.AppendLine($"- {entry}");
        }
        sb.AppendLine();
    }

    // ── Projects ──────────────────────────────────────────────────────
    if (data.TryGetValue("PROJECTS", out var projects) && projects.Count > 0)
    {
        sb.AppendLine("## 🚀 Projects");
        sb.AppendLine();
        foreach (var proj in projects)
        {
            var title = Safe(proj, "Title");
            if (string.IsNullOrEmpty(title)) title = Safe(proj, "Name");
            var description = Safe(proj, "Description");
            var url = Safe(proj, "Url");
            var dates = DateStr(proj);

            var header = !string.IsNullOrEmpty(title) ? $"**{title}**" : "";
            if (!string.IsNullOrEmpty(dates)) header += $" ({dates})";
            if (!string.IsNullOrEmpty(url)) header = $"[{header}]({url})";
            sb.AppendLine($"- {header}");
            if (!string.IsNullOrEmpty(description)) sb.AppendLine($"  > {description}");
        }
        sb.AppendLine();
    }

    // ── Organizations ─────────────────────────────────────────────────
    if (data.TryGetValue("ORGANIZATIONS", out var orgs) && orgs.Count > 0)
    {
        sb.AppendLine("## 🏢 Organizations");
        sb.AppendLine();
        foreach (var org in orgs)
        {
            var name = Safe(org, "Name");
            var position = Safe(org, "Position");
            if (string.IsNullOrEmpty(position)) position = Safe(org, "Title");
            var entry = !string.IsNullOrEmpty(name) ? $"**{name}**" : "";
            if (!string.IsNullOrEmpty(position)) entry += $" – {position}";
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
            .Take(5)
            .ToList();

        if (received.Count > 0)
        {
            sb.AppendLine("## 💬 Recommendations Received");
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
                    var truncated = text.Length > 300 ? text[..300] + "..." : text;
                    sb.AppendLine($"> *\"{truncated}\"*");
                    if (!string.IsNullOrEmpty(recommender))
                        sb.AppendLine($"> — {recommender}");
                    sb.AppendLine();
                }
            }
        }
    }

    // ── Causes ────────────────────────────────────────────────────────
    if (data.TryGetValue("CAUSES_YOU_CARE_ABOUT", out var causes) && causes.Count > 0)
    {
        sb.AppendLine("## ❤️ Causes I Care About");
        sb.AppendLine();
        var causeNames = causes
            .Select(c => Safe(c, "Name").Length > 0 ? Safe(c, "Name") : Safe(c, "Cause"))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
        if (causeNames.Count > 0)
            sb.AppendLine(string.Join(", ", causeNames));
        sb.AppendLine();
    }

    // ── Footer ────────────────────────────────────────────────────────
    var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
    sb.AppendLine("---");
    sb.AppendLine();
    sb.AppendLine(
        $"<sub>🔄 Auto-generated from LinkedIn via " +
        $"[DMA Data Portability API](https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/) " +
        $"· Last updated: {now}</sub>");
    sb.AppendLine();

    return sb.ToString();
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
