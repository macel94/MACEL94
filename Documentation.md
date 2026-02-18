# Documentation

## Overview

This repository is a **self-updating GitHub profile README**. It pulls data from LinkedIn via the EU DMA Data Portability API, generates a polished `README.md`, a Europass CV XML, and a PDF version — all orchestrated by a GitHub Actions workflow.

```
LinkedIn Profile → DMA Portability API → C# Scripts (.NET 10) → README.md + CV → GitHub Actions (auto-commit)
```

## Repository Structure

```
.
├── README.md                                 # Auto-generated GitHub profile (do not edit manually)
├── Documentation.md                          # This file
├── LINKEDIN_POST.md                          # LinkedIn announcement post (copy-paste ready)
├── .github/workflows/
│   ├── update-readme.yml                     # Scheduled + manual: full LinkedIn → README/CV/PDF pipeline
│   ├── generate-cv-cloud-sre.yml            # Manual: AI-tailored CV for Cloud SRE
│   ├── generate-cv-cloud-devops.yml         # Manual: AI-tailored CV for Cloud DevOps
│   └── generate-cv-cloud-swdev.yml          # Manual: AI-tailored CV for Cloud Software Developer
├── src/                                      # Source code
│   ├── fetch_linkedin.cs                     # Fetches LinkedIn data via DMA API
│   ├── generate_readme.cs                    # Generates README.md from LinkedIn JSON
│   ├── generate_cv.cs                        # Generates Europass CV XML
│   ├── generate_pdf.sh                       # Converts README → PDF (pandoc + Puppeteer)
│   ├── generate_role_pdf.sh                  # Converts role-tailored README → PDF
│   ├── tailor_readme.sh                      # Rewrites README for a target role via GitHub Models API
│   ├── html_to_pdf.js                        # Puppeteer script for HTML → PDF rendering
│   ├── run.sh                                # Orchestrator script (fetch → generate → PDF)
│   ├── attachment.xml                        # Reference Europass XML schema
│   ├── package.json                          # Node.js dependencies (puppeteer)
│   ├── package-lock.json                     # Lockfile
│   └── .gitignore                            # Ignores linkedin_raw.json (cached API data)
└── artifacts/                                # Generated output (committed by CI)
    ├── profile/
    │   ├── stats.svg                         # GitHub stats card
    │   └── top-langs.svg                     # Top languages card
    ├── europass_cv.xml                       # Europass CV XML
    ├── Francesco_Belacca_CV.pdf              # PDF version of the README/CV
    ├── github-markdown.css                   # Cached GitHub markdown stylesheet
    ├── cloud-sre/                            # AI-tailored SRE variant
    │   ├── README.md
    │   └── Francesco_Belacca_CV.pdf
    ├── cloud-devops/                         # AI-tailored DevOps variant
    │   ├── README.md
    │   └── Francesco_Belacca_CV.pdf
    └── cloud-swdev/                          # AI-tailored Software Developer variant
        ├── README.md
        └── Francesco_Belacca_CV.pdf
```

## How It Works

### Data Source: LinkedIn DMA Portability API

As an EU/EEA citizen, the [Digital Markets Act](https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/) grants the right to programmatically access LinkedIn data. This project uses two LinkedIn APIs:

| API | Endpoint | Purpose |
|---|---|---|
| **Member Snapshot API** | `GET /rest/memberSnapshotData?q=criteria&domain=<DOMAIN>` | Point-in-time snapshot of profile data across 50+ domains |
| **Member Changelog API** | `GET /rest/memberChangeLogs?q=memberAndApplication` | Real-time change events (posts, comments, reactions) from the last 28 days |

### Snapshot Domains Used

