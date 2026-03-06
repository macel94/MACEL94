# Blog Article Proposals: Automatic CV Creation System

Based on the writing style of [Azure Container App Jobs: Why I think they're Great](https://www.neteye-blog.com/2025/03/azure-container-app-jobs-why-i-think-theyre-great/)

---

## Article 1: "GitHub Models API: Why I Think It's a Game-Changer for CV Automation"

### The Challenge
Like many developers, I've always struggled to keep my CV up-to-date. Every job application requires a tailored version highlighting different skills—Cloud SRE roles want observability and incident management, DevOps positions focus on CI/CD pipelines, and software development roles emphasize coding patterns and architectures. Creating these variations manually is tedious, time-consuming, and often outdated by the time you need them.

### The Cost of Traditional Approaches
When I first looked into automating CV customization, the options weren't great:
- **OpenAI API**: Would cost me roughly $0.002 per tailored CV with GPT-4. Doesn't sound like much, but for a personal project that regenerates CVs weekly and creates 3-4 variations, that's about $3-4 per month—plus I need to manage API keys and billing.
- **Local LLMs**: Running Llama or similar models locally means downloading 7-15GB models, dealing with hardware requirements (GPU ideally), and suffering through slower inference times on CPU-only machines.
- **Manual Templates**: The "free" option that costs you hours of time maintaining multiple CV versions, ensuring consistency, and keeping all variants synchronized.

### Why GitHub Models API?
When GitHub announced their Models API integration, I realized it was perfect for this use case:
- **Zero Cost**: For projects with the `models: read` permission, you get access to GPT-4.1 without any billing or external API keys
- **No External Dependencies**: Uses your workflow's `GITHUB_TOKEN`, so no secrets management or API key rotation
- **Built Into CI/CD**: Since my CV generation already runs on GitHub Actions, adding LLM-powered tailoring was literally just a `curl` call away
- **Controlled Environment**: Temperature and token limits are configurable, giving me deterministic rewrites

### Implementation Experience
I integrated the GitHub Models API into my automated CV pipeline that pulls data from LinkedIn's EU DMA Portability API. The workflow is beautifully simple:

1. **Fetch LinkedIn data** (17 different profile domains via OAuth)
2. **Generate base README** from the LinkedIn JSON using .NET 10 file-based C# scripts
3. **AI tailoring** for each role:
   ```bash
   curl -X POST "https://models.github.ai/inference/chat/completions" \
     -H "Authorization: Bearer $GITHUB_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
       "model": "openai/gpt-4.1",
       "messages": [{
         "role": "system",
         "content": "You are an expert CV writer. Rewrite this CV emphasizing Cloud SRE skills like observability, SLOs, incident management..."
       }, {
         "role": "user",
         "content": "'"$(cat README.md)"'"
       }],
       "temperature": 0.3,
       "max_tokens": 16000
     }'
   ```
4. **PDF generation** with Puppeteer, including embedded Europass XML metadata

The entire system runs weekly on a schedule, and I can manually trigger role-specific variants anytime. Each CV is tailored in ~10-15 seconds, and I get consistent, high-quality rewrites that preserve all dates, companies, and certifications while rephrasing descriptions for maximum impact.

### Why This Matters for Automated Workflows
The GitHub Models API isn't just useful for CV generation—it opens up possibilities for any GitHub Actions workflow that needs AI:
- **Automated PR descriptions**: Analyze diffs and generate comprehensive PR summaries
- **Code review assistance**: Highlight potential issues or suggest improvements
- **Documentation generation**: Convert code comments into user-friendly docs
- **Release notes**: Synthesize commit messages into coherent changelogs

For personal projects and small teams, the cost barrier to AI integration is completely eliminated. You're not choosing between "pay for external APIs" or "maintain local models"—you're just using infrastructure you already have.

### Technical Details Worth Noting
A few lessons learned:
- **Prompt engineering matters**: I use temperature 0.3 for consistency while avoiding complete rigidity
- **JSON escaping is tricky**: Use `jq -Rs '.'` to properly escape markdown for JSON payloads
- **Token limits are generous**: 16,000 max tokens handles even lengthy CVs without truncation
- **Model selection**: Only OpenAI models are currently available (no Anthropic/Claude), but GPT-4.1 works excellently

### Conclusion
GitHub Models API has made AI-powered customization accessible and practical for personal automation projects. It's especially valuable when you're already using GitHub Actions and just need LLM inference without the overhead of external services. For my CV automation system, it transformed a manual, tedious process into something that runs automatically every week, generating multiple tailored versions without any intervention from me.

If you're building automation workflows on GitHub and haven't explored the Models API yet, I highly recommend it. It's one of those features that removes friction at exactly the right point in the stack.

---

## Article 2: "From wkhtmltopdf to Puppeteer: Why I Rewrote My PDF Pipeline"

### The Challenge
My automated CV generation system had a problem: the PDFs looked terrible. I was using `wkhtmltopdf` to convert GitHub-flavored markdown (via pandoc) into PDFs, but three critical issues kept breaking the output:
1. **Remote images wouldn't load**: Shields.io badges and SkillIcons.dev graphics appeared as broken links
2. **CSS animations froze at frame zero**: GitHub-readme-stats SVGs use animations starting at `opacity: 0`, so stats were invisible
3. **Modern CSS was hit-or-miss**: Flexbox layouts, custom fonts, and GitHub's markdown styling often rendered incorrectly

Every time I looked at the generated PDF, I'd see missing badges, invisible stats, and broken layouts. For a CV that's supposed to showcase technical skills, this was embarrassing.

### The Cost of Traditional PDF Tools
I explored several alternatives:
- **wkhtmltopdf**: Free and lightweight, but hasn't seen major updates in years. It's based on an ancient QtWebKit engine that predates modern CSS standards.
- **WeasyPrint**: Better CSS support, but still struggles with remote resources and JavaScript-generated content.
- **Commercial tools** (Prince XML, DocRaptor): Would solve the problem, but cost $500-$4,000 per year for a personal project.
- **Keep patching wkhtmltopdf**: I tried downloading images locally, manually stripping animations, and simplifying CSS—each hack fixed one thing but broke another.

The fundamental issue wasn't fixable with workarounds: I needed a real browser engine.

### Why Puppeteer?
Puppeteer is Google's official Node.js library for controlling headless Chrome/Chromium. Unlike specialized PDF converters, it's literally using the same rendering engine that displays websites in production. This means:
- **Perfect rendering**: If it looks right in Chrome, it'll look right in the PDF
- **Remote resources work**: Network requests, CORS, SSL—everything just works
- **Modern standards**: ES6, CSS Grid, custom fonts, SVG animations—full support
- **Active maintenance**: Chromium is updated constantly, so you're never stuck on legacy tech

The only "cost" is ~300MB for the Chromium binary and ~50MB for Node.js dependencies, but storage is cheap and CI runners have plenty of space.

### Implementation Experience
Switching from wkhtmltopdf to Puppeteer took about an afternoon, but the quality improvement was dramatic. Here's what the implementation looks like:

**Step 1: Strip CSS animations** (since PDFs are static)
```bash
sed '/<style>/a\
  *, *::before, *::after {\
    animation-duration: 0s !important;\
    animation-delay: 0s !important;\
    transition-duration: 0s !important;\
  }\
  .stagger { opacity: 1 !important; }' \
  stats.svg > stats-static.svg
```

This injects override styles into GitHub-readme-stats SVGs, forcing animations to complete instantly. Now stats are visible at frame zero.

**Step 2: Convert Markdown to HTML with GitHub styling**
```bash
pandoc README.md \
  --from gfm \
  --to html \
  --standalone \
  --css github-markdown.css \
  -o output.html
```

**Step 3: Headless Chrome PDF generation**
```javascript
const puppeteer = require('puppeteer');
const browser = await puppeteer.launch({
  headless: true,
  args: ['--no-sandbox', '--disable-setuid-sandbox']
});

const page = await browser.newPage();
await page.goto(`file://${absoluteHtml}`, {
  waitUntil: 'networkidle0',  // Wait until 0 network connections
  timeout: 30000,
});

