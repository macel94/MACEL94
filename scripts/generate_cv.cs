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
// Generates an XML file conforming to the Europass Candidate XML schema
// (HR-XML / OAGIS namespaces) as used by the official Europass PDF export.
// Reference: attachment.xml extracted via `pdfdetach` from an Europass PDF.

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
    var countryCode = "it";

    var sb = new StringBuilder();

    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
    sb.AppendLine("<Candidate xsi:schemaLocation=\"http://www.europass.eu/1.0 Candidate.xsd\"");
    sb.AppendLine("    xmlns=\"http://www.europass.eu/1.0\"");
    sb.AppendLine("    xmlns:oa=\"http://www.openapplications.org/oagis/9\"");
    sb.AppendLine("    xmlns:eures=\"http://www.europass_eures.eu/1.0\"");
    sb.AppendLine("    xmlns:hr=\"http://www.hr-xml.org/3\"");
    sb.AppendLine("    xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");

    // ── DocumentID ───────────────────────────────────────────────────
    sb.AppendLine("    <hr:DocumentID schemeID=\"Test-0001\" schemeName=\"DocumentIdentifier\" schemeAgencyName=\"EUROPASS\" schemeVersionID=\"4.0\"/>");

    // ── CandidateSupplier ────────────────────────────────────────────
    sb.AppendLine("    <CandidateSupplier>");
    sb.AppendLine("        <hr:PartyID schemeID=\"Test-0001\" schemeName=\"PartyID\" schemeAgencyName=\"EUROPASS\" schemeVersionID=\"1.0\"/>");
    sb.AppendLine("        <hr:PartyName>Owner</hr:PartyName>");
    sb.AppendLine("        <PersonContact>");
    sb.AppendLine("            <PersonName>");
    sb.AppendLine($"                <oa:GivenName>{Esc(first)}</oa:GivenName>");
    sb.AppendLine($"                <hr:FamilyName>{Esc(last)}</hr:FamilyName>");
    sb.AppendLine("            </PersonName>");
    sb.AppendLine("            <Communication>");
    sb.AppendLine("                <ChannelCode>Email</ChannelCode>");
    sb.AppendLine("                <oa:URI>francesco.belacca@hotmail.it</oa:URI>");
    sb.AppendLine("            </Communication>");
    sb.AppendLine("        </PersonContact>");
    sb.AppendLine("        <hr:PrecedenceCode>1</hr:PrecedenceCode>");
    sb.AppendLine("    </CandidateSupplier>");

    // ── CandidatePerson ──────────────────────────────────────────────
    sb.AppendLine("    <CandidatePerson>");
    sb.AppendLine("        <PersonName>");
    sb.AppendLine($"            <oa:GivenName>{Esc(first)}</oa:GivenName>");
    sb.AppendLine($"            <hr:FamilyName>{Esc(last)}</hr:FamilyName>");
    sb.AppendLine("        </PersonName>");
    sb.AppendLine("        <Communication>");
    sb.AppendLine("            <ChannelCode>Email</ChannelCode>");
    sb.AppendLine("            <oa:URI>francesco.belacca@hotmail.it</oa:URI>");
    sb.AppendLine("        </Communication>");
    sb.AppendLine("        <Communication>");
    sb.AppendLine("            <ChannelCode>Web</ChannelCode>");
    sb.AppendLine("            <oa:URI>https://www.linkedin.com/in/fbelacca/</oa:URI>");
    sb.AppendLine("        </Communication>");
    sb.AppendLine("        <Communication>");
    sb.AppendLine("            <ChannelCode>Web</ChannelCode>");
    sb.AppendLine("            <oa:URI>https://github.com/MACEL94</oa:URI>");
    sb.AppendLine("        </Communication>");
    sb.AppendLine("        <Communication>");
    sb.AppendLine("            <ChannelCode>Telephone</ChannelCode>");
    sb.AppendLine("            <UseCode>mobile</UseCode>");
    sb.AppendLine("            <CountryDialing>39</CountryDialing>");
    sb.AppendLine("            <oa:DialNumber>3927192565</oa:DialNumber>");
    sb.AppendLine("            <CountryCode>it</CountryCode>");
    sb.AppendLine("        </Communication>");
    sb.AppendLine("        <Communication>");
    sb.AppendLine("            <UseCode>home</UseCode>");
    sb.AppendLine("            <Address type=\"home\">");
    sb.AppendLine("                <oa:AddressLine>Viale la Grola 29</oa:AddressLine>");
    sb.AppendLine($"                <oa:CityName>{Esc(city)}</oa:CityName>");
    sb.AppendLine($"                <CountryCode>{countryCode}</CountryCode>");
    sb.AppendLine("                <oa:PostalCode>43126</oa:PostalCode>");
    sb.AppendLine("            </Address>");
    sb.AppendLine("        </Communication>");
    sb.AppendLine($"        <NationalityCode>{countryCode}</NationalityCode>");
    sb.AppendLine("        <hr:BirthDate>1994-10-09</hr:BirthDate>");
    sb.AppendLine("        <GenderCode>male</GenderCode>");
    sb.AppendLine("        <PrimaryLanguageCode name=\"NORMAL\">ita</PrimaryLanguageCode>");
    sb.AppendLine("    </CandidatePerson>");

    // ── CandidateProfile ─────────────────────────────────────────────
    sb.AppendLine("    <CandidateProfile languageCode=\"en\">");
    sb.AppendLine("        <hr:ID schemeID=\"Test-0001\" schemeName=\"CandidateProfileID\" schemeAgencyName=\"EUROPASS\" schemeVersionID=\"1.0\">5f327b3f7a5af5347b0c995f</hr:ID>");

    // ── EmploymentHistory ────────────────────────────────────────────
    if (data.TryGetValue("POSITIONS", out var positions) && positions.Count > 0)
    {
        sb.AppendLine("        <EmploymentHistory>");
        foreach (var pos in positions)
        {
            var title = Safe(pos, "Title");
            var company = Safe(pos, "Company Name");
            var location = Safe(pos, "Location");
            var description = Safe(pos, "Description");

            // Parse location for city
            var posCity = ParseCity(location);
            var posCountry = "it";

            var started = Safe(pos, "Started On");
            var finished = Safe(pos, "Finished On");
            var isCurrent = string.IsNullOrEmpty(finished);

            sb.AppendLine("            <EmployerHistory>");
            sb.AppendLine($"                <hr:OrganizationName>{Esc(company)}</hr:OrganizationName>");
            sb.AppendLine("                <OrganizationContact>");
            sb.AppendLine("                    <Communication>");
            sb.AppendLine("                        <Address>");
            sb.AppendLine($"                            <oa:CityName>{Esc(posCity)}</oa:CityName>");
            sb.AppendLine($"                            <CountryCode>{posCountry}</CountryCode>");
            sb.AppendLine("                        </Address>");
            sb.AppendLine("                    </Communication>");
            sb.AppendLine("                </OrganizationContact>");
            sb.AppendLine("                <PositionHistory>");
            sb.AppendLine($"                    <PositionTitle typeCode=\"FREETEXT\">{Esc(title)}</PositionTitle>");
            sb.AppendLine("                    <eures:EmploymentPeriod>");
            sb.AppendLine("                        <eures:StartDate>");
            sb.AppendLine($"                            <hr:FormattedDateTime>{FormatIsoDate(started)}</hr:FormattedDateTime>");
            sb.AppendLine("                        </eures:StartDate>");
            if (!isCurrent)
            {
                sb.AppendLine("                        <eures:EndDate>");
                sb.AppendLine($"                            <hr:FormattedDateTime>{FormatIsoDate(finished)}</hr:FormattedDateTime>");
                sb.AppendLine("                        </eures:EndDate>");
            }
            sb.AppendLine($"                        <hr:CurrentIndicator>{(isCurrent ? "true" : "false")}</hr:CurrentIndicator>");
            sb.AppendLine("                    </eures:EmploymentPeriod>");
            if (!string.IsNullOrEmpty(description))
            {
                // Wrap description in <p> tags like the original attachment.xml
                var htmlDesc = $"&lt;p&gt;{Esc(description)}&lt;/p&gt;";
                sb.AppendLine($"                    <oa:Description>{htmlDesc}</oa:Description>");
            }
            sb.AppendLine($"                    <City>{Esc(posCity)}</City>");
            sb.AppendLine($"                    <Country>{posCountry}</Country>");
            sb.AppendLine("                </PositionHistory>");
            sb.AppendLine("            </EmployerHistory>");
        }
        sb.AppendLine("        </EmploymentHistory>");
    }

    // ── EducationHistory ─────────────────────────────────────────────
    sb.AppendLine("        <EducationHistory>");

    // Formal education from LinkedIn
    if (data.TryGetValue("EDUCATION", out var education) && education.Count > 0)
    {
        foreach (var edu in education)
        {
            var school = Safe(edu, "School Name");
            var degree = Safe(edu, "Degree Name");
            var notes = Safe(edu, "Notes");
            var started = Safe(edu, "Start Date");
            if (string.IsNullOrEmpty(started)) started = Safe(edu, "Started On");
            var finished = Safe(edu, "End Date");
            if (string.IsNullOrEmpty(finished)) finished = Safe(edu, "Finished On");
            var isOngoing = string.IsNullOrEmpty(finished);

            sb.AppendLine("            <EducationOrganizationAttendance>");
            if (!string.IsNullOrEmpty(school))
                sb.AppendLine($"                <hr:OrganizationName>{Esc(school)}</hr:OrganizationName>");
            sb.AppendLine("                <OrganizationContact>");
            sb.AppendLine("                    <Communication>");
            sb.AppendLine("                        <Address/>");
            sb.AppendLine("                    </Communication>");
            sb.AppendLine("                </OrganizationContact>");
            sb.AppendLine("                <AttendancePeriod>");
            if (!string.IsNullOrEmpty(started))
            {
                sb.AppendLine("                    <StartDate>");
                sb.AppendLine($"                        <hr:FormattedDateTime>{FormatIsoDate(started)}</hr:FormattedDateTime>");
                sb.AppendLine("                    </StartDate>");
            }
            if (!isOngoing)
            {
                sb.AppendLine("                    <EndDate>");
                sb.AppendLine($"                        <hr:FormattedDateTime>{FormatIsoDate(finished)}</hr:FormattedDateTime>");
                sb.AppendLine("                    </EndDate>");
            }
            sb.AppendLine($"                    <Ongoing>{(isOngoing ? "true" : "false")}</Ongoing>");
            sb.AppendLine("                </AttendancePeriod>");
            sb.AppendLine("                <EducationDegree>");
            sb.AppendLine($"                    <hr:DegreeName>{Esc(degree)}</hr:DegreeName>");
            if (!string.IsNullOrEmpty(notes))
                sb.AppendLine($"                    <OccupationalSkillsCovered>{Esc(notes)}</OccupationalSkillsCovered>");
            sb.AppendLine("                </EducationDegree>");
            sb.AppendLine("            </EducationOrganizationAttendance>");
        }
    }

    // Certifications as additional education entries (matching attachment.xml pattern)
    if (data.TryGetValue("CERTIFICATIONS", out var certs) && certs.Count > 0)
    {
        foreach (var c in certs)
        {
            var name = Safe(c, "Name");
            var authority = Safe(c, "Authority");
            var url = Safe(c, "Url");
            var startedOn = Safe(c, "Started On");

            sb.AppendLine("            <EducationOrganizationAttendance>");
            if (!string.IsNullOrEmpty(authority))
                sb.AppendLine($"                <hr:OrganizationName>{Esc(authority)}</hr:OrganizationName>");
            sb.AppendLine("                <OrganizationContact>");
            sb.AppendLine("                    <Communication>");
            sb.AppendLine("                        <Address/>");
            sb.AppendLine("                    </Communication>");
            sb.AppendLine("                </OrganizationContact>");
            if (!string.IsNullOrEmpty(startedOn))
            {
                sb.AppendLine("                <AttendancePeriod>");
                sb.AppendLine("                    <StartDate>");
                sb.AppendLine($"                        <hr:FormattedDateTime>{FormatIsoDate(startedOn)}</hr:FormattedDateTime>");
                sb.AppendLine("                    </StartDate>");
                sb.AppendLine("                    <Ongoing>false</Ongoing>");
                sb.AppendLine("                </AttendancePeriod>");
            }
            sb.AppendLine("                <EducationDegree>");
            sb.AppendLine($"                    <hr:DegreeName>{Esc(name)}</hr:DegreeName>");
            if (!string.IsNullOrEmpty(url))
                sb.AppendLine($"                    <OccupationalSkillsCovered>&lt;p&gt;Certificate:&lt;/p&gt;&lt;p&gt;{Esc(url)}&lt;/p&gt;</OccupationalSkillsCovered>");
            sb.AppendLine("                </EducationDegree>");
            sb.AppendLine("            </EducationOrganizationAttendance>");
        }
    }
    sb.AppendLine("        </EducationHistory>");

    // ── Licenses ─────────────────────────────────────────────────────
    sb.AppendLine("        <eures:Licenses>");
    sb.AppendLine("            <eures:License>");
    sb.AppendLine("                <hr:LicenseTypeCode>B</hr:LicenseTypeCode>");
    sb.AppendLine("            </eures:License>");
    sb.AppendLine("        </eures:Licenses>");
    sb.AppendLine("        <Certifications/>");
    sb.AppendLine("        <PublicationHistory/>");

    // ── PersonQualifications (Language competencies with CEF levels) ─
    sb.AppendLine("        <PersonQualifications>");
    if (data.TryGetValue("LANGUAGES", out var languages) && languages.Count > 0)
    {
        foreach (var lang in languages)
        {
            var langName = Safe(lang, "Name");
            var proficiency = Safe(lang, "Proficiency").ToLowerInvariant();
            var normalizedName = NormalizeLanguageName(langName);
            var langCode = LanguageToIso3(normalizedName);
            var isNative = proficiency.Contains("native") || proficiency.Contains("bilingual");

            // Only emit CEF dimensions for non-native languages
            if (!isNative)
            {
                var cefrMap = ProficiencyToCefrMap(proficiency);
                sb.AppendLine("            <PersonCompetency>");
                sb.AppendLine($"                <CompetencyID schemeName=\"NORMAL\">{langCode}</CompetencyID>");
                sb.AppendLine("                <hr:TaxonomyID>language</hr:TaxonomyID>");
                foreach (var dim in cefrMap)
                {
                    sb.AppendLine("                <eures:CompetencyDimension>");
                    sb.AppendLine($"                    <hr:CompetencyDimensionTypeCode>{dim.Key}</hr:CompetencyDimensionTypeCode>");
                    sb.AppendLine("                    <eures:Score>");
                    sb.AppendLine($"                        <hr:ScoreText>{dim.Value}</hr:ScoreText>");
                    sb.AppendLine("                    </eures:Score>");
                    sb.AppendLine("                </eures:CompetencyDimension>");
                }
                sb.AppendLine("            </PersonCompetency>");
            }
        }
    }
    sb.AppendLine("        </PersonQualifications>");
    sb.AppendLine("        <EmploymentReferences/>");

    // ── Empty sections ───────────────────────────────────────────────
    sb.AppendLine("        <CreativeWorks/>");
    sb.AppendLine("        <Projects/>");
    sb.AppendLine("        <SocialAndPoliticalActivities/>");

    // ── Skills ───────────────────────────────────────────────────────
    sb.AppendLine("        <Skills>");
    if (data.TryGetValue("SKILLS", out var skills) && skills.Count > 0)
    {
        var skillNames = skills
            .Select(s => Safe(s, "Name").Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        foreach (var skillName in skillNames)
        {
            sb.AppendLine("            <PersonCompetency>");
            sb.AppendLine("                <hr:TaxonomyID>Digital_Skill</hr:TaxonomyID>");
            sb.AppendLine($"                <hr:CompetencyName>{Esc(skillName)}</hr:CompetencyName>");
            sb.AppendLine("            </PersonCompetency>");
        }
    }
    sb.AppendLine("        </Skills>");

    // ── Remaining empty sections ─────────────────────────────────────
    sb.AppendLine("        <NetworksAndMemberships/>");
    sb.AppendLine("        <ConferencesAndSeminars/>");
    sb.AppendLine("        <VoluntaryWorks/>");
    sb.AppendLine("        <CourseCertifications/>");

    // ── Others (summary / job-related skills) ────────────────────────
    if (!string.IsNullOrEmpty(summary))
    {
        sb.AppendLine("        <Others>");
        sb.AppendLine("            <Title>Job-related skills</Title>");
        sb.AppendLine("            <Other>");
        sb.AppendLine("                <Title>Job-related skills</Title>");
        var htmlSummary = $"&lt;p&gt;{Esc(summary)}&lt;/p&gt;";
        sb.AppendLine($"                <Description>{htmlSummary}</Description>");
        sb.AppendLine("            </Other>");
        sb.AppendLine("        </Others>");
    }

    sb.AppendLine("    </CandidateProfile>");

    // ── RenderingInformation ─────────────────────────────────────────
    sb.AppendLine("    <RenderingInformation>");
    sb.AppendLine("        <Design>");
    sb.AppendLine("            <Template>DynamicTemplate2</Template>");
    sb.AppendLine("            <Color>DarkGreen</Color>");
    sb.AppendLine("            <FontSize>Medium</FontSize>");
    sb.AppendLine("            <Logo>None</Logo>");
    sb.AppendLine("            <PageNumbers>true</PageNumbers>");
    sb.AppendLine("            <SectionsOrder>");
    sb.AppendLine("                <Section>");
    sb.AppendLine("                    <Title>work-experience</Title>");
    sb.AppendLine("                </Section>");
    sb.AppendLine("                <Section>");
    sb.AppendLine("                    <Title>education-training</Title>");
    sb.AppendLine("                </Section>");
    sb.AppendLine("                <Section>");
    sb.AppendLine("                    <Title>language</Title>");
    sb.AppendLine("                </Section>");
    sb.AppendLine("                <Section>");
    sb.AppendLine("                    <Title>profile-skills</Title>");
    sb.AppendLine("                </Section>");
    sb.AppendLine("                <Section>");
    sb.AppendLine("                    <Title>driving-licence</Title>");
    sb.AppendLine("                </Section>");
    sb.AppendLine("                <Section>");
    sb.AppendLine("                    <Title>Job-related skills</Title>");
    sb.AppendLine("                    <Custom>true</Custom>");
    sb.AppendLine("                </Section>");
    sb.AppendLine("            </SectionsOrder>");
    sb.AppendLine("        </Design>");
    sb.AppendLine("    </RenderingInformation>");
    sb.AppendLine("</Candidate>");

    return sb.ToString();
}

