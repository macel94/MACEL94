#!/usr/bin/env dotnet run
// Generate a compact, ATS-friendly one-page resume from cached LinkedIn data.
//
// Reads:  src/linkedin_raw.json (produced by fetch_linkedin.cs)
// Writes: slim/README.md
//
// The slim generator is intentionally independent from generate_readme.cs and
// generate_cv.cs. It never writes README.md, artifacts/, or role-specific CVs.
//
// Usage:
//   dotnet run src/generate_slim.cs
//
// Optional environment variables used for local testing/alternate output:
//   SLIM_RAW_PATH    Path to a LinkedIn snapshot JSON file.
//   SLIM_OUTPUT_DIR  Output directory (defaults to slim/).

#:property PublishAot=false

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string LinkedInUrl = "https://www.linkedin.com/in/francesco-belacca-dev/";
const string GitHubUrl = "https://github.com/macel94";
const string EmailAddress = "francesco.belacca@hotmail.it";

var scriptDirectory = GetScriptDirectory();
var rawPath = Environment.GetEnvironmentVariable("SLIM_RAW_PATH")
              ?? Path.Combine(scriptDirectory, "linkedin_raw.json");

if (!File.Exists(rawPath))
{
    Console.Error.WriteLine($"❌  {rawPath} not found. Run fetch_linkedin.cs first.");
    return 1;
}

Dictionary<string, List<JsonElement>> allData;
try
{
    var rawJson = File.ReadAllText(rawPath);
    allData = JsonSerializer.Deserialize<Dictionary<string, List<JsonElement>>>(rawJson)
              ?? new Dictionary<string, List<JsonElement>>();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"❌  Could not read {rawPath}: {ex.Message}");
    return 1;
}

if (allData.Count == 0)
{
    Console.Error.WriteLine("❌  LinkedIn snapshot is empty.");
    return 1;
}

var repoRoot = Path.GetFullPath(Path.Combine(scriptDirectory, ".."));
var outputDirectory = Environment.GetEnvironmentVariable("SLIM_OUTPUT_DIR")
                      ?? Path.Combine(repoRoot, "slim");
Directory.CreateDirectory(outputDirectory);