await new Promise(r => setTimeout(r, 2000));  // Extra render time

await page.pdf({
  path: 'output.pdf',
  format: 'A4',
  margin: { top: '15mm', right: '15mm', bottom: '15mm', left: '15mm' },
  printBackground: true,  // Essential for GitHub dark theme
});

await browser.close();
```

**Step 4: Embed Europass XML metadata**
```bash
pdfattach input.pdf europass_cv.xml output_final.pdf
```

This uses `poppler-utils` to attach structured CV data as a PDF attachment, making the CV compatible with Europass editors.

### What Improved Dramatically
The difference was night and day:
- **Remote badges**: Shields.io "Skills: Azure | C# | Kubernetes" badges now render perfectly
- **Skill icons**: SkillIcons.dev graphics load with proper colors and layouts
- **GitHub stats**: The animated SVGs now show complete data (contributions graph, language breakdown, etc.)
- **Font rendering**: Noto Color Emoji displays emojis properly instead of black-and-white fallbacks
- **CSS layout**: Complex flexbox/grid layouts work flawlessly

Most importantly, the PDFs now look *exactly* like the GitHub profile—because they're rendered by the same engine.

### Performance Characteristics
Some numbers from my GitHub Actions workflow:
- **wkhtmltopdf**: ~3-4 seconds (but broken output)
- **Puppeteer**: ~8-10 seconds (perfect output)
- **PDF size**: ~300-310 KB (similar to wkhtmltopdf)
- **CI resource usage**: No noticeable impact; GitHub runners have plenty of capacity

The extra 5-6 seconds per PDF is completely worth it for professional-quality output.

### Lessons Learned
A few gotchas I encountered:
1. **Network wait strategy matters**: Using `waitUntil: 'networkidle0'` ensures all remote images finish loading before PDF generation
2. **Extra sleep helps**: Even after network idle, adding a 2-second sleep prevents rare rendering glitches
3. **Animation stripping is essential**: PDFs capture a single frame, so animations starting at `opacity: 0` result in invisible content
4. **Path handling**: Always use absolute paths (`file://`) for local HTML files
5. **Sandbox mode**: GitHub Actions requires `--no-sandbox` flag (safe in containerized environments)

