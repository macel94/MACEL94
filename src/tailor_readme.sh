#!/usr/bin/env bash
set -euo pipefail

# ── Tailor README.md for a specific role using GitHub Models API ──────
#
# Reads the existing README.md and uses an LLM (GPT-4.1 via GitHub Models)
# to rewrite the About Me section and experience descriptions to better
# target a specific job role.
#
# Usage:
#   GITHUB_TOKEN="ghp_xxx" ./src/tailor_readme.sh <role>
#
# Where <role> is one of: cloud-sre, cloud-devops, cloud-swdev
#
# Output:
#   artifacts/<role>/README.md     – tailored README
#
# Prerequisites:
#   - GITHUB_TOKEN with models:read scope
#   - curl, jq

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

ROLE="${1:?Usage: tailor_readme.sh <cloud-sre|cloud-devops|cloud-swdev>}"

# ── Validate role ────────────────────────────────────────────────────
case "$ROLE" in
  cloud-sre)
    ROLE_TITLE="Cloud Site Reliability Engineer (SRE)"
    ROLE_FOCUS="site reliability engineering, observability, incident management, SLOs/SLIs/SLAs, production reliability, monitoring, alerting, capacity planning, toil reduction, on-call practices, chaos engineering, post-mortems, and infrastructure automation"
    ;;
  cloud-devops)
    ROLE_TITLE="Cloud DevOps Engineer"
    ROLE_FOCUS="CI/CD pipelines, infrastructure as code, deployment automation, container orchestration, release engineering, configuration management, platform engineering, developer experience, GitOps, build systems, and DevSecOps practices"
    ;;
  cloud-swdev)
    ROLE_TITLE="Cloud Software Developer"
    ROLE_FOCUS="cloud-native application development, distributed systems design, API development, microservices architecture, backend engineering, software design patterns, code quality, testing strategies, performance optimization, and cloud services integration"
    ;;
  *)
    echo "❌ Unknown role: $ROLE"
    echo "   Valid roles: cloud-sre, cloud-devops, cloud-swdev"
    exit 1
    ;;
esac

# ── Check prerequisites ─────────────────────────────────────────────
if [[ -z "${GITHUB_TOKEN:-}" ]]; then
  echo "❌ GITHUB_TOKEN is required (with models:read scope for GitHub Models API)."
  exit 1
fi

for cmd in curl jq; do
  if ! command -v "$cmd" &>/dev/null; then
    echo "❌ $cmd not found. Please install it."
    exit 1
  fi
done

# ── Read source README ──────────────────────────────────────────────
if [[ ! -f README.md ]]; then
  echo "❌ README.md not found in repo root."
  exit 1
fi

README_CONTENT="$(cat README.md)"

# ── Prepare output directory ────────────────────────────────────────
OUTPUT_DIR="artifacts/$ROLE"
mkdir -p "$OUTPUT_DIR"

# ── Call GitHub Models API (GPT-4.1) ───────────────────────────────
echo "▶ Tailoring README.md for $ROLE_TITLE using GitHub Models API..."

# Build the prompt — ask the LLM to rewrite specific sections
SYSTEM_PROMPT="You are an expert CV/resume writer. You will receive a GitHub profile README.md in markdown format. Your task is to rewrite ONLY the following sections to better target a ${ROLE_TITLE} position:

1. The headline (line starting with '### ' right after the '# Hi, I'm' line)
2. The '## 🧑‍💻 About Me' section (everything between '## 🧑‍💻 About Me' and '## 📊 GitHub Stats')
3. The experience descriptions (the bullet points under each job title in the '## 💼 Experience' section, and the descriptions in the collapsed 'Earlier roles' section)

Rules:
- Keep ALL the same markdown structure, formatting, links, badges, and sections
- Do NOT change the person's name, contact info, location, certifications, education, languages, volunteering, tech stack, GitHub stats, or featured projects sections
- Do NOT invent new experiences or skills — only rephrase and re-emphasize existing content
- Focus the headline, summary, and experience bullets on: ${ROLE_FOCUS}
- Keep the same tone (professional, concise, technical)
- Maintain all existing dates, company names, and locations exactly as they are
- The output must be the COMPLETE README.md file in valid markdown, not just the changed sections
- Do NOT wrap the output in markdown code fences or add any commentary — output ONLY the raw markdown content"

USER_PROMPT="Here is the README.md to tailor for a ${ROLE_TITLE} position:

${README_CONTENT}"

# Escape for JSON
SYSTEM_JSON=$(jq -Rs '.' <<< "$SYSTEM_PROMPT")
USER_JSON=$(jq -Rs '.' <<< "$USER_PROMPT")

REQUEST_BODY=$(cat <<REQEOF
{
  "model": "openai/gpt-4.1",
  "messages": [
    {"role": "system", "content": ${SYSTEM_JSON}},
    {"role": "user", "content": ${USER_JSON}}
  ],
  "max_tokens": 16000,
  "temperature": 0.3
}
REQEOF
)

# Make the API call
RESPONSE=$(curl -sS -w "\n%{http_code}" -X POST \
  -H "Accept: application/vnd.github+json" \
  -H "Authorization: Bearer ${GITHUB_TOKEN}" \
  -H "Content-Type: application/json" \
  "https://models.github.ai/inference/chat/completions" \
  -d "$REQUEST_BODY")

# Split response body and HTTP status code
HTTP_CODE=$(echo "$RESPONSE" | tail -1)
RESPONSE_BODY=$(echo "$RESPONSE" | sed '$d')

if [[ "$HTTP_CODE" -ne 200 ]]; then
  echo "❌ GitHub Models API returned HTTP $HTTP_CODE"
  echo "$RESPONSE_BODY" | jq . 2>/dev/null || echo "$RESPONSE_BODY"
  exit 1
fi

# Extract the assistant's message content
TAILORED_README=$(echo "$RESPONSE_BODY" | jq -r '.choices[0].message.content // empty')

if [[ -z "$TAILORED_README" ]]; then
  echo "❌ No content in API response."
  echo "$RESPONSE_BODY" | jq . 2>/dev/null || echo "$RESPONSE_BODY"
  exit 1
fi

# ── Write tailored README ───────────────────────────────────────────
echo "$TAILORED_README" > "$OUTPUT_DIR/README.md"

# Fix relative paths: the tailored README lives in artifacts/<role>/,
# not the repo root, so local references must be adjusted.
# 1. SVG images: ./artifacts/profile/ → ../profile/
# 2. PDF download: ./artifacts/Francesco_Belacca_CV.pdf → ./Francesco_Belacca_CV.pdf
sed -i -E \
  -e 's|(\./)?artifacts/profile/|../profile/|g' \
  -e 's|(\./)?artifacts/Francesco_Belacca_CV\.pdf|./Francesco_Belacca_CV.pdf|g' \
  "$OUTPUT_DIR/README.md"

echo "   ✅ Tailored README written to $OUTPUT_DIR/README.md"