| Domain | Description |
|---|---|
| `PROFILE` | Name, headline, summary, location, industry |
| `POSITIONS` | Work experience (title, company, dates, description) |
| `EDUCATION` | Schools, degrees, fields of study |
| `SKILLS` | All skills on your profile |
| `CERTIFICATIONS` | Certifications with authority and dates |
| `COURSES` | Courses listed on your profile |
| `LANGUAGES` | Languages and proficiency levels |
| `HONORS` | Awards and honors |
| `PUBLICATIONS` | Published works |
| `PATENTS` | Patents |
| `PROJECTS` | Projects with descriptions and URLs |
| `ORGANIZATIONS` | Organization memberships |
| `VOLUNTEERING_EXPERIENCES` | Volunteer roles |
| `RECOMMENDATIONS` | Recommendations received |
| `ENDORSEMENTS` | Skill endorsements |
| `CAUSES_YOU_CARE_ABOUT` | Causes you support |
| `PROFILE_SUMMARY` | AI-generated profile summary |

### Pipeline Steps

1. **`src/fetch_linkedin.cs`** — Fetches all relevant domains from the Member Snapshot API and saves raw JSON to `src/linkedin_raw.json` (gitignored).
2. **`src/generate_readme.cs`** — Parses the cached JSON and generates a formatted `README.md` at the repo root.
3. **`src/generate_cv.cs`** — Generates a Europass-compliant CV XML from the same data, written to `artifacts/europass_cv.xml`.
4. **GitHub Stats SVGs** — Generated by a GitHub Action in CI, saved to `artifacts/profile/`.
5. **`src/generate_pdf.sh`** — Converts the README to PDF using pandoc (markdown → HTML) and Puppeteer/headless Chromium (HTML → PDF), then embeds the Europass XML as a PDF attachment. Output: `artifacts/Francesco_Belacca_CV.pdf`.

All scripts use .NET 10 [file-based apps](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/) — no `.csproj`, no `dotnet restore`, no compilation step needed.

### Workflow 1 — Base Recurring Pipeline (`update-readme.yml`)

This is the core automation. It runs **every Monday at 06:00 UTC** (and on manual dispatch) and performs the full end-to-end pipeline:

```
┌──────────────────────────────────────────────────────────────────┐
│  Trigger: schedule (weekly) or workflow_dispatch (manual)        │
├──────────────────────────────────────────────────────────────────┤
│  1. Checkout repository                                          │
│  2. Setup .NET 10                                                │
│  3. Run src/run.sh (SKIP_PDF=1):                                 │
│     a. fetch_linkedin.cs  → pull 17 domains from LinkedIn API    │
│     b. generate_readme.cs → transform JSON into README.md        │
│     c. generate_cv.cs     → transform JSON into Europass XML     │
│  4. Generate GitHub stats SVGs (stats.svg, top-langs.svg)        │
│  5. Run src/generate_pdf.sh:                                     │
│     a. pandoc: README.md → HTML                                  │
│     b. Puppeteer/Chromium: HTML → PDF (renders badges & images)  │
│     c. pdfattach: embed europass_cv.xml inside the PDF           │
│  6. Auto-commit and push all changes                             │
└──────────────────────────────────────────────────────────────────┘
```

**Outputs committed to the repo:**
- `README.md` — the GitHub profile page
- `artifacts/europass_cv.xml` — machine-readable Europass CV
- `artifacts/Francesco_Belacca_CV.pdf` — PDF CV with embedded Europass XML
- `artifacts/profile/stats.svg` and `top-langs.svg` — GitHub stats cards

