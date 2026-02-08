#!/usr/bin/env bash
set -euo pipefail

# ── Generate PDF from README.md using headless Chromium ──────────────
#
# Uses pandoc (markdown → HTML) + Puppeteer/headless Chromium (HTML → PDF).
# This replaces the old wkhtmltopdf approach which could not:
#   • Render remote images (shields.io badges, skillicons.dev)
#   • Handle animated SVGs (captured frame 0 instead of final state)
#   • Support modern CSS layout
#
# Prerequisites (auto-installed on Debian/Ubuntu if missing):
#   - pandoc
#   - Node.js + puppeteer (npm install)
#   - poppler-utils (for pdfattach, optional)

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

# ── 1. Install pandoc if missing ─────────────────────────────────────
if ! command -v pandoc &>/dev/null; then
  echo "📦 Installing pandoc..."
  if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq 2>/dev/null || true
    sudo apt-get install -y -qq pandoc 2>/dev/null
  else
    echo "❌ pandoc not found and cannot auto-install. Aborting PDF generation."
    exit 1
  fi
fi

# ── 2. Ensure Puppeteer is installed (bundles its own Chromium) ──────
if ! NODE_PATH="$REPO_ROOT/src/node_modules" node -e "require('puppeteer')" 2>/dev/null; then
  echo "📦 Installing puppeteer..."
  (cd "$REPO_ROOT/src" && npm install --save-dev puppeteer 2>/dev/null)
fi

# ── 3. Ensure poppler-utils is available (for pdfattach) ─────────────
if ! command -v pdfattach &>/dev/null; then
  echo "📦 Installing poppler-utils (for pdfattach)..."
  if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq 2>/dev/null || true
    sudo apt-get install -y -qq poppler-utils 2>/dev/null
  fi
fi

# ── 4. Ensure emoji font is installed (for proper icon rendering) ────
if ! fc-list | grep -qi "Noto Color Emoji"; then
  echo "📦 Installing Noto Color Emoji font..."
  if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq 2>/dev/null || true
    sudo apt-get install -y -qq fonts-noto-color-emoji 2>/dev/null
  else
    echo "⚠ Cannot auto-install emoji font — emoji may render as boxes."
  fi
fi

# ── 5. Download GitHub markdown CSS if missing ──────────────────────
if [[ ! -f artifacts/github-markdown.css ]]; then
  echo "📥 Downloading GitHub markdown CSS..."
  mkdir -p artifacts
  curl -sL https://raw.githubusercontent.com/sindresorhus/github-markdown-css/main/github-markdown.css \
    -o artifacts/github-markdown.css
fi

# ── 5. Create static SVGs (strip CSS animations) ────────────────────
#
# The GitHub-readme-stats SVGs use CSS animations (fadeIn, scaleIn,
# rankAnimation) that start with opacity:0.  Static renderers and even
# Chromium's print-to-pdf capture frame 0 instead of the final state.
#
# Fix: inject a <style> block that forces animation-duration to 0s.
# Because the originals use `animation-fill-mode: forwards`, a 0s
# duration means the element immediately adopts the "to" keyframe
# values (opacity:1, scale:1, final stroke-dashoffset, etc.).

echo "▶ Preparing static SVGs for PDF..."
# Clean up any leftover static SVGs from previous runs
rm -f artifacts/profile/*_static.svg

for svg in artifacts/profile/*.svg; do
  [ -f "$svg" ] || continue
  # Skip if this is somehow already a _static file
  [[ "$svg" == *_static.svg ]] && continue
  static="${svg%.svg}_static.svg"

  # Insert override styles right after the opening <style> tag
  sed '/<style>/a\
    /* PDF static override – skip animations, show final state */\
    *, *::before, *::after {\
      animation-duration: 0s !important;\
      animation-delay: 0s !important;\
      transition-duration: 0s !important;\
    }\
    .stagger { opacity: 1 !important; }' \
    "$svg" > "$static"

  echo "   • $svg → $static"
done

# ── 6. Build a PDF-ready markdown (point to static SVGs, strip Download CV section) ─
echo "▶ Converting README to PDF..."
sed -E 's|(artifacts/profile/[a-z-]+)\.svg"|\1_static.svg"|g' README.md \
  | sed '/^### 📥 Download CV$/,/^<sub>/{ /^### 📥 Download CV$/d; /^- \[/d; /^$/d; }' > README_pdf.md

# ── 7. Convert markdown → HTML body with pandoc ─────────────────────
pandoc README_pdf.md \
  -f gfm \
  -t html5 \
  --metadata pagetitle="Francesco Belacca - CV" \
  -o README_pdf_body.html

# ── 8. Wrap in a full HTML document with GitHub styling ──────────────
cat > README_pdf.html <<HTMLEOF
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Francesco Belacca - CV</title>
  <style>
$(cat artifacts/github-markdown.css)

    /* GitHub-style page container */
    .markdown-body {
      box-sizing: border-box;
      min-width: 200px;
      max-width: 980px;
      margin: 0 auto;
      padding: 30px;
    }
    img { max-width: 100%; }

    /* Print-specific tweaks */
    @media print {
      .markdown-body { padding: 10px; }
      details { display: block; }
      details > summary { list-style: none; }
      details > summary::marker { display: none; }
      details[open] > summary ~ * { display: block; }
    }
  </style>
</head>
<body>
  <article class="markdown-body">
$(cat README_pdf_body.html)
  </article>
</body>
</html>
HTMLEOF

# ── 9. Render PDF with Puppeteer (headless Chromium) ─────────────────
echo "▶ Rendering PDF with Puppeteer (headless Chromium)..."
NODE_PATH="$REPO_ROOT/src/node_modules" node "$REPO_ROOT/src/html_to_pdf.js" \
  "$REPO_ROOT/README_pdf.html" \
  "$REPO_ROOT/artifacts/Francesco_Belacca_CV.pdf"

echo "   ✅ PDF generated: artifacts/Francesco_Belacca_CV.pdf"

# ── 10. Embed Europass XML metadata into the PDF ─────────────────────
if command -v pdfattach &>/dev/null && [[ -f artifacts/europass_cv.xml ]]; then
  echo "▶ Embedding europass_cv.xml into PDF as attachment..."
  pdfattach artifacts/Francesco_Belacca_CV.pdf artifacts/europass_cv.xml artifacts/Francesco_Belacca_CV_with_metadata.pdf
  mv artifacts/Francesco_Belacca_CV_with_metadata.pdf artifacts/Francesco_Belacca_CV.pdf
  echo "   ✅ Europass XML metadata embedded into PDF"
else
  echo "⚠ pdfattach not available or europass_cv.xml missing — PDF without embedded metadata."
fi

# ── Cleanup temp files ───────────────────────────────────────────────
rm -f README_pdf.md README_pdf_body.html README_pdf.html
rm -f artifacts/profile/*_static.svg

echo "✅ PDF generation complete."
