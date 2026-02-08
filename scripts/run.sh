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
if ! command -v pandoc &>/dev/null || ! command -v wkhtmltopdf &>/dev/null; then
  echo "📦 Installing pandoc & wkhtmltopdf..."
  if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq 2>/dev/null || true
    sudo apt-get install -y -qq pandoc wkhtmltopdf librsvg2-bin imagemagick poppler-utils 2>/dev/null
  else
    echo "⚠ pandoc/wkhtmltopdf not found and cannot auto-install. Skipping PDF generation."
    echo "✅ Done (README + Europass XML generated, PDF skipped)."
    exit 0
  fi
fi

# Ensure pdfattach is available (poppler-utils) for embedding XML metadata
if ! command -v pdfattach &>/dev/null; then
  echo "📦 Installing poppler-utils (for pdfattach)..."
  if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq 2>/dev/null || true
    sudo apt-get install -y -qq poppler-utils 2>/dev/null
  fi
fi

if [[ ! -f github-markdown.css ]]; then
  echo "📥 Downloading GitHub CSS theme..."
  curl -sL https://raw.githubusercontent.com/sindresorhus/github-markdown-css/main/github-markdown.css -o github-markdown.css
fi

# ── 4b. Convert animated SVGs to JPGs for PDF compatibility ──────────
if command -v rsvg-convert &>/dev/null || command -v convert &>/dev/null; then
  echo "▶ Converting SVGs to JPGs for PDF..."
  for svg in profile/*.svg; do
    [ -f "$svg" ] || continue
    jpg="${svg%.svg}.jpg"
    if command -v rsvg-convert &>/dev/null; then
      rsvg-convert -f png "$svg" | convert png:- -background white -flatten "$jpg"
    else
      convert -background white -flatten "$svg" "$jpg"
    fi
    echo "   • $svg → $jpg"
  done
else
  echo "⚠ No SVG converter found. Installing librsvg2-bin + imagemagick..."
  if command -v apt-get &>/dev/null; then
    sudo apt-get install -y -qq librsvg2-bin imagemagick 2>/dev/null
    for svg in profile/*.svg; do
      [ -f "$svg" ] || continue
      jpg="${svg%.svg}.jpg"
      rsvg-convert -f png "$svg" | convert png:- -background white -flatten "$jpg"
      echo "   • $svg → $jpg"
    done
  else
    echo "⚠ Cannot install SVG converters. PDF may contain broken images."
  fi
fi

# ── 4c. Create a temp README with JPG refs for PDF export ────────────
echo "▶ Converting README to PDF..."
sed 's|\.svg"|\.jpg"|g' README.md > README_pdf.md
pandoc README_pdf.md \
  -f gfm \
  -t html5 \
  --metadata pagetitle="Francesco Belacca - CV" \
  --css github-markdown.css \
  --self-contained \
  -o Francesco_Belacca_CV.pdf
rm -f README_pdf.md

# ── 5. Embed Europass XML metadata into the PDF ─────────────────────
if command -v pdfattach &>/dev/null && [[ -f europass_cv.xml ]]; then
  echo "▶ Embedding europass_cv.xml into PDF as attachment..."
  pdfattach Francesco_Belacca_CV.pdf europass_cv.xml Francesco_Belacca_CV_with_metadata.pdf
  mv Francesco_Belacca_CV_with_metadata.pdf Francesco_Belacca_CV.pdf
  echo "   ✅ Europass XML metadata embedded into PDF"
else
  echo "⚠ pdfattach not available or europass_cv.xml missing — PDF generated without embedded metadata."
fi

echo "✅ Done. Generated:"
echo "   • README.md"
echo "   • europass_cv.xml"
echo "   • Francesco_Belacca_CV.pdf (with embedded Europass XML metadata)"
