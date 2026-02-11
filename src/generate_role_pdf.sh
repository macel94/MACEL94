#!/usr/bin/env bash
set -euo pipefail

# ── Generate PDF from a role-specific README ─────────────────────────
#
# Reuses the same tooling as generate_pdf.sh but operates on a
# role-specific README stored in artifacts/<role>/README.md.
#
# Usage:
#   ./src/generate_role_pdf.sh <role>
#
# Where <role> is one of: cloud-sre, cloud-devops, cloud-swdev
#
# Output:
#   artifacts/<role>/Francesco_Belacca_CV.pdf
#
# Prerequisites (same as generate_pdf.sh):
#   - pandoc
#   - Node.js + puppeteer
#   - poppler-utils (optional, for pdfattach)

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

ROLE="${1:?Usage: generate_role_pdf.sh <cloud-sre|cloud-devops|cloud-swdev>}"
ROLE_DIR="artifacts/$ROLE"

if [[ ! -f "$ROLE_DIR/README.md" ]]; then
  echo "❌ $ROLE_DIR/README.md not found. Run tailor_readme.sh first."
  exit 1
fi

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

# ── 2. Ensure Puppeteer is installed ─────────────────────────────────
if ! NODE_PATH="$REPO_ROOT/src/node_modules" node -e "require('puppeteer')" 2>/dev/null; then
  echo "📦 Installing puppeteer..."
  (cd "$REPO_ROOT/src" && npm install --save-dev puppeteer 2>/dev/null)
fi

# ── 3. Ensure poppler-utils is available ─────────────────────────────
if ! command -v pdfattach &>/dev/null; then
  echo "📦 Installing poppler-utils (for pdfattach)..."
  if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq 2>/dev/null || true
    sudo apt-get install -y -qq poppler-utils 2>/dev/null
  fi
fi

# ── 4. Ensure emoji font is installed ────────────────────────────────
if ! fc-list | grep -qi "Noto Color Emoji"; then
  echo "📦 Installing Noto Color Emoji font..."
  if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq 2>/dev/null || true
    sudo apt-get install -y -qq fonts-noto-color-emoji 2>/dev/null
  else
    echo "⚠ Cannot auto-install emoji font — emoji may render as boxes."
  fi
fi

# ── 5. Download GitHub markdown CSS if missing ───────────────────────
if [[ ! -f artifacts/github-markdown.css ]]; then
  echo "📥 Downloading GitHub markdown CSS..."
  mkdir -p artifacts
  curl -sL https://raw.githubusercontent.com/sindresorhus/github-markdown-css/main/github-markdown.css \
    -o artifacts/github-markdown.css
fi

# ── 6. Create static SVGs (strip CSS animations) ────────────────────
echo "▶ Preparing static SVGs for PDF..."
rm -f artifacts/profile/*_static.svg

for svg in artifacts/profile/*.svg; do
  [ -f "$svg" ] || continue
  [[ "$svg" == *_static.svg ]] && continue
  static="${svg%.svg}_static.svg"

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

# ── 7. Build PDF-ready markdown ──────────────────────────────────────
echo "▶ Converting $ROLE_DIR/README.md to PDF..."
sed -E 's|\.\./profile/([a-z-]+)\.svg"|artifacts/profile/\1_static.svg"|g' "$ROLE_DIR/README.md" \
  | sed '/^### 📥 Download CV$/,/^<sub>/{ /^### 📥 Download CV$/d; /^- \[/d; /^$/d; }' > README_pdf.md

# ── 8. Convert markdown → HTML body with pandoc ─────────────────────
pandoc README_pdf.md \
  -f gfm \
  -t html5 \
  --metadata pagetitle="Francesco Belacca - CV ($ROLE)" \
  -o README_pdf_body.html

# ── 9. Wrap in a full HTML document with GitHub styling ──────────────
cat > README_pdf.html <<HTMLEOF
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Francesco Belacca - CV ($ROLE)</title>
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

# ── 10. Render PDF with Puppeteer ────────────────────────────────────
echo "▶ Rendering PDF with Puppeteer (headless Chromium)..."
NODE_PATH="$REPO_ROOT/src/node_modules" node "$REPO_ROOT/src/html_to_pdf.js" \
  "$REPO_ROOT/README_pdf.html" \
  "$REPO_ROOT/$ROLE_DIR/Francesco_Belacca_CV.pdf"

echo "   ✅ PDF generated: $ROLE_DIR/Francesco_Belacca_CV.pdf"

# ── 11. Embed Europass XML metadata into the PDF ─────────────────────
if command -v pdfattach &>/dev/null && [[ -f artifacts/europass_cv.xml ]]; then
  echo "▶ Embedding europass_cv.xml into PDF as attachment..."
  pdfattach "$ROLE_DIR/Francesco_Belacca_CV.pdf" artifacts/europass_cv.xml "$ROLE_DIR/Francesco_Belacca_CV_with_metadata.pdf"
  mv "$ROLE_DIR/Francesco_Belacca_CV_with_metadata.pdf" "$ROLE_DIR/Francesco_Belacca_CV.pdf"
  echo "   ✅ Europass XML metadata embedded into PDF"
else
  echo "⚠ pdfattach not available or europass_cv.xml missing — PDF without embedded metadata."
fi

# ── Cleanup temp files ───────────────────────────────────────────────
rm -f README_pdf.md README_pdf_body.html README_pdf.html
rm -f artifacts/profile/*_static.svg

echo "✅ Role-specific PDF generation complete for $ROLE."