var markdown = GenerateSlimMarkdown(allData);
var outputPath = Path.GetFullPath(Path.Combine(outputDirectory, "README.md"));
File.WriteAllText(outputPath, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.Error.WriteLine($"✅  Slim resume written to {outputPath}");

return 0;

// ── Resume generation ────────────────────────────────────────────────

string GenerateSlimMarkdown(Dictionary<string, List<JsonElement>> data)
{
    var profile = GetFirst(data, "PROFILE");
    var firstName = Safe(profile, "First Name");
    var lastName = Safe(profile, "Last Name");
    var fullName = string.Join(" ", new[] { firstName, lastName }
        .Where(value => !string.IsNullOrWhiteSpace(value)))
        .Trim();
    if (string.IsNullOrWhiteSpace(fullName))
        fullName = "Francesco Belacca";

    var headline = CleanPlainText(Safe(profile, "Headline"));
    if (string.IsNullOrWhiteSpace(headline))
        headline = "Senior Site Reliability Engineer | DevOps | Azure";

    var location = ResumeLocation(Safe(profile, "Geo Location"));
    var positions = GetPositions(data);
    var evidence = BuildEvidence(data, positions, headline);
    var skills = BuildSkills(data, evidence);
    var summary = BuildSummary(data, profile, headline, positions);

    var sb = new StringBuilder();

    // Contact details are deliberately in the document body, not a header or footer.
    sb.AppendLine($"# {fullName}");
    sb.AppendLine(headline);
    sb.AppendLine(string.Join(" | ", new[]
    {
        location,
        EmailAddress,
        LinkedInUrl,
        GitHubUrl
    }.Where(value => !string.IsNullOrWhiteSpace(value))));
    sb.AppendLine();

    if (!string.IsNullOrWhiteSpace(summary))
    {
        sb.AppendLine("## SUMMARY");
        sb.AppendLine(summary);
        sb.AppendLine();
    }

    if (!string.IsNullOrWhiteSpace(skills))
    {
        sb.AppendLine("## SKILLS");
        sb.AppendLine(skills);
        sb.AppendLine();
    }

    if (positions.Count > 0)
    {
        sb.AppendLine("## EXPERIENCE");
        // A one-page resume should emphasize the most recent, relevant work;
        // older roles remain searchable in compact lines below.
        var featuredCount = Math.Min(3, positions.Count);

        for (var index = 0; index < featuredCount; index++)
        {
            AppendFeaturedPosition(sb, positions[index]);
        }

        if (positions.Count > featuredCount)
        {
            sb.AppendLine("### Earlier Experience");
            foreach (var position in positions.Skip(featuredCount).Take(5))
            {
                var compactHeader = PositionHeader(position);
                var compactDates = DateRange(position);
                var compactLocation = ResumeLocation(Safe(position, "Location"));
                var details = new[] { compactHeader, compactLocation, compactDates }
                    .Where(value => !string.IsNullOrWhiteSpace(value));
                sb.AppendLine($"- {string.Join(" | ", details)}");
            }
            sb.AppendLine();
        }
    }

    var certifications = GetCertifications(data);
    if (certifications.Count > 0)
    {
        sb.AppendLine("## CERTIFICATIONS");
        foreach (var certification in certifications)
            sb.AppendLine($"- {certification}");
        sb.AppendLine();
    }

    var education = GetEducation(data);
    if (education.Count > 0)
    {
        sb.AppendLine("## EDUCATION");
        foreach (var entry in education)
            sb.AppendLine($"- {entry}");
        sb.AppendLine();
    }

    var languages = GetLanguages(data);
    if (languages.Count > 0)
    {
        sb.AppendLine("## LANGUAGES");
        sb.AppendLine(string.Join("; ", languages));
        sb.AppendLine();
    }

    return sb.ToString().TrimEnd() + Environment.NewLine;
}

void AppendFeaturedPosition(StringBuilder sb, JsonElement position)
{
    var header = PositionHeader(position);
    var location = ResumeLocation(Safe(position, "Location"));
    var dates = DateRange(position);

    sb.AppendLine($"### {header}");
    var metadata = new[] { location, dates }
        .Where(value => !string.IsNullOrWhiteSpace(value));
    if (metadata.Any())
        sb.AppendLine(string.Join(" | ", metadata));

    foreach (var bullet in GetBullets(position))
        sb.AppendLine($"- {bullet}");
    sb.AppendLine();
}

string PositionHeader(JsonElement position)
{
    var title = CleanPlainText(Safe(position, "Title"));
    var company = CleanPlainText(Safe(position, "Company Name"));

    if (string.IsNullOrWhiteSpace(title)) return company;
    if (string.IsNullOrWhiteSpace(company)) return title;
    return $"{title} | {company}";
}

List<JsonElement> GetPositions(Dictionary<string, List<JsonElement>> data)
{
    if (!data.TryGetValue("POSITIONS", out var positions))
        return new List<JsonElement>();

    return positions
        .Where(position => !string.IsNullOrWhiteSpace(Safe(position, "Title")) ||
                           !string.IsNullOrWhiteSpace(Safe(position, "Company Name")))
        .OrderByDescending(position => ParseDate(FirstNonEmpty(
            Safe(position, "Started On"), Safe(position, "Start Date"))))
        .ToList();
}

string BuildSummary(
    Dictionary<string, List<JsonElement>> data,
    JsonElement profile,
    string headline,
    List<JsonElement> positions)
{
    var summaryBlock = GetFirst(data, "PROFILE_SUMMARY");
    var rawSummary = FirstNonEmpty(
        Safe(summaryBlock, "Summary"),
        Safe(summaryBlock, "summary"),
        Safe(profile, "Summary"));

    var clean = NormalizeText(rawSummary);
    var hasVerifiedScale = rawSummary.Contains("85,000+ users", StringComparison.OrdinalIgnoreCase) &&
                           rawSummary.Contains("500+ companies", StringComparison.OrdinalIgnoreCase);
    clean = CutAtFirstMarker(clean, "Key Competencies:", "Core Competencies:", "My Mission:", "Based in");

    var cleanHeadline = NormalizeText(headline);
    if (!string.IsNullOrWhiteSpace(clean) &&
        clean.StartsWith(cleanHeadline, StringComparison.OrdinalIgnoreCase))
    {
        // The headline is already in the contact block; do not spend summary
        // space repeating it. This also keeps the profile more scannable.
        clean = clean[cleanHeadline.Length..].Trim(' ', '.', ':', '|', '-');
    }

    // Resume guidance favors concise, fact-based prose over first-person
    // narrative. The source LinkedIn summary is written in first person, so
    // replace it with a deterministic summary built only from verified fields.
    if (Regex.IsMatch(clean, @"\b(?:I|I['’]m|I['’]ve|my)\b", RegexOptions.IgnoreCase))
    {
        var years = CalculateExperienceYears(positions);
        var yearsText = years > 0 ? $" with {years}+ years of experience" : "";
        var summaryTitle = positions.Count > 0
            ? CleanPlainText(Safe(positions[0], "Title"))
            : "DevOps and Site Reliability Engineer";
        var scaleSentence = hasVerifiedScale
            ? " Operates in a single-tenant environment supporting 85,000+ users across 500+ companies."
            : "";
        return $"{summaryTitle}{yearsText} building, securing, and scaling Azure cloud infrastructure, .NET services, and delivery automation. Expertise spans CI/CD, Infrastructure as Code, cloud governance, reliability engineering, and distributed systems.{scaleSentence}";
    }

    // Keep the summary to a few lines so the document remains one page while
    // leaving room for experience, certifications, and education.
    clean = LimitAtSentenceBoundary(clean, 420);
    if (!string.IsNullOrWhiteSpace(clean))
        return clean;

    var fallbackYears = CalculateExperienceYears(positions);
    var fallbackYearsText = fallbackYears > 0 ? $" with {fallbackYears}+ years of experience" : "";
    var currentTitle = positions.Count > 0
        ? CleanPlainText(Safe(positions[0], "Title"))
        : headline;

    return $"{currentTitle}{fallbackYearsText} building, securing, and scaling cloud infrastructure, software, and delivery automation across Azure and .NET environments.";
}

string BuildEvidence(
    Dictionary<string, List<JsonElement>> data,
    List<JsonElement> positions,
    string headline)
{
    var fragments = new List<string> { headline };

    if (data.TryGetValue("PROFILE_SUMMARY", out var summaries))
        fragments.AddRange(summaries.Select(summary => Safe(summary, "Summary")));
    if (data.TryGetValue("SKILLS", out var skills))
        fragments.AddRange(skills.Select(skill => FirstNonEmpty(Safe(skill, "Name"), Safe(skill, "Skill"))));

    foreach (var position in positions)
    {
        fragments.Add(Safe(position, "Title"));
        fragments.Add(Safe(position, "Description"));
    }

    return NormalizeText(string.Join(" ", fragments));
}

string BuildSkills(Dictionary<string, List<JsonElement>> data, string evidence)
{
    var explicitSkills = new List<string>();
    if (data.TryGetValue("SKILLS", out var skills))
    {
        explicitSkills.AddRange(skills
            .Select(skill => FirstNonEmpty(Safe(skill, "Name"), Safe(skill, "Skill")))
            .Where(skill => !string.IsNullOrWhiteSpace(skill)));
    }

    var skillEvidence = string.Join(" ", explicitSkills) + " " + evidence;
    var definitions = new (string Label, string[] Terms)[]
    {
        ("Azure", ["Azure", "Windows Azure"]),
        ("Azure DevOps", ["Azure DevOps", "AZURE DEVOPS"]),
        ("GitHub Actions", ["GitHub Actions"]),
        ("GitHub Enterprise", ["GitHub Enterprise"]),
        ("Bicep", ["Bicep"]),
        ("Infrastructure as Code (IaC)", ["Infrastructure as Code", "IaC"]),
        ("CI/CD", ["CI/CD", "continuous integration", "continuous delivery"]),
        ("PowerShell", ["PowerShell", "Powershell"]),
        (".NET", [".NET", ".net core", ".net framework"]),
        ("C#", ["C#"]),
        ("APIs", ["API"]),
        ("Microservices", ["microservices"]),
        ("Azure Kubernetes Service (AKS)", ["Azure Kubernetes Service", "AKS"]),
        ("Docker", ["Docker", "containerized", "containers"]),
        ("Helm", ["Helm"]),
        ("Prometheus", ["Prometheus"]),
        ("Grafana", ["Grafana"]),
        ("JMeter", ["JMeter"]),
        ("Chaos Mesh", ["Chaos Mesh"]),
        ("Azure Load Testing", ["Azure Load Testing"]),
        ("Microsoft Entra ID", ["Entra ID", "Microsoft Entra"]),
        ("Azure Key Vault", ["Key Vault"]),
        ("Managed Identity", ["Managed Identity"]),
        ("RBAC", ["RBAC", "role-based access"]),
        ("DevSecOps", ["DevSecOps"]),
        ("SQL", ["SQL", "T-SQL", "Microsoft SQL"]),
        ("Elasticsearch", ["Elasticsearch", "ElasticSearch"]),
        ("Blazor", ["Blazor"]),
        ("Microsoft Dynamics 365", ["Dynamics 365"]),
        ("Microsoft Business Central", ["Business Central"]),
        ("Microsoft Power Platform", ["Power Platform"]),
        ("Microsoft Fabric", ["Microsoft Fabric"])
    };

    var selected = new List<string>();
    foreach (var definition in definitions)
    {
        if (ContainsAny(skillEvidence, definition.Terms))
            selected.Add(definition.Label);
    }

    // Keep a useful but scannable skills line if a future LinkedIn snapshot
    // contains unfamiliar skills not covered by the curated ATS vocabulary.
    var knownLower = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
    foreach (var skill in explicitSkills)
    {
        var cleaned = CleanPlainText(skill);
        if (string.IsNullOrWhiteSpace(cleaned) ||
            cleaned.Length > 40 ||
            knownLower.Contains(cleaned) ||
            selected.Count >= 36)
            continue;

        selected.Add(cleaned);
        knownLower.Add(cleaned);
    }

    return string.Join(", ", selected);
}

List<string> GetBullets(JsonElement position)
{
    var description = Safe(position, "Description");
    var parts = SplitDescription(description);
    var candidates = new List<(string Text, int Index, int Score)>();

    for (var index = 0; index < parts.Count; index++)
    {
        var cleaned = CleanBullet(parts[index]);
        if (string.IsNullOrWhiteSpace(cleaned) || IsLowValueBullet(cleaned))
            continue;

        candidates.Add((cleaned, index, ScoreBullet(cleaned)));
    }

    var selected = candidates
        .OrderByDescending(candidate => candidate.Score)
        .ThenBy(candidate => candidate.Index)
        .Take(3)
        .OrderBy(candidate => candidate.Index)
        .Select(candidate => candidate.Text)
        .ToList();

    return selected;
}

List<string> SplitDescription(string description)
{
    var text = CleanPlainText(description);
    if (string.IsNullOrWhiteSpace(text))
        return new List<string>();

    text = Regex.Replace(text, @"\s*Focus areas\s*:.*$", "", RegexOptions.IgnoreCase);

    var coarseParts = Regex.Split(text, @"(?:\r?\n+|\s{2,})")
        .Select(part => part.Trim())
        .Where(part => !string.IsNullOrWhiteSpace(part))
        .ToList();

    var result = new List<string>();
    foreach (var coarsePart in coarseParts)
    {
        if (coarsePart.Length <= 240)
        {
            result.Add(coarsePart);
            continue;
        }

        var sentenceParts = Regex.Split(coarsePart, @"(?<=[.!?])\s+(?=[A-Z0-9])")
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part));
        result.AddRange(sentenceParts);
    }

    if (result.Count == 1)
    {
        var sentenceParts = Regex.Split(result[0], @"(?<=[.!?])\s+(?=[A-Z0-9])")
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
        if (sentenceParts.Count > 1)
            result = sentenceParts;
    }

    return result;
}

