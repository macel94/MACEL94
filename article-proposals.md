## Article 2: "The EU DMA Data Portability API: Why LinkedIn Finally Gave Me My Data"

### The Challenge

LinkedIn is my single source of truth for professional experience. Every new role, certification, or skill update goes there first. My CV should reflect those changes automatically — not weeks later when I remember to manually copy information across formats.

The idea was simple: build a pipeline that pulls my LinkedIn data on a schedule, generates a polished README for my GitHub profile, a Europass CV in XML, and a PDF — all committed automatically. One source, many outputs, zero manual effort.

But for years, *programmatic access to my own LinkedIn data* was effectively impossible.

### The Walled Garden Problem

LinkedIn's API story has always been one of progressively locking things down. The platform's approach to data access was designed for enterprise integrations, not for individual developers wanting to control their own professional information.

The legacy LinkedIn API v2 required joining a partner program. The application process asked you to explain your commercial business model, projected user counts, and revenue plans. "I want to automate my own CV" was never going to pass that bar. Even if you managed to create a developer application, most useful endpoints demanded company verification and commercial justifications designed for SaaS platforms, not personal projects.

Over the past decade, LinkedIn systematically shut down the very APIs that would have made personal automation possible. The Connections API was discontinued. Profile endpoints became restricted. Each API version removed more capabilities. The direction was unambiguous: your data lives in LinkedIn, and it stays there.

The official "Download your data" feature wasn't much better — a ZIP file delivered after a 10-minute wait, containing CSV and HTML files with no machine-readable structure. Profile photos sit in separate files with no structured relationship to the rest. Every time anything changes, you re-download and re-parse. For a weekly automated pipeline, this was a non-starter.

I wasn't building a commercial product. I just wanted structured access to *my own data*. And for a long time, that simply wasn't possible.

### Then the EU Stepped In

The European Union's Digital Markets Act (DMA) was adopted in September 2022, entered into force on 1 November 2022, and became fully applicable in May 2023. By September 2023, the European Commission had designated six companies as "gatekeepers" — Alphabet, Amazon, Apple, ByteDance, Meta, and Microsoft. LinkedIn, as a Microsoft platform, fell squarely under the regulation's scope.

The compliance deadline was 6 March 2024: designated gatekeepers had to fully implement the DMA's provisions by that date.

What makes the DMA different from previous data protection regulations is its emphasis on *portability as a right*. GDPR already established the right to access your data, but in practice most platforms complied with bulk download features — ZIP files, CSV exports, the kind of thing LinkedIn already had. The DMA goes further. It mandates:

- **Machine-readable formats**: structured data (JSON, XML), not human-readable HTML dumps
- **Programmatic access**: real API endpoints, not manual download buttons with 10-minute waits
- **Comprehensive coverage**: all data categories, not cherry-picked subsets
- **Standard authentication**: OAuth 2.0 consent flows, respecting user authorization

For LinkedIn specifically, this meant building an entirely new API product — the **Member Data Portability API** — available to any EU/EEA or Swiss member without commercial justifications or partner approvals.

Suddenly, I had exactly what I needed.

### Two APIs, One Pipeline

LinkedIn's DMA compliance actually provides two distinct APIs, and understanding both matters for building useful automation.

The **Member Snapshot API** is the workhorse. It returns a point-in-time snapshot of your profile data across 17 queryable domains: profile information, work positions, education, skills, certifications, courses, languages, honors, publications, patents, projects, organizations, volunteering experiences, recommendations, endorsements, causes you care about, and an AI-generated profile summary. Each domain returns clean, paginated JSON. One request per domain, pagination for large datasets, and you've got your complete professional history in structured form.

The **Member Changelog API** complements this with real-time interaction data — posts you've created, comments, reactions — from the past 28 days. This is particularly interesting for keeping a GitHub profile README fresh with recent activity, though I primarily rely on the Snapshot API for CV-relevant data.

The authentication model is refreshingly simple compared to LinkedIn's commercial API tiers. You create a developer application on the LinkedIn Developers portal, using a specific default company page LinkedIn provides for exactly this purpose — no need to register your own company. You request access to the Member Data Portability API product, agree to the terms, and generate an access token through LinkedIn's OAuth Token Generator Tool with the `r_dma_portability_self_serve` scope. The token lasts approximately 60 days, and renewal follows the same straightforward process.

No partner negotiations. No usage-based pricing. No API key tiers. Just standard Bearer token authentication with a version header.

### Building the Pipeline

The implementation turned out to be almost anticlimactically simple. My fetch script iterates over the 17 snapshot domains, making one paginated API call per domain, and caches the combined JSON locally. Downstream scripts then parse that cached data to generate a GitHub-flavored markdown README, a Europass-compliant CV in XML, and a PDF rendered through headless Chromium.

