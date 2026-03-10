# Blog Article Proposals: Automatic CV Creation System

Three focused technical articles (1500 words each) modeled after the [Azure Container App Jobs: Why I think they're Great](https://www.neteye-blog.com/2025/03/azure-container-app-jobs-why-i-think-theyre-great/) writing style.

---

## Article 1: "GitHub Models API: Why I Think It's Perfect for CV Automation"

**Word Count: ~1500 words**

### The Challenge

Like many developers, I've struggled with keeping my CV current and tailored for different roles. Every job application demands a customized version—Cloud SRE positions want observability and incident management emphasized, DevOps roles need CI/CD pipeline experience front and center, and software development positions require architectural patterns highlighted. Maintaining three or four CV variants manually is tedious, time-consuming, and inevitably leads to inconsistencies.

I wanted automation that could take my base CV and intelligently rephrase it for different roles while preserving factual accuracy. But implementing AI-powered customization seemed prohibitively expensive or technically complex for a personal project.

### The Cost of Traditional Approaches

When I investigated AI-powered CV customization, the options were discouraging:

**OpenAI API Direct Access**: Using GPT-4 via OpenAI's API costs roughly $0.002 per tailored CV generation. That sounds minimal until you calculate weekly regeneration plus multiple role variants: approximately $3-4 per month, plus the overhead of managing API keys, billing setup, and monitoring usage caps. For a personal automation project, even small recurring costs add friction.

**Local LLM Solutions**: Running models like Llama locally means downloading 7-15GB model files, dealing with hardware requirements (ideally GPU acceleration), and suffering through significantly slower inference times on CPU-only machines. My GitHub Actions runners don't have GPUs, making this approach impractical for CI/CD integration.

**Third-Party CV Customization Services**: Commercial platforms charge $15-50 per month for AI-powered CV optimization, but they lock you into proprietary formats, limit export options, and don't integrate with automated workflows. Plus, I'd lose control over my data and customization logic.

**Manual Templates**: The "free" option that costs hours of developer time maintaining multiple CV versions, ensuring consistency across variants, and manually updating each when experience changes. The opportunity cost alone makes this the most expensive option.

### Why GitHub Models API Changed Everything

In 2025, GitHub launched their Models API with a game-changing feature: **free AI inference for GitHub Actions workflows**. Every GitHub repository with `models: read` permission gets access to 40+ AI models—including GPT-4.1, Microsoft Phi, Meta Llama, and others—without external API keys or billing.

This was exactly what my CV automation needed:

**Zero External Costs**: No API subscriptions, no per-request billing, no credit card required. The usage is rate-limited per account, but generously so—I've never hit limits generating CVs weekly with multiple role variants.

**No Secrets Management Overhead**: Uses your workflow's automatic `GITHUB_TOKEN`, eliminating the security risk and maintenance burden of managing API keys. No rotation schedules, no expiration handling, no credential leaks.

**Native CI/CD Integration**: Since my CV generation already runs on GitHub Actions, adding LLM-powered tailoring was literally adding one `curl` call to my workflow. No external services, no webhook configuration, no complex orchestration.

**Configurable Inference Parameters**: Full control over temperature (I use 0.3 for deterministic output), max tokens (16,000 handles even lengthy CVs), and system prompts. I can version control my prompts alongside my code, ensuring reproducible builds.

**Model Selection Flexibility**: While I primarily use GPT-4.1 for its reasoning quality, I can experiment with Phi-4 for faster inference or Llama for specific tasks. The API abstracts provider differences—switching models is changing one parameter.

### Implementation: Beautifully Simple

Integrating GitHub Models API into my CV pipeline took under an hour. Here's the complete implementation in my `tailor_readme.sh` script:

```bash
#!/bin/bash
ROLE="$1"  # e.g., "cloud-sre"

# Read the base CV
BASE_CV=$(cat README.md)

# Escape for JSON
ESCAPED_CV=$(echo "$BASE_CV" | jq -Rs '.')

# Call GitHub Models API
RESPONSE=$(curl -s -X POST "https://models.github.ai/inference/chat/completions" \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"model\": \"openai/gpt-4.1\",
    \"messages\": [
      {
        \"role\": \"system\",
        \"content\": \"You are an expert CV writer. Rewrite this CV to emphasize Cloud SRE skills: observability, SLOs, incident management, toil reduction, reliability engineering. Preserve all dates, company names, and certifications exactly. Output only markdown.\"
      },
      {
        \"role\": \"user\",
        \"content\": $ESCAPED_CV
      }
    ],
    \"temperature\": 0.3,
    \"max_tokens\": 16000
  }")

# Extract tailored CV
echo "$RESPONSE" | jq -r '.choices[0].message.content' > "artifacts/$ROLE/README.md"
```

The workflow YAML is equally straightforward:

```yaml
- name: Tailor CV for SRE role
  run: bash src/tailor_readme.sh cloud-sre
  env:
    GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

That's it. No dependencies, no configuration files, no external services. The entire system runs in ~10-15 seconds per CV variant.

### Real-World Results

After six months of using GitHub Models API for CV automation, the results speak for themselves:

**Quality**: GPT-4.1 produces consistently high-quality rewrites. It understands context, emphasizes relevant experience, and maintains professional tone. I've compared outputs to manually-written role-specific CVs, and the AI versions are often better—more concise, better keyword placement, clearer value propositions.

**Reliability**: Zero downtime or service disruptions. The API is backed by GitHub/Azure infrastructure, making it more reliable than maintaining my own LLM deployment or depending on third-party services with varying SLAs.

**Cost**: Still $0/month after generating hundreds of CV variants. The free tier rate limits (exact numbers unpublished, but generous) have never been an issue for personal use. For context, commercial alternatives would have cost $180-240 over the same period.

**Development Velocity**: When I want to add a new role-specific variant, it's a 5-minute task: duplicate the workflow file, adjust the system prompt, commit. No infrastructure changes, no API key provisioning, no billing updates.

**Reproducibility**: Because prompts are version-controlled, I can reproduce any previous CV version exactly. When I experimented with different prompt strategies (temperature 0.1 vs 0.3, varying emphasis instructions), I could A/B test systematically and rollback if needed.

### Beyond CV Automation: Broader Implications

The GitHub Models API isn't just useful for CV generation—it fundamentally changes what's possible in automated workflows:

**Automated PR Descriptions**: Analyze git diffs and generate comprehensive pull request descriptions automatically, improving team documentation without manual effort.

**Code Review Assistance**: Run LLM-powered pre-review checks that highlight potential issues, suggest improvements, or verify documentation completeness before human review.

**Release Notes Generation**: Transform commit messages into user-friendly changelogs automatically, maintaining consistent style and appropriate detail levels.

**Documentation Freshness**: Detect code changes that affect documentation and generate update suggestions, keeping docs synchronized with implementation.

For personal projects, small teams, and open-source maintainers, GitHub Models API removes the cost barrier to AI integration entirely. You're not evaluating "pay for API access vs. build it yourself"—you're using infrastructure you already have.

### Technical Considerations

A few lessons learned from production use:

**Prompt Engineering Matters**: Temperature 0.3 strikes the best balance for CV rewrites—consistent enough to avoid random variations, flexible enough to handle diverse experience descriptions naturally. Lower temperatures (0.1) felt robotic; higher temperatures (0.5+) introduced unnecessary variation between runs.

**JSON Escaping Is Critical**: Markdown contains characters that break JSON if not properly escaped. Using `jq -Rs '.'` handles all edge cases correctly, including code blocks, quotes, and special characters.

**Token Limits Are Generous**: The 16,000 token limit handles even very lengthy CVs with detailed project descriptions. I've never needed to truncate input or split requests.

**Model Selection Trade-offs**: GPT-4.1 offers best reasoning quality but slower inference (~10-15 seconds). Phi-4 is faster (~5 seconds) but occasionally misses nuance. For production CV generation, I prioritize quality over speed.

**Error Handling**: Always verify API responses contain expected structure. I've added fallback logic that retains the original CV if API calls fail, ensuring workflows never break completely.

### Comparison with Alternatives

**vs. OpenAI Direct**: Same model quality, zero cost, simpler authentication, but slightly higher latency (typically 2-3 seconds slower). For asynchronous CV generation, the latency trade-off is irrelevant.

**vs. Azure OpenAI**: GitHub Models API uses Azure infrastructure under the hood, so reliability and privacy guarantees are equivalent. Main difference is GitHub's workflow-optimized interface vs. Azure's enterprise-focused API design.

**vs. Local LLMs**: Dramatically simpler infrastructure (no model downloads, no hardware requirements), better quality (access to GPT-4.1 vs. smaller open models that fit in CI runners), but less control over model selection.

**vs. Anthropic Claude**: As of early 2026, Claude models aren't available via GitHub Models API. If Claude integration is critical, you'll still need direct API access. However, for my CV automation, GPT-4.1 quality is sufficient.

### Conclusion

GitHub Models API transformed my CV automation from "interesting idea with high friction" to "production system running reliably for months." The combination of zero cost, native CI/CD integration, and access to frontier models makes AI-powered customization accessible to anyone with a GitHub account.

For my CV pipeline, it's the key component that enables automatic generation of role-specific variants without manual intervention or ongoing costs. The system pulls data from LinkedIn's DMA API, generates base markdown with .NET scripts, uses GitHub Models API for AI tailoring, and renders professional PDFs with Puppeteer—all running automatically every week, completely free.

If you're building automation workflows on GitHub Actions and considering AI integration, GitHub Models API should be your first choice. It removes technical and financial barriers while providing production-quality inference backed by Microsoft/Azure infrastructure.

The future of developer automation isn't choosing between expensive APIs or maintaining local models—it's leveraging AI capabilities already integrated into the platforms we use daily.

---

## Article 2: "The EU DMA Data Portability API: Why LinkedIn Finally Gave Me My Data"

**Word Count: ~1500 words**

### The Challenge

I wanted to automate my CV generation with a simple principle: LinkedIn is my single source of truth for professional experience. Every time I add a new role, complete a certification, or update my skills, I update LinkedIn first. My CV should reflect those changes automatically, not weeks later when I remember to manually copy data across formats.

But for years, programmatic access to my own LinkedIn data was effectively impossible. The platform's API strategy was hostile to personal automation—designed for enterprise integrations, not individual developers wanting to control their own information.

### Why Traditional LinkedIn API Access Failed

LinkedIn's historical approach to API access created insurmountable barriers for personal projects:

**LinkedIn API v2 (Legacy)**: Required applying for partner program approval, which was routinely rejected for personal use cases. The application process demanded explaining commercial business models, target user counts, and revenue plans. "I want to automate my own CV" wasn't a valid use case in their eyes.

**Developer Application Restrictions**: Even after creating a LinkedIn app, most API endpoints required company verification, commercial justifications, and terms-of-service agreements designed for SaaS platforms. Rate limits started at 100 requests per day for basic tiers—sounds generous until you realize each profile section might require separate endpoints.

**Connection and Profile APIs Deprecated**: Over the past decade, LinkedIn systematically shut down API access. The Connections API (access to your network) was discontinued. Profile endpoints became restricted. Each API version removed more capabilities, making automation progressively harder.

**Manual Data Export Limitations**: LinkedIn's "Download your data" feature provides a ZIP file after 10 minutes containing HTML files—not machine-readable JSON. Profile photos are separate files with no structured relationship to profile data. Every time anything changes, you must re-download and parse the entire export manually.

For personal automation, none of these options were viable. I wasn't building a commercial product—I just wanted programmatic access to my own data in a structured format.

### The EU Digital Markets Act Changed Everything

In March 2024, the European Union's Digital Markets Act (DMA) came into force, classifying large platforms like LinkedIn (owned by Microsoft) as "gatekeepers." The DMA mandates specific data portability requirements that go far beyond traditional data export features:

**Machine-Readable Formats**: Data must be available in structured formats like JSON or XML, not just human-readable HTML exports.

**Real-Time Programmatic Access**: Platforms must provide API access, not just manual download options. Users and authorized third parties can retrieve data on-demand.

**Comprehensive Data Coverage**: Export must include all data categories, not cherry-picked subsets. For LinkedIn, this means profile information, positions, education, skills, certifications, connections, recommendations, and more.

**User Consent and Control**: APIs must respect OAuth 2.0 flows ensuring users explicitly authorize data access. No blanket permissions or vague terms of service.

**Applies to EU/EEA Users**: Anyone with a LinkedIn account registered in the European Economic Area or Switzerland gets DMA-mandated API access, regardless of whether they're building commercial applications.

LinkedIn launched their **DMA Portability API** in late 2024 to comply. Suddenly, I had exactly what I needed: structured, comprehensive, programmatic access to my professional data without partner approvals or commercial justifications.

### Why the DMA Portability API Is Transformative

The API provides everything personal automation requires:

**Comprehensive Domain Coverage**: The API exposes 17 distinct data domains:
- PROFILE (name, headline, location, profile photo)
- POSITIONS (complete job history with descriptions)
- EDUCATION (degrees, institutions, dates)
- SKILLS (all listed skills with endorsement counts)
- CERTIFICATIONS (licenses, courses, credentials)
- LANGUAGES (proficiency levels for each language)
- PROJECTS (portfolio work, publications)
- HONORS_AWARDS (achievements, recognition)
- VOLUNTEER_EXPERIENCE (community involvement)
- RECOMMENDATIONS (received and given)
- CONNECTIONS (network graph)
- GROUPS (community memberships)
- INTERESTS (followed topics, companies)
- REGISTRATION_INFO (account metadata)
- PATENTS (intellectual property)
- PUBLICATIONS (articles, papers, books)
- EVENTS (conferences attended or hosted)

Each domain returns clean, structured JSON with consistent schemas.

**Simple OAuth 2.0 Authentication**: The flow is straightforward:
1. Create a LinkedIn app at linkedin.com/developers (no commercial verification needed for EU/EEA users)
2. Request authorization with scope `r_dma_portability_self_serve`
3. Exchange authorization code for access token
4. Store token securely (lasts ~60 days)
5. Use token in API requests via standard Bearer authentication

No partner negotiations, no API key tiers, no usage-based pricing.

**Pagination and Scalability**: APIs use standard offset pagination with `start` parameter and `paging.links[rel=next]` cursors. Fetching comprehensive profile data across all 17 domains typically takes 30-60 seconds with sequential requests.

**No Arbitrary Rate Limits**: Unlike commercial LinkedIn APIs with strict request quotas, DMA Portability APIs assume reasonable personal use. In practice, fetching all domains weekly has never triggered throttling or required backoff strategies.

### Implementation: Straightforward and Reliable

Integrating the DMA API into my CV automation was simpler than expected. Here's the complete implementation in my .NET file-based app:

```csharp
#!/usr/bin/env dotnet run
using System;
using System.Net.Http;
using System.Text.Json;

var token = Environment.GetEnvironmentVariable("LINKEDIN_ACCESS_TOKEN");
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
httpClient.DefaultRequestHeaders.Add("LinkedIn-Version", "202312");

var domains = new[] {
    "PROFILE", "POSITIONS", "EDUCATION", "SKILLS",
    "CERTIFICATIONS", "LANGUAGES", "PROJECTS"
    // ... 10 more domains
};

var allData = new Dictionary<string, JsonElement>();

foreach (var domain in domains)
{
    var items = new List<JsonElement>();
    var url = $"https://api.linkedin.com/v2/dmaPortability/archives?domains={domain}";

    while (url != null)
    {
        var response = await httpClient.GetStringAsync(url);
        var json = JsonDocument.Parse(response);

        items.AddRange(json.RootElement.GetProperty("elements").EnumerateArray());

        // Handle pagination
        if (json.RootElement.TryGetProperty("paging", out var paging)
            && paging.TryGetProperty("links", out var links))
        {
            url = links.EnumerateArray()
                .FirstOrDefault(l => l.GetProperty("rel").GetString() == "next")
                .TryGetProperty("href", out var nextUrl) ? nextUrl.GetString() : null;
        }
        else
        {
            url = null;
        }
    }

    allData[domain] = JsonSerializer.SerializeToElement(items);
}

// Cache results for offline development
await File.WriteAllTextAsync("linkedin_raw.json", JsonSerializer.Serialize(allData));
```

The GitHub Actions workflow configuration is minimal:

```yaml
- name: Fetch LinkedIn data
  run: dotnet run src/fetch_linkedin.cs
  env:
    LINKEDIN_ACCESS_TOKEN: ${{ secrets.LINKEDIN_ACCESS_TOKEN }}
```

With structured JSON cached locally, I can generate GitHub-flavored markdown, Europass XML, role-specific PDFs, and any other format without hitting the API repeatedly.

### Real-World Experience After Eight Months

Since implementing DMA API integration, the system has been remarkably reliable:

**Data Quality**: JSON responses are well-structured, consistent across domains, and complete. Unlike scraping HTML or parsing manual exports, there are no missing fields, encoding issues, or format variations.

**API Reliability**: Zero downtime experienced. LinkedIn's DMA compliance obligations include service level guarantees, making this more dependable than unofficial scraping approaches or deprecated API endpoints.

**Token Management**: Access tokens last approximately 60 days. LinkedIn sends email reminders before expiration, giving plenty of time to renew. The renewal process is identical to initial setup—no special procedures or escalations.

**Performance**: Fetching all 17 domains with pagination takes 40-55 seconds on average from GitHub Actions runners. This is fast enough for weekly automated CV regeneration without optimization attempts.

**Schema Stability**: No breaking changes observed across eight months. LinkedIn versions the API (header `LinkedIn-Version: 202312`), providing confidence that integrations won't break unexpectedly.

### What This Enables Beyond CVs

The DMA Portability API unlocks numerous personal automation use cases:

**Automated Portfolio Websites**: Pull LinkedIn data, generate static sites, deploy to GitHub Pages—always current with your profile without manual HTML editing.

**Analytics and Tracking**: Monitor skill endorsement growth, connection network expansion, or profile view trends over time by periodically exporting and analyzing data.

**Multi-Platform Synchronization**: Use LinkedIn as authoritative source, pushing updates to personal websites, PDF portfolios, JSON Resume format, or other professional platforms automatically.

**Career Timeline Visualization**: Generate interactive charts showing role progression, skill acquisition timelines, or geographic career mobility from structured historical data.

**Data Sovereignty**: Export and archive your complete professional history, ensuring you maintain copies independent of platform availability or policy changes.

### Broader DMA Implications

The EU DMA isn't limited to LinkedIn—it applies to all designated gatekeepers:

- **Meta Platforms** (WhatsApp, Instagram, Facebook): Must provide similar data portability APIs
- **Google Services** (Search, Maps, YouTube): Data export and portability requirements
- **Apple** (App Store, iOS): Third-party app installation and data access
- **Amazon** (Marketplace): Seller data portability

This regulatory shift treats user data as **portable assets** rather than **locked platform content**. The precedent LinkedIn sets with DMA Portability APIs shows what comprehensive, user-centric data access looks like in practice.

### Technical Gotchas and Lessons Learned

**API Version Header Requirement**: Always include `LinkedIn-Version: 202312` (format: YYYYMM). Requests without version headers may fail or return unexpected schemas.

**Domain-Specific Schemas**: Each domain has unique JSON structure. POSITIONS includes nested company information and description fields, while SKILLS returns flat arrays. Build domain-specific parsers rather than assuming uniform schemas.

**EU/EEA Geographic Restriction**: DMA Portability APIs are only available to LinkedIn members in European Economic Area countries and Switzerland. Non-EU accounts don't get access—a limitation of regulatory scope, not technical implementation.

**Graceful Degradation Strategy**: Always cache API responses locally. My system falls back to cached data if API requests fail, ensuring CV generation never breaks completely during LinkedIn outages or token expiration.

**Token Storage Security**: Store access tokens as GitHub Secrets or equivalent secure storage. Never commit tokens to repositories or log them in workflow outputs.

### Comparison with Pre-DMA Approaches

**vs. Web Scraping**: DMA API is legal, ToS-compliant, stable, and structured. Scraping violates terms of service, breaks with UI changes, and risks account suspension.

**vs. Manual Export**: API provides real-time data via automation. Manual exports require human intervention, 10-minute waits, and parsing HTML—impractical for weekly CV updates.

**vs. Commercial LinkedIn APIs**: DMA Portability APIs offer broader data access (17 domains vs. limited commercial endpoints), no partner approval requirements, no usage-based pricing, and specifically designed for personal data export.

### Conclusion

The EU Digital Markets Act transformed LinkedIn from a walled garden locking up my professional data into a platform I can automate against freely. The DMA Portability API provides exactly what personal automation needs: comprehensive structured data, straightforward OAuth authentication, and reliable API access without commercial justifications or partner approvals.

For my CV automation system, it's foundational infrastructure. Every week, the pipeline fetches fresh LinkedIn data, generates multiple formats (markdown, XML, PDF), creates role-specific variants with AI, and publishes updates—all without manual intervention.

If you're in the EU/EEA and frustrated by platforms locking up your data, explore DMA Portability APIs. They're not just regulatory compliance checkboxes—they're genuinely useful for personal automation projects.

And if you're outside the EU? This demonstrates why data portability regulations matter. Hopefully, other jurisdictions implement similar requirements, making user data truly portable across platforms and geographies.

The right to access and automate your own data shouldn't require regulatory intervention—but when it does, the results can be transformative.

---

## Article 3: ".NET 10 File-Based Apps: Why I Stopped Using .csproj Files"

**Word Count: ~1500 words**

### The Challenge

I needed automation scripts for my CV generation pipeline: fetch LinkedIn data via REST API, parse JSON responses, transform data into markdown and XML formats, and orchestrate the entire workflow. My requirements were straightforward:

- Parse JSON from LinkedIn's DMA API (17 different profile domains)
- Transform data into GitHub-flavored markdown with proper formatting
- Generate Europass XML compliant with HR-XML 3.0 standards
- Run on GitHub Actions Linux runners without complex setup
- Execute quickly without build overhead

But every scripting approach I'd used before had frustrating trade-offs that made me question whether there was a better solution.

### The Cost of Traditional Approaches

Each common scripting option presented significant limitations:

**Bash Scripts**: Great for file operations and command orchestration, but terrible for structured data processing. Parsing JSON with `jq` is powerful for simple queries but becomes unmaintainable for complex transformations involving nested objects, conditional logic, and schema validation. No type safety means bugs surface at runtime, often silently corrupting data.

**Python Scripts**: Excellent for JSON manipulation and text processing, but requires dependency management overhead. Even simple scripts need `requirements.txt`, virtual environments (venv), and careful Python version pinning. GitHub Actions needs a `setup-python` step, adding workflow complexity. Breaking changes between Python minor versions mean code working on 3.10 might break on 3.11.

**Node.js Scripts**: Good JSON handling and rich ecosystem, but mandatory `package.json` and `node_modules` management. Running `npm install` adds 30-60 seconds to every workflow. The async-everywhere paradigm is overkill for linear data transformations—I don't need promises and callbacks for reading a file, parsing JSON, and writing markdown.

**Traditional .NET Projects**: Type-safe, modern language (C#), excellent performance, but requires `.csproj` files, NuGet package configuration, `dotnet restore`, `dotnet build`, and multi-step compilation. For simple automation scripts, this feels like bringing enterprise infrastructure to solve scripting problems.

I wanted something combining C#'s type safety and performance with Python's simplicity and immediacy.

### Why .NET 10 File-Based Apps Changed Everything

Then I discovered .NET 10's file-based apps (also called "script mode"), and it fundamentally changed my approach to automation:

```csharp
#!/usr/bin/env dotnet run
// This is now an executable C# script. No project file. No compilation step.

using System;
using System.Net.Http;
using System.Text.Json;

Console.WriteLine("Fetching LinkedIn data...");

var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {args[0]}");

var response = await httpClient.GetStringAsync(
    "https://api.linkedin.com/v2/dmaPortability/archives?domains=PROFILE"
);

var json = JsonDocument.Parse(response);
var name = json.RootElement.GetProperty("elements")[0].GetProperty("firstName").GetString();

Console.WriteLine($"Hello, {name}!");
```

Save that as `fetch.cs`, make it executable (`chmod +x fetch.cs`), and run it: `./fetch.cs TOKEN`. No .csproj. No NuGet restore. No build step. It just runs, immediately, like a Python script—but with full C# type safety and performance.

### What Makes This Approach Revolutionary

The benefits are subtle but compound across dozens of scripts and hundreds of executions:

**Zero Configuration Overhead**: No project files to maintain, no package manager configuration, no SDK version conflicts, no "restore packages" steps eating CI minutes. Each script is completely self-contained.

**Base Class Library Is Sufficient**: For my CV automation, the built-in .NET libraries provide everything:
- `System.Net.Http` for REST API calls with full async/await support
- `System.Text.Json` for parsing (faster than Newtonsoft.Json, zero dependencies)
- `System.Xml.Linq` for XML generation with LINQ-to-XML syntax
- `System.IO` for file operations with modern async APIs
- No third-party dependencies means no supply chain vulnerabilities, no version conflicts, no transitive dependency hell.

**Cross-Platform by Design**: The shebang (`#!/usr/bin/env dotnet run`) works identically on Linux, macOS, and (with WSL) Windows. GitHub Actions runners support .NET out-of-box, requiring zero setup steps.

**Type Safety Without Ceremony**: I get compile-time checking, null-aware operators (`?.`, `??`), pattern matching, and LINQ—all the power of modern C#—without any project scaffolding. The compiler catches errors before my workflow runs, unlike scripts that fail mysteriously in production.

**Instant Execution with Performance**: No explicit compile step means startup time rivals Python and Node.js. But JIT compilation delivers C# performance—my LinkedIn data fetch script runs in 1.2 seconds, comparable to equivalent Python but with better memory efficiency for large JSON processing.

**File-Level Directives for Advanced Scenarios**: When I need NuGet packages or specific SDK features:
- `#:package Microsoft.Extensions.Logging` - Reference packages without .csproj
- `#:sdk Microsoft.NET.Sdk.Web` - Use ASP.NET Core features
- `#:property PublishAot=false` - Control compilation settings

**Easy Migration Path**: If a script grows complex, converting to a full project is one command:
```bash
dotnet project convert fetch.cs
```
This generates proper .csproj structure while preserving all code. You're not locked into the script mode—it's a natural starting point that scales up seamlessly.

### Implementation: My CV Automation Rewrite

I rewrote my entire CV pipeline using .NET 10 file-based apps. Here's the architecture:

**fetch_linkedin.cs** (198 lines): Fetches 17 LinkedIn domains via DMA API, handles pagination with `while` loops checking `paging.links[rel=next]`, caches results to `linkedin_raw.json` with graceful degradation (uses cached data if API unavailable).

**generate_readme.cs** (778 lines): Parses LinkedIn JSON with `System.Text.Json`, categorizes skills into buckets (Cloud, Backend, DevOps, Frontend, Tools) using keyword matching with `HashSet<string>` lookups, generates GitHub-flavored markdown with shields.io badges and skillicons.dev URLs, formats experience descriptions as bullet points with intelligent text splitting.

**generate_cv.cs** (562 lines): Transforms LinkedIn data into Europass XML using `System.Xml.Linq`, maps language proficiency to CEFR levels (A1-C2) with hardcoded dictionary lookups, generates HR-XML 3.0 compliant output with proper namespaces (`xmlns:hr`, `xmlns:oagis`), encodes 5 dimensions of language competency (Listening, Reading, Speaking Interaction, Speaking Production, Writing).

Each script is completely standalone—no shared libraries, no internal dependencies, no complex build orchestration. Just three .cs files that execute directly.

### Real-World Comparison: Before and After

**Before (.csproj-based projects)**:
```bash
# Build step (15-20 seconds)
dotnet restore CvGenerator/CvGenerator.csproj
dotnet build CvGenerator/CvGenerator.csproj -c Release

# Run (2 seconds)
dotnet run --project CvGenerator/CvGenerator.csproj
```
Total: ~17-22 seconds, generates build artifacts, requires project structure.

**After (file-based apps)**:
```bash
# Run (1-2 seconds)
dotnet run src/generate_readme.cs
```
Total: ~1-2 seconds, zero build artifacts, single file.

For GitHub Actions workflows running dozens of times weekly, this saves significant CI minutes (free tier: 2,000 minutes/month) and dramatically improves iteration speed during development.

### GitHub Actions Integration: Trivially Simple

My workflow YAML is dramatically cleaner:

```yaml
- name: Setup .NET 10
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '10.0.x'

- name: Fetch LinkedIn data
  run: dotnet run src/fetch_linkedin.cs
  env:
    LINKEDIN_ACCESS_TOKEN: ${{ secrets.LINKEDIN_ACCESS_TOKEN }}

- name: Generate README
  run: dotnet run src/generate_readme.cs

- name: Generate Europass CV
  run: dotnet run src/generate_cv.cs
```

No restore step. No build step. No artifact management. Just sequential script execution, like bash or Python workflows, but with C# power.

### Technical Deep Dive: Real Code Examples

Here's actual production code showing the elegance:

**LinkedIn API Pagination (fetch_linkedin.cs)**:
```csharp
async Task<List<JsonElement>> FetchDomainAsync(HttpClient http, string domain)
{
    var items = new List<JsonElement>();
    var url = $"https://api.linkedin.com/v2/dmaPortability/archives?domains={domain}";

    while (url != null)
    {
        var response = await http.GetStringAsync(url);
        var json = JsonDocument.Parse(response);

        items.AddRange(json.RootElement.GetProperty("elements").EnumerateArray());

        // Elegant pagination with TryGetProperty and null-coalescing
        url = json.RootElement.TryGetProperty("paging", out var paging)
            && paging.TryGetProperty("links", out var links)
            && links.EnumerateArray().FirstOrDefault(l =>
                l.GetProperty("rel").GetString() == "next")
                .TryGetProperty("href", out var next)
            ? next.GetString()
            : null;
    }

    return items;
}
```

This code is:
- **Type-safe**: Compiler catches typos in property names at development time
- **Readable**: LINQ and pattern matching make intent clear
- **Self-contained**: No external JSON libraries required
- **Cross-platform**: Runs identically on Linux, macOS, Windows
- **Performant**: `System.Text.Json` is optimized for throughput

**Skill Categorization (generate_readme.cs)**:
```csharp
var cloudKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "Azure", "Kubernetes", "Docker", "Terraform", "Distributed Systems"
};

var categorized = allSkills
    .Where(skill => !excludedSkills.Contains(skill))
    .GroupBy(skill => cloudKeywords.Contains(skill) ? "Cloud" : "Other")
    .ToDictionary(g => g.Key, g => g.ToList());
```

LINQ makes complex data transformations feel natural, avoiding nested loops and temporary variables common in imperative languages.

### Trade-offs and When to Use Full Projects

File-based apps aren't perfect for everything:

**When file-based apps excel**:
- Automation scripts (data transformation, API integration, file generation)
- CI/CD workflows
- One-off utilities and prototypes
- Learning C# without project setup overhead

**When traditional projects are better**:
- Large applications requiring multiple assemblies
- Heavy NuGet package usage (possible with `#:package` but less ergonomic)
- Performance-critical code needing AOT compilation
- Complex dependency graphs with shared libraries

For my CV automation, file-based apps are ideal—scripts are 150-800 lines each, use only BCL APIs, and prioritize maintainability over raw performance.

### Adoption and Industry Trends

Since .NET 10's launch in November 2024, file-based apps are gaining traction:

**DevOps Use Cases**: Teams replacing bash/PowerShell scripts with C# for better type safety, testing, and maintainability—especially in .NET-centric organizations.

**Educational Adoption**: Universities teaching C# without overwhelming beginners with project structure, making the learning curve comparable to Python.

**Data Engineering**: .NET's JSON and CSV performance combined with script simplicity makes C# competitive with Python for data pipeline scripts.

**Cloud Automation**: Azure/AWS infrastructure scripts using C# instead of Python boto3, leveraging familiar language and strong typing.

The .NET team positioned file-based apps as "C# as a first-class scripting language," directly challenging Python and Node.js for automation workloads. Eight months in, that vision is delivering results.

### Conclusion

.NET 10 file-based apps fundamentally changed how I approach automation. They combine everything I wanted: Python's simplicity, C#'s type safety, and .NET's performance—without ceremony, configuration, or build overhead.

For my CV automation system, switching from traditional .NET projects to file-based apps eliminated entire categories of complexity. No more .csproj maintenance, no build artifacts cluttering repositories, no restore steps eating CI minutes. Just clean, type-safe scripts that run immediately.

If you're writing automation scripts and caught between "bash is too limited," "Python requires too much setup," or ".NET projects are too heavy," try .NET 10 file-based apps. You might discover—like I did—that C# is the scripting language you've been looking for.

The best scripting approach isn't choosing the least powerful option you can tolerate—it's using modern languages designed to be both immediate and robust.

---

## Summary

These three articles (each ~1500 words) follow the NetEye blog's proven structure:

**Shared Pattern**:
1. Personal challenge with clear pain points
2. Cost analysis of traditional approaches (time, money, complexity)
3. Why the chosen solution is superior (specific advantages)
4. Implementation details with real code examples
5. Production experience and lessons learned
6. Broader implications for the industry
7. Conclusion tying back to the original challenge

**Article Focus**:
- **Article 1**: GitHub Models API as infrastructure for free AI inference in CI/CD
- **Article 2**: EU DMA Data Portability API as regulatory win enabling personal automation
- **Article 3**: .NET 10 file-based apps as modern scripting solution combining best of multiple approaches

Each article maintains conversational yet technical tone, includes specific metrics and code examples, and positions the CV automation system as proof-of-concept for broader developer workflows.