string CleanBullet(string value)
{
    var text = NormalizeText(value).Trim(' ', '-', '•', '*');
    if (string.IsNullOrWhiteSpace(text)) return "";

    var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Rolling out "] = "Rolled out ",
        ["Lead admin/governance "] = "Led administration and governance ",
        ["Partner with "] = "Partnered with ",
        ["Collaborate with "] = "Collaborated with ",
        ["Administer and govern "] = "Administered and governed ",
        ["Architect and maintain "] = "Architected and maintain ",
        ["Develop and deploy "] = "Developed and deployed ",
        ["Perform end-to-end "] = "Performed end-to-end "
    };

    foreach (var replacement in replacements)
    {
        if (text.StartsWith(replacement.Key, StringComparison.OrdinalIgnoreCase))
        {
            text = replacement.Value + text[replacement.Key.Length..];
            break;
        }
    }

    if (text.StartsWith("I ", StringComparison.OrdinalIgnoreCase))
        text = text[2..];

    text = LimitAtWord(text, 260).TrimEnd('.', ';', ':', ',');
    return text + ".";
}

bool IsLowValueBullet(string text)
{
    var lower = text.ToLowerInvariant();
    return lower.StartsWith("i’ve been ") ||
           lower.StartsWith("i've been ") ||
           lower.StartsWith("continued within ") ||
           lower.StartsWith("worked as a consultant") ||
           lower.StartsWith("always worked in team") ||
           lower.Length < 24;
}