### Alternative Approaches
If Puppeteer doesn't fit your stack:
- **Playwright**: Microsoft's Chromium-based automation tool with similar PDF capabilities
- **Selenium with Chrome**: Heavier but equally capable for PDF generation
- **Bun/Deno with Chrome DevTools Protocol**: Lighter-weight than Puppeteer if you're already using alternative runtimes

The key insight is: **use a real browser engine**. Purpose-built PDF converters will always lag behind web standards.

### Conclusion
Switching from wkhtmltopdf to Puppeteer transformed my CV PDFs from "broken and embarrassing" to "indistinguishable from the live GitHub profile." It's a reminder that sometimes the right solution isn't a specialized tool—it's using the same rendering engine that powers billions of web pages.

If you're generating PDFs from HTML and hitting rendering issues, stop fighting with legacy converters. Puppeteer/Playwright give you a real Chromium browser with ~10 lines of code, and the quality difference is worth every millisecond of extra execution time.

For my CV automation system, it was the final piece that made the output truly professional.

---

## Article 3: ".NET 10 File-Based Apps: Why I Stopped Using .csproj Files"

### The Challenge
I needed automation scripts to fetch LinkedIn data, generate markdown, and create Europass XML. My requirements were simple:
- Parse JSON from LinkedIn's API (17 different profile domains)
- Transform data into markdown and XML formats
- Run on GitHub Actions (Linux runners)
- Execute quickly without build overhead

But every time I've created automation scripts in the past, I've faced the same dilemma: **Bash vs. Python vs. Node.js vs. compiled languages**.

### The Cost of Traditional Approaches
Each option has trade-offs that frustrated me:

**Bash scripts**:
- Great for simple file operations
- Terrible for JSON parsing (jq is powerful but awkward for complex transformations)
- No type safety, easy to introduce bugs
- Hard to test complex logic

**Python scripts**:
- Excellent for JSON and text processing
- Requires managing dependencies (pip, venv, requirements.txt)
- GitHub Actions needs setup-python step
- Version compatibility issues (Python 2 vs. 3, breaking changes between minor versions)

**Node.js scripts**:
- Good JSON handling
- Requires package.json, node_modules, npm install
- Async-everything model is overkill for linear data transformations
- Dependency hell with transitive packages

