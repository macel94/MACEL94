# LinkedIn → GitHub README Auto-Updater

Automatically keep your GitHub profile README in sync with your LinkedIn profile using the **EU DMA Data Portability API**.

## How it works

```
LinkedIn Profile → DMA Portability API → C# Script (.NET 10) → README.md → GitHub Actions (auto-commit)
```

As an EU/EEA citizen, the [Digital Markets Act](https://learn.microsoft.com/en-us/linkedin/dma/member-data-portability/member-data-portability-member/) grants you the right to programmatically access your LinkedIn data. This project uses two LinkedIn APIs:

| API | Endpoint | What it gives you |
|---|---|---|
| **Member Snapshot API** | `GET /rest/memberSnapshotData?q=criteria&domain=<DOMAIN>` | Point-in-time snapshot of your profile data across 50+ domains |
| **Member Changelog API** | `GET /rest/memberChangeLogs?q=memberAndApplication` | Real-time change events (posts, comments, reactions) from the last 28 days |

### Snapshot Domains used for the README

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

### Other available domains (not used in README, but queryable)

`ADS_CLICKED`, `MEMBER_FOLLOWING`, `login`, `RICH_MEDIA`, `SEARCHES`, `INFERENCE_TAKEOUT`, `ALL_COMMENTS`, `CONTACTS`, `Events`, `RECEIPTS`, `AD_TARGETING`, `REGISTRATION`, `REVIEWS`, `ARTICLES`, `GROUPS`, `COMPANY_FOLLOWS`, `INVITATIONS`, `PHONE_NUMBERS`, `CONNECTIONS`, `EMAIL_ADDRESSES`, `JOB_POSTINGS`, `JOB_APPLICATIONS`, `JOB_SEEKER_PREFERENCES`, `LEARNING`, `INBOX`, `SAVED_JOBS`, `SAVED_JOB_ALERTS`, `TEST_SCORES`, `MEMBER_SHARE_INFO`, `SECURITY_CHALLENGE_PIPE`, `TRUSTED_GRAPH`, `MARKETPLACE_ENGAGEMENTS`, `MARKETPLACE_PROVIDERS`, `MARKETPLACE_OPPORTUNITIES`, `ACTOR_SAVE_ITEM`, `JOB_APPLICANT_SAVED_ANSWERS`, `TALENT_QUESTION_SAVED_RESPONSE`, `ALL_LIKES`, `ALL_VOTES`, `RECEIPTS_LBP`, `easyapply-blocking`, `LEARNING_COACH_AI_TAKEOUT`, `LEARNING_COACH_INBOX`, `LEARNING_ROLEPLAY_INBOX`, `ACCOUNT_HISTORY`, `INSTANT_REPOSTS`, `IDENTITY_CREDENTIALS_AND_ASSETS`, `ADS_LAN`

## Setup (one-time)

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

### 3. Add the token to GitHub Secrets

1. Go to your repo → **Settings → Secrets and variables → Actions**
2. Click **New repository secret**
3. Name: `LINKEDIN_ACCESS_TOKEN`
4. Value: paste the token from step 2

### 4. Run the workflow

- Go to **Actions → Update README from LinkedIn → Run workflow**
- Or wait for the weekly schedule (every Monday at 06:00 UTC)

## Local development

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later.

```bash
export LINKEDIN_ACCESS_TOKEN="your_token_here"
dotnet run scripts/fetch_linkedin.cs
```

No project file, no `dotnet restore`, no compilation step — .NET 10 [file-based apps](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/) handle everything from a single `.cs` file.

The script will:
1. Fetch all relevant domains from the Member Snapshot API
2. Save raw JSON to `scripts/linkedin_raw.json` (gitignored)
3. Generate a formatted `README.md` at the repo root

## API Details

### Authentication

All requests use:
```
Authorization: Bearer <access_token>
Linkedin-Version: 202312
Content-Type: application/json
```

### Member Snapshot API

```
GET https://api.linkedin.com/rest/memberSnapshotData?q=criteria&domain=PROFILE
```

Returns paginated results. Each page has `paging.links` with `rel: "next"` for the next page.

### Member Changelog API

```
GET https://api.linkedin.com/rest/memberChangeLogs?q=memberAndApplication&startTime=<epoch_ms>
```

Returns real-time activity events from the last 28 days. Useful for tracking profile changes.

### Rate Limits

LinkedIn enforces rate limits. The script makes one request per domain — with 17 domains, that's ~17+ requests per run (more with pagination). The weekly schedule is very conservative.

## File structure

```
.
├── .github/workflows/update-readme.yml   # GitHub Actions workflow
├── scripts/
│   ├── fetch_linkedin.cs                 # Main script (C# / .NET 10 file-based app)
│   └── .gitignore                        # Ignores linkedin_raw.json
├── README.md                             # Auto-generated from LinkedIn
└── SETUP.md                              # This file
```
