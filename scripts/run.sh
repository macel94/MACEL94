#!/usr/bin/env bash
set -euo pipefail

# ── Generate README, Europass CV XML and PDF from LinkedIn data ──────
#
# Usage (local):
#   export LINKEDIN_ACCESS_TOKEN="your_token"   # optional - uses cached data if missing
#   ./scripts/run.sh
#
# Usage (CI):
#   The GitHub Actions workflow calls this script directly.
#
# Prerequisites:
#   - .NET 10+
#   - pandoc & wkhtmltopdf (for PDF export; auto-installed if missing on Debian/Ubuntu)

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

# ── 1. Fetch LinkedIn data ───────────────────────────────────────────
if [[ -n "${LINKEDIN_ACCESS_TOKEN:-}" ]]; then
  echo "▶ Fetching LinkedIn data..."
  dotnet run scripts/fetch_linkedin.cs
else
  if [[ -f scripts/linkedin_raw.json ]]; then
    echo "⏭ No LINKEDIN_ACCESS_TOKEN set - using cached scripts/linkedin_raw.json"
  else
    echo "❌ No LINKEDIN_ACCESS_TOKEN and no cached linkedin_raw.json. Set the token or provide the file."
    exit 1
  fi
fi

# ── 2. Generate README ───────────────────────────────────────────────
echo "▶ Generating README.md..."
dotnet run scripts/generate_readme.cs

# ── 3. Generate Europass CV XML ──────────────────────────────────────
echo "▶ Generating europass_cv.xml..."
dotnet run scripts/generate_cv.cs

# ── 4. Generate PDF from README ──────────────────────────────────────
# PDF generation is handled by scripts/generate_pdf.sh (uses headless
# Chromium for proper rendering of badges, icons, and SVGs).
# In CI the workflow calls it after GitHub Stats SVGs are generated.
if [[ "${SKIP_PDF:-}" != "1" ]]; then
  echo "▶ Generating PDF..."
  bash "$REPO_ROOT/scripts/generate_pdf.sh"
else
  echo "⏭ Skipping PDF generation (SKIP_PDF=1)"
fi

echo "✅ Done. Generated:"
echo "   • README.md"
echo "   • europass_cv.xml"
[[ "${SKIP_PDF:-}" != "1" ]] && echo "   • Francesco_Belacca_CV.pdf (with embedded Europass XML metadata)"
