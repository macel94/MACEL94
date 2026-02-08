#!/usr/bin/env dotnet run
// Fetch LinkedIn profile data via the DMA Member Data Portability API
// and save the raw JSON for downstream scripts (generate_readme.cs, generate_cv.cs).
//
// Requires:
//   - .NET 10+ (file-based app, no .csproj needed)
//   - An access token generated with scope r_dma_portability_self_serve
//     via the LinkedIn Developer Portal OAuth Token Generator Tool.
//   - The token must be from an EU/EEA/Switzerland-based LinkedIn member.
//
// Usage:
//   export LINKEDIN_ACCESS_TOKEN="your_token"
//   dotnet run src/fetch_linkedin.cs
//
// API Reference:
//   https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/

#:property PublishAot=false

using System.Net.Http.Headers;
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
        Console.Error.WriteLine(" - no data");
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

// Save raw JSON for downstream scripts
var csFilePath = GetScriptDirectory();
var rawPath = Path.Combine(csFilePath, "linkedin_raw.json");
var rawJson = JsonSerializer.Serialize(allData, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(rawPath, rawJson);
Console.Error.WriteLine($"💾  Raw data saved to {rawPath}");

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
            Console.Error.WriteLine($"\n  ⚠  {domain}: HTTP {(int)resp.StatusCode} - {body[..Math.Min(body.Length, 200)]}");
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

// ── Helpers ──────────────────────────────────────────────────────────

string GetScriptDirectory()
{
    var candidates = new[]
    {
        Path.Combine(Environment.CurrentDirectory, "src"),
        Environment.CurrentDirectory
    };

    foreach (var dir in candidates)
    {
        if (File.Exists(Path.Combine(dir, "fetch_linkedin.cs")))
            return Path.GetFullPath(dir);
    }

    return Path.GetFullPath("src");
}
