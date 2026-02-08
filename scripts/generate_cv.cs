#!/usr/bin/env dotnet run
// Generate Europass CV XML from cached LinkedIn data.
// The PDF is produced by the CI pipeline (pandoc converts the README).
//
// Reads: scripts/linkedin_raw.json  (produced by fetch_linkedin.cs)
// Writes: europass_cv.xml
//
// Usage:
//   dotnet run scripts/generate_cv.cs

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

// Generate Europass CV XML
var europassXml = GenerateEuropassXml(allData);
var europassPath = Path.Combine(csFilePath, "..", "europass_cv.xml");
europassPath = Path.GetFullPath(europassPath);
File.WriteAllText(europassPath, europassXml);
Console.Error.WriteLine($"✅  Europass CV XML written to {europassPath}");

return 0;

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
            var eduTitle = titleParts.Any() ? string.Join(" - ", titleParts) : "Education";
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

// ── Europass Helpers ─────────────────────────────────────────────────

string FormatEuropassDate(string dateStr)
{
    if (DateTime.TryParseExact(dateStr, "MMM yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
    {
        return $"<Year>{dt.Year}</Year><Month>--{dt.Month:D2}</Month>";
    }
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
    if (proficiency.Contains("native") || proficiency.Contains("bilingual")) return "C2";
    if (proficiency.Contains("full professional")) return "C1";
    if (proficiency.Contains("professional working")) return "B2";
    if (proficiency.Contains("limited working")) return "B1";
    if (proficiency.Contains("elementary")) return "A2";
    return "B2";
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

string GetScriptDirectory()
{
    var candidates = new[]
    {
        Path.Combine(Environment.CurrentDirectory, "scripts"),
        Environment.CurrentDirectory
    };

    foreach (var dir in candidates)
    {
        if (File.Exists(Path.Combine(dir, "generate_cv.cs")))
            return Path.GetFullPath(dir);
    }

    return Path.GetFullPath("scripts");
}