int ScoreBullet(string text)
{
    var lower = text.ToLowerInvariant();
    var actionVerbs = new[]
    {
        "built", "developed", "designed", "led", "implemented", "deployed",
        "delivered", "created", "wrote", "rolled", "drove", "performed",
        "standardized", "managed", "defined", "engineered", "automated",
        "administered", "partnered", "collaborated", "conducted"
    };
    var keywords = new[]
    {
        "azure", "bicep", "ci/cd", "github", "devops", "kubernetes", "aks",
        "powerShell", " .net", ".net", "api", "microservice", "security",
        "key vault", "managed identity", "rbac", "load testing", "prometheus",
        "grafana", "chaos mesh", "infrastructure as code", "governance", "sql"
    };

    var score = actionVerbs.Any(lower.StartsWith) ? 4 : 0;
    score += keywords.Count(keyword => lower.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase));
    if (lower.Contains("multiple") || lower.Contains("various") || lower.Contains("etc.")) score--;
    return score;
}

List<string> GetCertifications(Dictionary<string, List<JsonElement>> data)
{
    if (!data.TryGetValue("CERTIFICATIONS", out var certifications))
        return new List<string>();

    return certifications
        .Where(certification =>
            Safe(certification, "Authority").Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            Safe(certification, "Name").Contains("Microsoft Certified", StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(certification => ParseDate(FirstNonEmpty(
            Safe(certification, "Started On"), Safe(certification, "Start Date"))))
        .Take(4)
        .Select(certification =>
        {
            var name = CleanPlainText(Safe(certification, "Name"));
            var authority = CleanPlainText(Safe(certification, "Authority"));
            if (!name.Contains("Microsoft Certified", StringComparison.OrdinalIgnoreCase) &&
                authority.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                name = $"Microsoft Certified: {name}";

            var date = FormatMonthYear(FirstNonEmpty(
                Safe(certification, "Started On"), Safe(certification, "Start Date")));
            return string.IsNullOrWhiteSpace(date) ? name : $"{name} ({date})";
        })
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToList();
}

List<string> GetEducation(Dictionary<string, List<JsonElement>> data)
{
    if (!data.TryGetValue("EDUCATION", out var education))
        return new List<string>();

    var formal = education
        .Where(entry =>
        {
            var text = string.Join(" ", Safe(entry, "School Name"), Safe(entry, "Degree Name"), Safe(entry, "Notes"));
            return text.Contains("Bachelor", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("University", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Università", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Computer Science", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Informatica", StringComparison.OrdinalIgnoreCase);
        })
        .OrderByDescending(entry => ParseDate(FirstNonEmpty(
            Safe(entry, "End Date"), Safe(entry, "Finished On"))))
        .Take(1)
        .ToList();

    var selected = formal.Count > 0 ? formal : education.Take(1).ToList();
    var result = new List<string>();

    foreach (var entry in selected)
    {
        var school = CleanPlainText(Safe(entry, "School Name"));
        var degree = CleanPlainText(Safe(entry, "Degree Name"));
        var notes = CleanPlainText(Safe(entry, "Notes"));
        var endDate = FormatMonthYear(FirstNonEmpty(
            Safe(entry, "End Date"), Safe(entry, "Finished On")));

        if (degree.Contains("Informatica", StringComparison.OrdinalIgnoreCase) &&
            notes.Contains("Bachelor", StringComparison.OrdinalIgnoreCase))
            degree = "Bachelor's Degree in Applied Computer Science";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(degree)) parts.Add(degree);
        if (!string.IsNullOrWhiteSpace(school)) parts.Add(school);
        var value = string.Join(", ", parts);
        if (!string.IsNullOrWhiteSpace(endDate)) value += $" ({endDate})";
        if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
    }

    return result;
}

List<string> GetLanguages(Dictionary<string, List<JsonElement>> data)
{
    if (!data.TryGetValue("LANGUAGES", out var languages))
        return new List<string>();

    return languages
        .Select(language =>
        {
            var name = NormalizeLanguageName(CleanPlainText(Safe(language, "Name")));
            var proficiency = CleanPlainText(Safe(language, "Proficiency"));
            return string.IsNullOrWhiteSpace(proficiency) ? name : $"{name} - {proficiency}";
        })
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToList();
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

string ResumeLocation(string value)
{
    var clean = CleanPlainText(value);
    if (string.IsNullOrWhiteSpace(clean)) return "";

    var parts = clean.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length >= 3 && parts[^1].Equals("Italy", StringComparison.OrdinalIgnoreCase))
        return $"{parts[0]}, Italy";
    return clean;
}

string DateRange(JsonElement element)
{
    var started = FormatMonthYear(FirstNonEmpty(
        Safe(element, "Started On"), Safe(element, "Start Date")));
    var finishedRaw = FirstNonEmpty(Safe(element, "Finished On"), Safe(element, "End Date"));
    var finished = FormatMonthYear(finishedRaw);

    if (string.IsNullOrWhiteSpace(started)) return finished;
    if (string.IsNullOrWhiteSpace(finished)) return $"{started} - Present";
    return $"{started} - {finished}";
}

int CalculateExperienceYears(List<JsonElement> positions)
{
    var starts = positions
        .Select(position => ParseDate(FirstNonEmpty(
            Safe(position, "Started On"), Safe(position, "Start Date"))))
        .Where(date => date != DateTime.MinValue)
        .ToList();
    if (starts.Count == 0) return 0;

    var years = (int)Math.Floor((DateTime.UtcNow - starts.Min()).TotalDays / 365.25);
    return Math.Max(years, 1);
}

string FormatMonthYear(string value)
{
    var date = ParseDate(value);
    if (date == DateTime.MinValue) return CleanPlainText(value);
    return date.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
}

DateTime ParseDate(string value)
{
    if (string.IsNullOrWhiteSpace(value)) return DateTime.MinValue;

    var formats = new[]
    {
        "MMM yyyy", "MMMM yyyy", "yyyy-MM-dd", "yyyy-MM", "yyyy",
        "d MMM yyyy", "dd MMM yyyy", "d MMMM yyyy", "dd MMMM yyyy"
    };

    if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out var parsed))
        return parsed;

    return DateTime.TryParse(value, CultureInfo.InvariantCulture,
        DateTimeStyles.AllowWhiteSpaces, out parsed) ? parsed : DateTime.MinValue;
}

string NormalizeText(string value)
{
    var text = CleanPlainText(value);
    if (string.IsNullOrWhiteSpace(text)) return "";

    text = text
        .Replace("SpecialistWith", "Specialist. With", StringComparison.Ordinal)
        .Replace("clients.Currently", "clients. Currently", StringComparison.Ordinal)
        .Replace("critical.Key", "critical. Key", StringComparison.Ordinal);

    text = Regex.Replace(text, @"(?<=[a-z])\.(?=[A-Z])", ". ");
    text = Regex.Replace(text, @"[ \t\r\n]+", " ");
    return text.Trim();
}

string CutAtFirstMarker(string value, params string[] markers)
{
    var cut = value.Length;
    foreach (var marker in markers)
    {
        var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0 && index < cut) cut = index;
    }
    return value[..cut].Trim();
}

string LimitAtSentenceBoundary(string value, int maximumLength)
{
    if (value.Length <= maximumLength) return value;

    var candidate = value[..maximumLength];
    var lastStop = candidate.LastIndexOfAny(new[] { '.', '!', '?' });
    if (lastStop >= maximumLength / 2)
        return candidate[..(lastStop + 1)].Trim();
    return LimitAtWord(candidate, maximumLength).TrimEnd() + "…";
}

string LimitAtWord(string value, int maximumLength)
{
    if (value.Length <= maximumLength) return value;
    var cut = value[..maximumLength];
    var lastSpace = cut.LastIndexOf(' ');
    return (lastSpace > maximumLength / 2 ? cut[..lastSpace] : cut).TrimEnd() + "…";
}

string CleanPlainText(string value)
{
    if (string.IsNullOrWhiteSpace(value)) return "";

    var text = WebUtility.HtmlDecode(value);
    text = Regex.Replace(text, @"<[^>]+>", " ");
    text = text.Replace("\u00A0", " ", StringComparison.Ordinal);
    text = Regex.Replace(text, @"\s+", " ");
    return text.Trim();
}

bool ContainsAny(string value, IEnumerable<string> terms)
{
    return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}

string FirstNonEmpty(params string[] values)
{
    return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}

string Safe(JsonElement element, string key)
{
    if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        return "";

    if (element.ValueKind != JsonValueKind.Object)
        return "";

    foreach (var property in element.EnumerateObject())
    {
        if (!property.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
            continue;

        return property.Value.ValueKind switch
        {
            JsonValueKind.String => property.Value.GetString()?.Trim() ?? "",
            JsonValueKind.Number => property.Value.ToString(),
            JsonValueKind.Null => "",
            _ => property.Value.ToString()
        };
    }

    return "";
}

JsonElement GetFirst(Dictionary<string, List<JsonElement>> data, string domain)
{
    if (data.TryGetValue(domain, out var records) && records.Count > 0)
        return records[0];
    return default;
}

string GetScriptDirectory()
{
    var candidates = new[]
    {
        Path.Combine(Environment.CurrentDirectory, "src"),
        Environment.CurrentDirectory
    };

    foreach (var directory in candidates)
    {
        if (File.Exists(Path.Combine(directory, "generate_slim.cs")))
            return Path.GetFullPath(directory);
    }

    return Path.GetFullPath("src");
}