// ── Europass Helpers ─────────────────────────────────────────────────

/// <summary>Convert LinkedIn "MMM yyyy" or "yyyy" to ISO date "yyyy-MM-dd".</summary>
string FormatIsoDate(string dateStr)
{
    if (string.IsNullOrEmpty(dateStr)) return "";
    if (DateTime.TryParseExact(dateStr, "MMM yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
    {
        return dt.ToString("yyyy-MM-dd");
    }
    if (int.TryParse(dateStr, out var year))
        return $"{year}-01-01";
    return dateStr;
}

/// <summary>Extract the first city name from a LinkedIn location string.</summary>
string ParseCity(string location)
{
    if (string.IsNullOrEmpty(location)) return "";
    var parts = location.Split(',').Select(p => p.Trim()).ToArray();
    return parts.Length > 0 ? parts[0] : "";
}

/// <summary>Shorthand for EscapeXml.</summary>
string Esc(string text) => EscapeXml(text);

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

string LanguageToIso3(string language)
{
    return language.ToLowerInvariant() switch
    {
        "italian" => "ita",
        "english" => "eng",
        "french" => "fra",
        "spanish" => "spa",
        "german" => "deu",
        "portuguese" => "por",
        _ => language.Length >= 3 ? language[..3].ToLowerInvariant() : "xxx"
    };
}

/// <summary>Map LinkedIn proficiency to per-CEF-dimension levels (matching attachment.xml pattern).</summary>
Dictionary<string, string> ProficiencyToCefrMap(string proficiency)
{
    // Default: full professional proficiency → C1 reading/listening/writing, B2 speaking
    string listening, reading, spokenInteraction, spokenProduction, writing;

    if (proficiency.Contains("native") || proficiency.Contains("bilingual"))
    {
        listening = reading = spokenInteraction = spokenProduction = writing = "C2";
    }
    else if (proficiency.Contains("full professional"))
    {
        listening = "C1"; reading = "C1"; writing = "C1";
        spokenInteraction = "B2"; spokenProduction = "B2";
    }
    else if (proficiency.Contains("professional working"))
    {
        listening = reading = spokenInteraction = spokenProduction = writing = "B2";
    }
    else if (proficiency.Contains("limited working"))
    {
        listening = reading = spokenInteraction = spokenProduction = writing = "B1";
    }
    else if (proficiency.Contains("elementary"))
    {
        listening = reading = spokenInteraction = spokenProduction = writing = "A2";
    }
    else
    {
        listening = reading = spokenInteraction = spokenProduction = writing = "B2";
    }

    return new Dictionary<string, string>
    {
        ["CEF-Understanding-Listening"] = listening,
        ["CEF-Understanding-Reading"] = reading,
        ["CEF-Speaking-Interaction"] = spokenInteraction,
        ["CEF-Speaking-Production"] = spokenProduction,
        ["CEF-Writing-Production"] = writing
    };
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
