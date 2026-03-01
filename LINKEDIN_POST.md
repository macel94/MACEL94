# LinkedIn Post

> Copy-paste the text below into a new LinkedIn post.
> Suggested media: attach a screenshot of the GitHub profile README side-by-side with the generated PDF, or a short screen-recording of the workflow running.

---

I open-sourced the tool I built to keep my CV permanently in sync with my LinkedIn profile — zero manual editing, ever.

Here is the problem I wanted to solve:
Every time I update my LinkedIn profile, my CV, my GitHub README, and my Europass XML all go out of date. Keeping them aligned is tedious, error-prone, and — for someone who automates infrastructure for a living — frankly embarrassing.

So I built an automation pipeline that treats my LinkedIn profile as the single source of truth and generates everything else from it, for free, using only GitHub Actions.

🔄 How the base workflow works (runs every Monday automatically):

1. A C# script calls the LinkedIn Member Data Portability API (EU DMA) and pulls my full profile: positions, skills, certifications, education, languages, volunteering — 17 data domains in total.
2. Another C# script transforms that raw JSON into a polished GitHub profile README.md with shields.io badges, tech-stack icons, and structured sections.
3. A third script generates a Europass-compliant XML CV from the same data.
4. GitHub Actions renders stats SVGs (contribution graph, top languages).
5. Pandoc + headless Chromium convert the README into a professional PDF and embed the Europass XML as a PDF attachment — one file, two standards.
6. Everything is auto-committed back to the repo.

Result: my GitHub profile, my downloadable PDF CV, and my Europass XML are always up to date. I edit LinkedIn once; three formats update themselves.

🤖 Bonus — AI-tailored CVs for specific roles:

I also added manually triggered workflows that use the GitHub Models API (GPT-4.1) to rephrase my experience for a specific role (Cloud SRE, Cloud DevOps, Cloud Software Developer) without inventing anything new — same facts, different emphasis. Each variant gets its own README and PDF under artifacts/.

No hallucinated experience. No copy-paste. Just the same career story, told through the right lens.

🇪🇺 Why this works (and why it is EU-only):

The data comes from LinkedIn's Member Data Portability API, which exists because the EU Digital Markets Act (DMA, Article 6(9)) requires "gatekeeper" platforms like LinkedIn to let European users export their data programmatically. This API is geo-restricted to EEA and Swiss accounts — there is no equivalent regulation elsewhere that compels LinkedIn to offer it.

Official guide: https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/

If you are in the EU/EEA, you can set this up in under 15 minutes:
→ Create a LinkedIn Developer app using the "Member Data Portability (Member) Default Company" page
→ Request the Member Data Portability API (Member) product
→ Generate a token with the r_dma_portability_self_serve scope
→ Add it as a GitHub secret and you are done

🍴 Fork it, use it, make it yours:

The repo is fully open source. Fork it, swap in your own LinkedIn token, and you will have:
✅ An auto-updating GitHub profile README
✅ A Europass XML CV (EU standard, machine-readable)
✅ A professional PDF with the Europass data embedded
✅ Optional AI-tailored CVs for any role you define

No SaaS. No subscriptions. Just GitHub Actions, .NET 10, and a bit of scripting.

🔗 Repo: https://github.com/macel94/macel94

If you believe your online presence should update itself, give it a ⭐ and let me know what you build with it.

#OpenSource #GitHub #GitHubActions #LinkedIn #Automation #DevOps #CV #Europass #DMA #DigitalMarketsAct #DeveloperTools #CareerDev #DotNet