**Secrets required:** `LINKEDIN_ACCESS_TOKEN` (see [Setup](#setup-one-time))

### Workflow 2 — AI-Tailored CVs (manually triggered)

Three additional workflows let you generate **role-specific CV variants** on demand using the [GitHub Models API](https://docs.github.com/en/github-models) (GPT-4.1):

| Workflow file | Target role | Output directory |
|---|---|---|
| `generate-cv-cloud-sre.yml` | Cloud Site Reliability Engineer | `artifacts/cloud-sre/` |
| `generate-cv-cloud-devops.yml` | Cloud DevOps Engineer | `artifacts/cloud-devops/` |
| `generate-cv-cloud-swdev.yml` | Cloud Software Developer | `artifacts/cloud-swdev/` |

**How they work:**

```
┌──────────────────────────────────────────────────────────────────┐
│  Trigger: workflow_dispatch only (manual)                        │
│  Permissions: contents: write, models: read                      │
├──────────────────────────────────────────────────────────────────┤
│  1. Checkout repository                                          │
│  2. Run src/tailor_readme.sh <role>:                             │
│     a. Read the current README.md                                │
│     b. Send it to GitHub Models API (openai/gpt-4.1) with a     │
│        system prompt that rewrites the headline, About Me, and   │
│        experience bullets to emphasize the target role            │
│     c. Rules enforced by the prompt:                             │
│        • No invented experience or skills                        │
│        • Same structure, dates, companies, and formatting        │
│        • Only rephrase & re-emphasize existing content           │
│     d. Fix relative paths (SVGs, PDF links) for subdirectory     │
│     e. Write result to artifacts/<role>/README.md                │
│  3. Generate GitHub stats SVGs (same as base workflow)           │
│  4. Run src/generate_role_pdf.sh <role>:                         │
│     a. Convert artifacts/<role>/README.md → HTML → PDF           │
│     b. Embed Europass XML as PDF attachment                      │
│  5. Auto-commit artifacts/<role>/ and push                       │
└──────────────────────────────────────────────────────────────────┘
```

**Outputs per role:**
- `artifacts/<role>/README.md` — role-tailored README
- `artifacts/<role>/Francesco_Belacca_CV.pdf` — role-tailored PDF

**Secrets required:** None beyond `GITHUB_TOKEN` (automatic). The GitHub Models API uses the built-in `GITHUB_TOKEN` with the `models: read` permission.

**Adding a new role:** Duplicate any `generate-cv-cloud-*.yml` workflow and add a new `case` block in `src/tailor_readme.sh` with the role name, title, and focus keywords.

### Other Available LinkedIn Domains (not used, but queryable)

`ADS_CLICKED`, `MEMBER_FOLLOWING`, `login`, `RICH_MEDIA`, `SEARCHES`, `INFERENCE_TAKEOUT`, `ALL_COMMENTS`, `CONTACTS`, `Events`, `RECEIPTS`, `AD_TARGETING`, `REGISTRATION`, `REVIEWS`, `ARTICLES`, `GROUPS`, `COMPANY_FOLLOWS`, `INVITATIONS`, `PHONE_NUMBERS`, `CONNECTIONS`, `EMAIL_ADDRESSES`, `JOB_POSTINGS`, `JOB_APPLICATIONS`, `JOB_SEEKER_PREFERENCES`, `LEARNING`, `INBOX`, `SAVED_JOBS`, `SAVED_JOB_ALERTS`, `TEST_SCORES`, `MEMBER_SHARE_INFO`, `SECURITY_CHALLENGE_PIPE`, `TRUSTED_GRAPH`, `MARKETPLACE_ENGAGEMENTS`, `MARKETPLACE_PROVIDERS`, `MARKETPLACE_OPPORTUNITIES`, `ACTOR_SAVE_ITEM`, `JOB_APPLICANT_SAVED_ANSWERS`, `TALENT_QUESTION_SAVED_RESPONSE`, `ALL_LIKES`, `ALL_VOTES`, `RECEIPTS_LBP`, `easyapply-blocking`, `LEARNING_COACH_AI_TAKEOUT`, `LEARNING_COACH_INBOX`, `LEARNING_ROLEPLAY_INBOX`, `ACCOUNT_HISTORY`, `INSTANT_REPOSTS`, `IDENTITY_CREDENTIALS_AND_ASSETS`, `ADS_LAN`

## Setup (One-Time)

### 1. Create a LinkedIn Developer Application

1. Go to [LinkedIn Developers](https://developer.linkedin.com/)
2. Create a new app
3. **Important**: Use the default company page **"Member Data Portability (Member) Default Company"** — do NOT create a new company page
4. Go to the **Products** tab and request access to **"Member Data Portability API (Member)"**
5. Agree to the Terms & Conditions

### 2. Generate an Access Token

1. In the developer portal, go to **Docs and tools → OAuth Token Tools**
2. Click **Create token**
3. Select your app
4. Select scope **`r_dma_portability_self_serve`**
5. Click **Request access token**
6. Log in and click **Allow**
7. Copy the generated token

> ⚠️ The token expires (typically 60 days). You'll need to regenerate it when it does. There is currently no way to get a refresh token via this self-serve flow.

### 3. Add the Token to GitHub Secrets

1. Go to your repo → **Settings → Secrets and variables → Actions**
2. Click **New repository secret**
3. Name: `LINKEDIN_ACCESS_TOKEN`
4. Value: paste the token from step 2

### 4. Run the Workflow

- Go to **Actions → Update README from LinkedIn → Run workflow**
- Or wait for the weekly schedule (every Monday at 06:00 UTC)

## Local Development

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later.

```bash
export LINKEDIN_ACCESS_TOKEN="your_token_here"
./src/run.sh
```

Or run individual steps:

```bash
# Fetch LinkedIn data
dotnet run src/fetch_linkedin.cs

# Generate README (requires src/linkedin_raw.json from the fetch step)
dotnet run src/generate_readme.cs

# Generate Europass CV XML
dotnet run src/generate_cv.cs

# Generate PDF (requires pandoc, Node.js, and the artifacts/profile SVGs)
./src/generate_pdf.sh
```

### API Authentication

All LinkedIn API requests use:
```
Authorization: Bearer <access_token>
Linkedin-Version: 202312
Content-Type: application/json
```

### Rate Limits

LinkedIn enforces rate limits. The script makes one request per domain — with 17 domains, that's ~17+ requests per run (more with pagination). The weekly schedule is very conservative.

## Why This API Is EU/EEA-Only

The LinkedIn Member Data Portability API exists solely because of the **EU Digital Markets Act (DMA)**, specifically **Article 6(9)**, which requires designated "gatekeeper" platforms to provide **effective, real-time data portability** to end-users and authorized third parties via APIs.

### Key facts

- **Who it applies to:** LinkedIn (as a Microsoft service) is classified as a gatekeeper under the DMA. The obligation applies only within the **European Economic Area (EEA)** and **Switzerland**.
- **What it mandates:** Gatekeepers must allow users to port their data continuously and in real time — not just via static data downloads. This is a step beyond the GDPR's Article 20 portability right, which does not require real-time API access.
- **Why it is geo-restricted:** The DMA is an EU regulation with no equivalent in any other jurisdiction. LinkedIn has no legal obligation (and currently no commercial incentive) to offer this API to users outside the EEA/Switzerland. The API endpoints enforce geographic restrictions based on the LinkedIn account's location.
- **Official documentation:** [Member Data Portability (Member) — Microsoft Learn](https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/)
- **OAuth & token guide:** [Getting Access to LinkedIn APIs — Microsoft Learn](https://learn.microsoft.com/en-us/linkedin/shared/authentication/getting-access)
- **DMA regulatory context:** [EU Digital Markets Act — European Commission](https://digital-markets-act.ec.europa.eu/index_en)

### Practical implications for non-EU users

If your LinkedIn account is not in the EEA or Switzerland, the `r_dma_portability_self_serve` scope will not be available and API calls will return `403 Not enough permissions`. There is currently no workaround — the restriction is based on account location, not IP address.

## Europass CV Standard

The [Europass CV](https://europass.europa.eu/en/create-europass-cv) is the official, standardized curriculum vitae format recognized across all EU member states. It provides a machine-readable XML schema for qualifications, work history, education, and skills.

This project generates a Europass-compliant XML file (`artifacts/europass_cv.xml`) from the same LinkedIn data and embeds it as a PDF attachment inside the downloadable CV. This means a single PDF file carries both a human-readable CV and a machine-parseable Europass XML — useful for European employers and HR systems that expect the Europass format.