**Traditional .NET projects**:
- Type-safe, modern language (C#)
- But requires .csproj, NuGet packages, dotnet restore, dotnet build
- Multi-step compilation process
- More complex project structure for simple scripts

### Why .NET 10 File-Based Apps?
Then I discovered .NET 10's file-based app model (also called "script mode"), and it changed everything:

```csharp
#!/usr/bin/env dotnet run
// That's it. This is now an executable script.

using System;
using System.Net.Http;
using System.Text.Json;

Console.WriteLine("Hello from C# script!");

var http = new HttpClient();
var response = await http.GetStringAsync("https://api.example.com/data");
var data = JsonSerializer.Deserialize<MyType>(response);
```

Save that as `script.cs`, make it executable (`chmod +x script.cs`), and run it: `./script.cs`. No .csproj. No build step. No NuGet restore. It just runs.

### What Makes This Approach Special
The benefits are subtle but compound:

**1. Zero configuration overhead**
- No project file to maintain
- No package manager configuration
- No version conflicts
- No "restore packages" step

**2. Uses only Base Class Library (BCL)**
- System.Net.Http for REST APIs
- System.Text.Json for parsing (extremely fast, built-in)
- System.Xml.Linq for XML generation
- No third-party dependencies = no supply chain risk

**3. Cross-platform by design**
- Shebang (`#!/usr/bin/env dotnet run`) works on Linux/macOS
- GitHub Actions supports .NET 10 out-of-box
- Same code runs on Windows with `dotnet run script.cs`

**4. Type safety and modern language features**
- Null-aware operators (`?.`, `??`)
- Pattern matching
- LINQ for collections
- Top-level statements (no `class Program { static void Main() { } }` boilerplate)

**5. Instant execution**
- No explicit compile step (JIT compilation happens transparently)
- Startup time similar to Python/Node.js
- Can disable AOT with `#:property PublishAot=false` if needed

### Implementation Experience
I rewrote my entire CV automation pipeline using file-based C# apps:

**fetch_linkedin.cs** (198 lines):
- Fetches 17 LinkedIn domains via EU DMA API
- Handles pagination with `start` parameter
- Caches results to `linkedin_raw.json`
- Graceful degradation: uses cached data if API unavailable

**generate_readme.cs** (778 lines):
- Parses LinkedIn JSON
- Categorizes skills into buckets (Cloud, Backend, DevOps, Frontend, Tools)
- Generates GitHub-flavored markdown with badges, icons, collapsible sections
- Formats experience descriptions as bullet points

**generate_cv.cs** (562 lines):
- Transforms LinkedIn data into Europass XML
- Maps language proficiency to CEFR levels (A1-C2)
- Generates HR-XML 3.0 compliant output
- 5 dimensions of language competency (Listening, Reading, Speaking, Writing)

Each script is completely standalone. No shared dependencies. No complex build configuration. Just C# files that execute directly.

### Real-World Example: LinkedIn API Pagination
Here's actual code from `fetch_linkedin.cs` showing the simplicity:

```csharp
async Task<Dictionary<string, JsonElement>> FetchAllDomainsAsync(string token)
{
    var http = new HttpClient();
    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

    var domains = new[] {
        "PROFILE", "POSITIONS", "EDUCATION", "SKILLS",
        "CERTIFICATIONS", "LANGUAGES", "PATENTS", /* ... */
    };

    var results = new Dictionary<string, JsonElement>();

    foreach (var domain in domains)
    {
        var items = new List<JsonElement>();
        var url = $"https://api.linkedin.com/v2/dmaPortability/archives?domains={domain}";

        while (url != null)
        {
            var response = await http.GetStringAsync(url);
            var json = JsonDocument.Parse(response);

            items.AddRange(json.RootElement.GetProperty("elements").EnumerateArray());

            // Check for pagination
            url = json.RootElement.TryGetProperty("paging", out var paging)
                && paging.TryGetProperty("links", out var links)
                && links.EnumerateArray().FirstOrDefault(l =>
                    l.GetProperty("rel").GetString() == "next").TryGetProperty("href", out var next)
                ? next.GetString()
                : null;
        }

        results[domain] = JsonSerializer.SerializeToElement(items);
    }

    return results;
}
```

This code is:
- Type-safe (compiler catches errors)
- Readable (looks like modern C#, not enterprise boilerplate)
- Self-contained (no external packages)
- Cross-platform (runs anywhere .NET 10 runs)

### GitHub Actions Integration
In my workflow YAML, the setup is trivial:

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

No restore step. No build step. Just run. Each script executes in ~1-2 seconds.

### Trade-offs and Limitations
File-based apps aren't perfect for everything:

**When this approach works great**:
- Automation scripts (data transformation, API integration, file generation)
- CI/CD workflows
- One-off utilities
- Prototyping and experimentation

**When you should use traditional projects**:
- Need third-party NuGet packages (though you technically can use them with global.json)
- Large applications with multiple assemblies
- Performance-critical code requiring AOT compilation
- Applications with complex dependency graphs

For my CV automation, the BCL provides everything needed:
- `System.Net.Http` for REST API calls
- `System.Text.Json` for JSON parsing (faster than Newtonsoft.Json)
- `System.Xml.Linq` for XML generation
- `System.IO` for file operations

No external dependencies = no maintenance burden.

### Why This Matters for Automation
The .NET file-based app model hits a sweet spot:
- **Simpler than traditional compiled languages** (no build configuration)
- **More robust than scripting languages** (type safety, compile-time checks)
- **Faster than interpreted languages** (JIT compilation, BCL is highly optimized)
- **More secure than dependency-heavy ecosystems** (no supply chain vulnerabilities)

For GitHub Actions workflows, this translates to:
- Fewer steps in YAML
- Faster execution
- Better error messages
- Easier debugging (C# stack traces > shell script errors)

### Conclusion
.NET 10 file-based apps are what automation scripts should have been all along: simple enough to write as standalone files, powerful enough to handle complex logic, and type-safe enough to catch errors before runtime.

For my automated CV generation system, switching from a mix of Bash and Node.js to pure C# scripts eliminated entire categories of bugs (JSON parsing errors, type mismatches, silent failures). The code is cleaner, the execution is faster, and the GitHub Actions workflows are simpler.

If you're writing automation scripts and find yourself stuck between "Bash is too limited" and "Python/Node requires too much setup," give .NET file-based apps a try. You might be surprised how well they fit the use case.

---

## Article 4: "The EU DMA Data Portability API: Why LinkedIn Finally Gave Me My Own Data"

### The Challenge
I wanted to automate my CV generation by pulling data directly from LinkedIn—my single source of truth for professional experience. But for years, LinkedIn's API strategy was hostile to personal automation:
- The **LinkedIn API v2** required applying for API access (often rejected for personal use)
- The **Connections API** was shut down entirely
- **Web scraping** violated terms of service and broke constantly with UI changes
- **Manual export** ("Download your data" feature) gave a ZIP of HTML files—not machine-readable

Every time I updated my profile, I had to manually copy data into my CV. It was tedious, error-prone, and completely defeated the purpose of keeping LinkedIn as a single source of truth.

### Why Traditional LinkedIn API Access Didn't Work
LinkedIn's historical approach to API access was... frustrating:

**LinkedIn API v2 (deprecated)**:
- Required partner program approval
- Personal use cases almost always rejected
- Limited to 3rd-party integrations, not self-automation
- Shut down most endpoints over time

**OAuth-based developer apps**:
- Requires company verification
- Must explain commercial use case
- Rate-limited to 100 requests per day for basic tier
- No guarantee of approval

**Manual data export**:
- Settings → Data privacy → Download your data
- Get a ZIP file after ~10 minutes
- Contents: HTML files, not JSON
- Profile photos as separate files, no structured relationship
- Need to re-download every time anything changes

For a personal automation project, none of these options were viable. I wasn't building a SaaS product—I just wanted *my own data* in a machine-readable format.

### Enter the EU Digital Markets Act (DMA)
In 2024, the European Union's Digital Markets Act came into force, mandating that "gatekeepers" (large platforms like LinkedIn, owned by Microsoft) must provide **data portability** to users. This isn't just "download a ZIP"—it requires:
- **Machine-readable formats** (JSON, XML, CSV)
- **Programmatic access** (API, not just manual downloads)
- **Comprehensive data** (not cherry-picked fields)
- **Real-time** (or near-real-time) updates

LinkedIn complied by launching the **DMA Portability API** in late 2024. Suddenly, I had programmatic access to my own data—no partner approval, no commercial use case, no rate limit nightmares.

### Why the DMA Portability API is a Game-Changer
The API is exactly what personal automation needs:

**Comprehensive data coverage**:
The API exposes **17 different domains**:
- PROFILE (name, headline, location, photo)
- POSITIONS (job history)
- EDUCATION (degrees, certifications)
- SKILLS (endorsements)
- CERTIFICATIONS (licenses, courses)
- LANGUAGES (proficiency levels)
- PROJECTS (portfolio work)
- PUBLICATIONS (articles, papers)
- PATENTS (intellectual property)
- HONORS_AWARDS (achievements)
- VOLUNTEER_EXPERIENCE
- RECOMMENDATIONS (given/received)
- CONNECTIONS (network)
- GROUPS (community memberships)
- INTERESTS (followed topics)
- REGISTRATION_INFO (account metadata)
- EVENTS (attended/hosted)

**Clean JSON format**:
```json
{
  "elements": [
    {
      "firstName": "Francesco",
      "lastName": "Belacca",
      "headline": "DevOps Engineer | Consultant | SRE",
      "location": {
        "locality": "Milan",
        "country": "Italy"
      }
    }
  ],
  "paging": {
    "start": 0,
    "count": 10,
    "links": [
      { "rel": "next", "href": "..." }
    ]
  }
}
```

**Simple OAuth 2.0 authentication**:
- Scope: `r_dma_portability_self_serve`
- Token lifetime: ~60 days
- No partner verification required (if you're in EU/EEA)

**Pagination support**:
- Use `start` parameter for offset pagination
- Check `paging.links[rel=next]` for continuation
- No arbitrary rate limits (reasonable use assumed)

### Implementation Experience
Integrating the DMA API into my CV automation was straightforward:

**Step 1: Obtain access token**
This requires manual OAuth flow (one-time setup):
1. Create LinkedIn app at https://www.linkedin.com/developers/
2. Add redirect URL (can be localhost for personal use)
3. Request authorization code with scope `r_dma_portability_self_serve`
4. Exchange code for access token
5. Store token as GitHub Secret (`LINKEDIN_ACCESS_TOKEN`)

**Step 2: Fetch all domains**
```csharp
var domains = new[] {
    "PROFILE", "POSITIONS", "EDUCATION", "SKILLS",
    "CERTIFICATIONS", "LANGUAGES", "PROJECTS",
    "PUBLICATIONS", "PATENTS", "HONORS_AWARDS",
    "VOLUNTEER_EXPERIENCE", "RECOMMENDATIONS",
    "CONNECTIONS", "GROUPS", "INTERESTS",
    "REGISTRATION_INFO", "EVENTS"
};

foreach (var domain in domains)
{
    var url = $"https://api.linkedin.com/v2/dmaPortability/archives?domains={domain}";

    while (url != null)
    {
        var response = await httpClient.GetStringAsync(url);
        var json = JsonDocument.Parse(response);

        // Collect elements
        items.AddRange(json.RootElement.GetProperty("elements").EnumerateArray());

        // Check for next page
        url = GetNextPageUrl(json);
    }
}
```

**Step 3: Cache results**
```csharp
var cachedPath = "src/linkedin_raw.json";
await File.WriteAllTextAsync(cachedPath, JsonSerializer.Serialize(allData));
```

This cache enables:
- Local development without hitting the API
- Graceful degradation if token expires
- Faster iteration during README template changes

**Step 4: Transform to CV formats**
With structured JSON, I can now:
- Generate GitHub-flavored markdown README
- Create Europass XML (HR-XML 3.0 compliant)
- Tailor CVs for specific roles using LLMs
- Export to PDF with embedded metadata

### What This Unlocks
The DMA API makes previously impossible automation trivial:

**Automated portfolio websites**:
- Pull LinkedIn data → Generate static site → Deploy to GitHub Pages
- Always up-to-date with your profile

**Multi-format CV generation**:
- JSON → PDF, Europass XML, JSON Resume, LinkedIn profile
- One source of truth, infinite outputs

**Role-specific tailoring**:
- Feed LinkedIn data to LLM (GitHub Models API)
- Generate SRE-focused, DevOps-focused, SwDev-focused variants
- Automatically adjust emphasis based on target role

**Analytics and insights**:
- Track skill endorsements over time
- Visualize network growth
- Analyze connection patterns

For my use case, it's the foundation of a fully automated CV pipeline that runs weekly on GitHub Actions.

### Technical Gotchas
A few surprises I encountered:

**1. API version header**:
```http
X-Restli-Protocol-Version: 2.0.0
LinkedIn-Version: 202312
```
The `LinkedIn-Version` header is required (format: YYYYMM).

**2. Domain-specific schemas**:
Each domain has slightly different JSON structure. POSITIONS includes nested `companyName` and `title`, while SKILLS is a flat array of strings.

**3. Rate limiting is unclear**:
LinkedIn doesn't document exact rate limits for DMA API. In practice, fetching 17 domains sequentially (~30-60 seconds total) has never been throttled.

**4. Token expiration**:
Tokens last ~60 days. I get reminder emails from LinkedIn before expiration. Renewal requires re-doing OAuth flow (can't be automated due to security).

**5. EU/EEA restriction**:
The DMA only applies to European users. Non-EU LinkedIn accounts don't have access to the DMA Portability API.

### Why This Matters Beyond CVs
The EU DMA sets a precedent for **data sovereignty**:
- Users own their data
- Platforms must provide machine-readable access
- No gatekeeping for personal automation

This is bigger than LinkedIn. The DMA applies to:
- WhatsApp (Meta)
- Instagram (Meta)
- Facebook (Meta)
- Google Search
- Google Maps
- iOS App Store (Apple)
- Android Play Store (Google)

Imagine the automation possibilities:
- Export your WhatsApp history programmatically
- Build personal search analytics from Google Search data
- Create custom Instagram feeds with your own algorithms
- Archive all your Facebook posts/photos without manual download

The DMA is forcing tech giants to treat user data as **portable assets**, not **locked content**.

### Conclusion
The EU Digital Markets Act transformed LinkedIn from a walled garden into a data source I can actually automate against. The DMA Portability API gives me programmatic access to *my own data* without partner approvals, commercial justifications, or arbitrary rate limits.

For my automated CV generation system, it's the cornerstone: every week, the system pulls fresh LinkedIn data, generates multiple CV formats, and creates role-specific variants—all without any manual intervention.

If you're in the EU/EEA and frustrated by platforms locking up your data, explore the DMA Portability APIs. They're not just compliance checkboxes—they're genuinely useful for personal automation.

And if you're outside the EU? Well, this is a great example of why data portability regulations matter. Hopefully, other jurisdictions will follow suit.

---

## Article 5: "Building a Fully Automated CV Pipeline: From LinkedIn API to PDF in 2 Minutes"

### The Challenge
I wanted a CV system that:
- **Always stays current** (no manual updates)
- **Generates multiple formats** (GitHub profile, PDF, Europass XML)
- **Creates role-specific variants** (SRE, DevOps, Software Developer)
- **Runs automatically** (no manual intervention)
- **Costs nothing** (open-source tools, free CI/CD)

Traditionally, this would require a complex stack:
- Web scraping (fragile, violates ToS)
- Manual data entry (tedious, error-prone)
- Multiple CV templates to maintain (consistency nightmare)
- Expensive CI/CD minutes (commercial runners)
- Paid LLM APIs (GPT-4 isn't cheap)

But by combining modern technologies—LinkedIn's DMA API, .NET file-based apps, GitHub Actions, GitHub Models API, and Puppeteer—I built a system that runs weekly, generates 4 CV variants (1 generic + 3 role-specific), and costs $0 per month.

### Architecture Overview
The pipeline has 5 stages:

**Stage 1: Data Collection** (LinkedIn DMA API)
- Fetches 17 profile domains via OAuth
- Caches results to `linkedin_raw.json`
- Handles pagination, rate limits, token expiration

**Stage 2: Base CV Generation** (.NET 10 C# scripts)
- Parses LinkedIn JSON
- Generates GitHub-flavored markdown README
- Creates Europass XML (HR-XML 3.0 compliant)
- Categorizes skills, formats experience descriptions

**Stage 3: AI Tailoring** (GitHub Models API)
- Uses GPT-4.1 to rephrase CV for target roles
- Preserves dates, companies, certifications
- Adjusts emphasis (SRE → observability, DevOps → CI/CD, SwDev → architecture)

**Stage 4: PDF Rendering** (Puppeteer + pandoc)
- Converts markdown → HTML (GitHub styling)
- Headless Chrome renders to PDF
- Strips CSS animations for static output
- Embeds Europass XML as PDF attachment

**Stage 5: Distribution** (GitHub Actions + auto-commit)
- Commits changes to repo (README, PDFs, XML)
- Publishes GitHub profile automatically
- Stores artifacts in `artifacts/<role>/` directories
- Runs weekly on schedule (Mondays 06:00 UTC)

### The Cost of Traditional Approaches
Before building this system, I considered alternatives:

**Option 1: Manual CV maintenance**
- Cost: $0, but hours of time per update
- Update frequency: Whenever I remember (sporadic)
- Consistency: Always drift between variants
- Format support: Whatever I manually create

**Option 2: Commercial CV builders** (Zety, Resume.io, etc.)
- Cost: $5-20/month for premium features
- Lock-in: Export limitations, watermarks
- Customization: Limited templates, no API access
- Automation: None (manual edits required)

**Option 3: Custom solution with paid services**
- LinkedIn scraping service: $50-100/month
- GPT-4 API: $0.03 per request × 4 variants × 52 weeks = ~$6/year
- CI/CD (if using self-hosted): Server costs ~$5-10/month
- **Total: ~$70-130/month**

**Option 4: Fully automated open-source pipeline**
- LinkedIn DMA API: Free (EU mandated)
- GitHub Actions: Free (2,000 minutes/month for public repos)
- GitHub Models API: Free (for workflows)
- .NET 10: Free (open-source runtime)
- Puppeteer: Free (open-source)
- **Total: $0/month**

### Why This Stack Works So Well
The magic is in how the pieces fit together:

**1. LinkedIn DMA API** (solves data sourcing)
- Eliminates need for web scraping
- Structured JSON (no parsing HTML)
- Legal and ToS-compliant
- Real-time data (always current)

**2. .NET 10 file-based apps** (solves scripting complexity)
- Type-safe data transformations
- No build overhead (scripts run directly)
- Fast execution (JIT compiled)
- No external dependencies (uses BCL)

**3. GitHub Actions** (solves infrastructure)
- Free for public repos
- Runs on schedule (cron syntax)
- Pre-installed tools (.NET, Node.js, pandoc)
- Auto-commit bot for updates

**4. GitHub Models API** (solves AI customization)
- Free GPT-4.1 access (no external API keys)
- Uses workflow token (no secrets management)
- Generous token limits (16,000 max)
- Low latency (runs in GitHub infrastructure)

**5. Puppeteer** (solves PDF rendering)
- Perfect fidelity (real Chromium engine)
- Handles remote images (badges, icons)
- Modern CSS support (flexbox, grid, custom fonts)
- Reliable (if it renders in Chrome, it renders in PDF)

Each technology solves exactly one problem without overengineering.

### Implementation: The Full Workflow
Here's how all the pieces connect in `update-readme.yml`:

```yaml
name: Update README and CV
on:
  schedule:
    - cron: '0 6 * * 1'  # Monday 06:00 UTC
  workflow_dispatch:  # Manual trigger

permissions:
  contents: write  # For auto-commit
  models: read     # For GitHub Models API

jobs:
  update:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repo
        uses: actions/checkout@v4

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

      - name: Generate GitHub stats SVGs
        uses: soulteary/github-readme-stats-action@v1.0.0
        with:
          username: ${{ github.repository_owner }}

      - name: Generate PDF
        run: |
          sudo apt-get update
          sudo apt-get install -y pandoc poppler-utils fonts-noto-color-emoji
          npm install puppeteer@24.37.2
          bash src/generate_pdf.sh

      - name: Commit changes
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add README.md europass_cv.xml Francesco_Belacca_CV.pdf
          git commit -m "docs: update README, CV exports [skip ci]" || echo "No changes"
          git push
```

**Role-specific workflows** (3x similar):
```yaml
name: Generate Cloud SRE CV
on:
  workflow_dispatch:

jobs:
  generate:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Tailor README for SRE role
        run: bash src/tailor_readme.sh cloud-sre
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Generate stats
        uses: soulteary/github-readme-stats-action@v1.0.0

      - name: Generate PDF
        run: bash src/generate_role_pdf.sh cloud-sre

      - name: Commit
        run: |
          git add artifacts/cloud-sre/
          git commit -m "docs: update Cloud SRE CV [skip ci]"
          git push
```

### Real-World Execution Times
From actual GitHub Actions runs:

- **Data fetch**: 30-60 seconds (17 LinkedIn domains, paginated)
- **README generation**: <1 second (.NET JIT compilation + logic)
- **Europass XML generation**: <1 second
- **GitHub stats SVGs**: 5-10 seconds (fetches repo data)
- **PDF generation**: 8-10 seconds (Puppeteer + network waits)
- **AI tailoring** (per role): 10-15 seconds (GPT-4.1 inference)
- **Total (main workflow)**: ~1-2 minutes
- **Total (role-specific)**: ~30-45 seconds

For 4 CV variants per week:
- Main workflow: 1x per week = ~2 minutes
- Role workflows: 3x per month = ~2 minutes
- **Monthly CI usage**: ~10 minutes (0.5% of free tier)

### Why This Architecture is Resilient
Several design decisions make the system robust:

**1. Graceful degradation**:
```csharp
if (allData.Count == 0 && File.Exists("src/linkedin_raw.json"))
{
    Console.Error.WriteLine("⚠️ No live data. Using cache...");
    allData = JsonSerializer.Deserialize<...>(cachedJson);
}
```
If LinkedIn API fails, the system uses cached data instead of crashing.

**2. Idempotent commits**:
```bash
git commit -m "..." || echo "No changes"
```
If nothing changed, the workflow succeeds without empty commits.

**3. Skip flags**:
```bash
[[ "$SKIP_PDF" == "1" ]] && echo "Skipping PDF..." && exit 0
```
During development, skip slow steps for faster iteration.

**4. Dependency auto-installation**:
```bash
command -v pandoc || sudo apt-get install -y pandoc
```
Scripts install missing tools on first run.

**5. Path-agnostic execution**:
```csharp
var scriptDir = candidates.FirstOrDefault(dir =>
    File.Exists(Path.Combine(dir, "fetch_linkedin.cs"))
);
```
Works whether you run from repo root or `src/` directory.

### What Makes This Different from Traditional CV Systems
Most CV builders are static:
- You update manually
- They export once
- No role customization
- Locked to proprietary formats

This system is **dynamic**:
- Updates automatically from LinkedIn
- Exports to 4 formats (README, XML, PDF, role-PDFs)
- AI-tailors content for target roles
- Open formats (markdown, Europass XML)

Most automation scripts are fragile:
- Web scraping breaks with UI changes
- Hard-coded selectors
- No error handling
- Manual token renewal

This system is **robust**:
- Official API (no scraping)
- Structured JSON (no parsing DOM)
- Caching fallbacks
- Graceful error handling

### Lessons Learned
Building this system taught me several things:

**1. Combine technologies at the right level**
Don't try to do everything in one language. Use:
- C# for structured data transformation
- Bash for orchestration
- JavaScript for browser automation
- YAML for workflow configuration

**2. Optimize for iteration speed**
Caching LinkedIn data saved ~30 seconds per test run. Over 100+ iterations during development, that's **50 minutes saved**.

**3. Free tiers are powerful**
This entire system runs on free tiers:
- GitHub Actions (2,000 minutes/month)
- GitHub Models (no per-request billing)
- LinkedIn DMA API (EU mandated, free)

**4. Standards matter**
Using Europass XML means my CV works with EU job platforms. Using GitHub-flavored markdown means it renders correctly everywhere.

**5. Real browser engines > specialized tools**
Puppeteer's output quality is worth the extra 5 seconds vs. wkhtmltopdf.

### Future Enhancements
Ideas I haven't implemented yet:

- **Automatic job application**: Detect new job postings, match skills, auto-apply with tailored CV
- **Skill trend analysis**: Track endorsement growth, suggest in-demand skills to learn
- **Multi-language CVs**: Use GPT-4.1 to translate to Italian, German, French
- **JSON Resume format**: Export to standard resume.json schema
- **LaTeX output**: For academic CVs requiring precise typesetting
- **Webhook triggers**: Regenerate CV whenever LinkedIn profile changes

### Conclusion
Building a fully automated CV pipeline is easier than ever in 2025. By combining:
- LinkedIn's DMA API (free data source)
- .NET 10 file-based apps (type-safe scripting)
- GitHub Actions (free CI/CD)
- GitHub Models API (free AI inference)
- Puppeteer (professional PDF rendering)

...I created a system that generates 4 CV variants automatically, runs weekly, costs $0/month, and requires zero manual intervention after initial setup.

The entire codebase is ~2,000 lines of code (~70% C#, ~27% Bash, ~3% JavaScript) and takes 1-2 minutes to execute. It's open-source, fully customizable, and demonstrates how modern cloud-native tools can automate traditionally manual workflows.

If you're maintaining a CV manually in 2025, you're working too hard. Automate it.

---

## Summary

These five article proposals follow the NetEye blog style:
1. **Personal, first-person narrative** ("Why I think...", "Why I stopped...")
2. **Real-world problem framing** (challenge, cost of traditional approaches, why the solution is better)
3. **Technical depth with practical focus** (code snippets, architecture diagrams, real metrics)
4. **Implementation experience** (lessons learned, gotchas, trade-offs)
5. **Conversational yet professional** (accessible to developers, but thorough)

Each article takes a different angle on the same system:
- **Article 1**: GitHub Models API integration
- **Article 2**: PDF rendering technology choice
- **Article 3**: .NET scripting approach
- **Article 4**: LinkedIn DMA API access
- **Article 5**: End-to-end system architecture

All follow the pattern: **Challenge → Traditional Costs → Why New Solution → Implementation → Benefits → Conclusion**.