The entire fetch phase — all 17 domains with pagination — completes in under a minute from a GitHub Actions runner. The cached JSON serves as the single intermediate representation: if the API is unavailable or the token has expired, the pipeline falls back to cached data rather than failing entirely. This graceful degradation means CV generation *never breaks* — at worst, it uses slightly stale data.

The GitHub Actions workflow runs weekly and requires exactly one secret: the LinkedIn access token. Everything else is self-contained. You can see the complete implementation — fetch script, README generator, CV generator, PDF pipeline — in my repository, which is open source specifically so others can replicate this setup.

### What I Learned Running This in Production

Several things surprised me after months of running this pipeline:

The data quality is excellent. JSON responses are well-structured, consistent across domains, and complete. Coming from years of scraping workarounds and manual exports, the contrast is stark — no missing fields, no encoding issues, no format variations between runs.

The API has been remarkably stable. LinkedIn versions the API through a request header (`Linkedin-Version: 202312`), and I haven't observed a single breaking change since I started using it. For something born out of regulatory compliance, the engineering quality is genuinely good.

Token management is the one manual step that remains. The 60-day expiration means roughly six renewals per year. LinkedIn sends email reminders before expiration, and the renewal process takes about two minutes. There's currently no way to obtain refresh tokens through the self-serve flow, which is the main friction point. I'm hopeful LinkedIn will add this eventually, but even without it, the overhead is minimal for a weekly pipeline.

Each domain has its own JSON schema, which caught me off guard initially. Positions include nested company objects with description fields. Skills return flat arrays. Languages have proficiency levels in a different structure than certifications have dates. You need domain-specific parsing logic rather than a one-size-fits-all approach. My repository has separate handling for each domain, and looking at that code is probably the fastest way to understand the data shapes.

### Beyond CVs: Why This Matters

The most interesting thing about the DMA Portability API isn't what it does for my specific use case — it's what it represents.

For the first time, I can treat my LinkedIn profile as a data source I *own* rather than content I've *contributed to someone else's platform*. I can generate a static portfolio website from the same data. I can build career timeline visualizations. I can synchronize my professional information across multiple platforms with LinkedIn as the authoritative source, rather than manually keeping three different profile pages in sync.

The real shift is philosophical. The DMA treats user data as a **portable asset** rather than **locked platform content**. And LinkedIn is just one of the designated gatekeepers. Meta, Google, Apple, Amazon, and ByteDance all face similar obligations. The precedent being set here — comprehensive, programmatic, user-controlled data access — could fundamentally change how we relate to the platforms that hold our information.

### The Comparison That Matters

The alternatives to the DMA API all have obvious problems now that a legitimate option exists. Web scraping is fragile, violates terms of service, and risks account suspension. Manual exports require human intervention and produce formats that are painful to parse programmatically. The commercial LinkedIn APIs, where they exist, require partner approvals and commercial justifications that personal projects will never satisfy.

The DMA Portability API sidesteps all of this. It's legal, it's stable, it's structured, and it's available to any EU/EEA member as a matter of regulatory right. You don't need to justify *why* you want your own data. You just access it.

### The Geographic Catch

There's one significant limitation worth discussing honestly: this is only available to LinkedIn members located in the European Economic Area and Switzerland. If your LinkedIn account isn't registered in an EU/EEA country, you don't get access. This isn't a technical limitation — it's a regulatory scope boundary. The DMA is EU law, and LinkedIn has no obligation to extend these APIs globally.

This is both the strength and the weakness of the regulatory approach. It works *extremely well* for those it covers, but it creates a two-tier system where EU residents have programmatic access to their own professional data and everyone else doesn't.

If you're outside the EU, this article might feel like reading about a feature you can't use. And honestly, that's fair. But it also demonstrates *what's possible* when data portability is treated as a right rather than a feature request. Hopefully other jurisdictions take note.

### Conclusion

The EU Digital Markets Act turned LinkedIn from a platform that locked up my professional data into one I can automate against freely. My CV pipeline fetches fresh data weekly, generates multiple output formats, and commits everything to GitHub — all without manual intervention.

The technical implementation was the easy part. The hard part was having an API to call at all, and that took an act of the European Parliament.

If you're an EU/EEA developer frustrated by platforms hoarding your data, the DMA Portability APIs aren't just a regulatory checkbox — they're genuinely useful infrastructure for personal automation. My entire system is open source, and I'd encourage anyone interested to explore the code and build their own pipeline.

The right to access and automate your own data shouldn't require regulatory intervention. But when it does, the results can be transformative.
