#!/usr/bin/env bash
set -euo pipefail

# ── Render the ATS-friendly slim resume as a selectable-text PDF ─────
#
# Reads:  slim/README.md (or the first argument)
# Writes: slim/Francesco_Belacca_CV.pdf (or the second argument)
#
# This renderer deliberately uses a local, single-column stylesheet with no
# images, icons, tables, columns, headers, or footers. It uses the repository's
# Puppeteer dependency through a slim-specific launcher; no existing CV or
# README is read or modified.
#
# Prerequisites:
#   - pandoc
#   - Node.js and src/node_modules/puppeteer (npm ci in src)
#   - pdfinfo (optional; used to enforce the one-page contract)

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

INPUT_MD="${1:-slim/README.md}"
OUTPUT_PDF="${2:-slim/Francesco_Belacca_CV.pdf}"

if [[ ! -f "$INPUT_MD" ]]; then
  echo "❌ $INPUT_MD not found. Generate slim/README.md first."
  exit 1
fi

if ! command -v pandoc &>/dev/null; then
  echo "❌ pandoc is required to render the slim PDF."
  exit 1
fi

if ! NODE_PATH="$REPO_ROOT/src/node_modules" node -e "require('puppeteer')" 2>/dev/null; then
  echo "❌ Puppeteer is not installed. Run 'npm ci' in src/ first."
  exit 1
fi

mkdir -p "$(dirname "$OUTPUT_PDF")"
TEMP_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$TEMP_DIR"
}
trap cleanup EXIT

pandoc "$INPUT_MD" \
  -f gfm \
  -t html5 \
  --metadata pagetitle="Francesco Belacca - Slim Resume" \
  -o "$TEMP_DIR/body.html"

cat > "$TEMP_DIR/resume.html" <<'HTMLEOF'
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Francesco Belacca - Slim Resume</title>
  <style>
    @page { size: A4; margin: 0; }

    /* ATS-safe: common font, black text, one linear document flow. */
    html, body {
      margin: 0;
      padding: 0;
      background: #fff;
      color: #000;
      font-family: Arial, Helvetica, sans-serif;
      font-size: 10pt;
      line-height: 1.16;
    }

    .markdown-body {
      box-sizing: border-box;
      width: 100%;
      max-width: none;
      padding: 12.7mm;
      margin: 0;
    }

    h1 {
      margin: 0 0 2pt;
      font-size: 19pt;
      line-height: 1.05;
      font-weight: 700;
    }

    h1 + p {
      margin: 0 0 2pt;
      font-size: 10.5pt;
      font-weight: 700;
    }

    h1 + p + p {
      margin: 0 0 9pt;
      font-size: 9pt;
      line-height: 1.1;
    }

    h2 {
      margin: 5pt 0 2pt;
      padding-bottom: 0.5pt;
      font-size: 11pt;
      line-height: 1.05;
      font-weight: 700;
      letter-spacing: 0.3pt;
    }

    h3 {
      margin: 3pt 0 0.5pt;
      font-size: 10pt;
      line-height: 1.08;
      font-weight: 700;
    }

    p {
      margin: 0 0 2pt;
    }

    ul {
      margin: 0.5pt 0 2pt 14pt;
      padding: 0;
    }

    li {
      margin: 0 0 0.8pt;
      padding-left: 1pt;
    }

    /* Keep role blocks intact when Chromium can do so without overflow. */
    h3, h3 + p, h3 + p + ul, li {
      break-inside: avoid;
      page-break-inside: avoid;
    }

    a {
      color: #000;
      text-decoration: none;
    }

    @media print {
      .markdown-body { padding: 12.7mm; }
    }
  </style>
</head>
<body>
  <main class="markdown-body">
HTMLEOF
cat "$TEMP_DIR/body.html" >> "$TEMP_DIR/resume.html"
cat >> "$TEMP_DIR/resume.html" <<'HTMLEOF'
  </main>
</body>
</html>
HTMLEOF

NODE_PATH="$REPO_ROOT/src/node_modules" node "$REPO_ROOT/src/slim_html_to_pdf.js" \
  "$TEMP_DIR/resume.html" \
  "$OUTPUT_PDF"

if command -v pdfinfo &>/dev/null; then
  pages="$(pdfinfo "$OUTPUT_PDF" | awk '/^Pages:/ { print $2 }')"
  if [[ "$pages" != "1" ]]; then
    echo "❌ Slim resume rendered to ${pages:-unknown} pages; expected exactly 1."
    exit 1
  fi
  echo "   ✅ One-page check passed"
fi

if command -v pdftotext &>/dev/null; then
  text_length="$(pdftotext "$OUTPUT_PDF" - 2>/dev/null | wc -c | tr -d ' ')"
  if [[ "${text_length:-0}" -lt 200 ]]; then
    echo "❌ Slim PDF contains unexpectedly little selectable text."
    exit 1
  fi
  echo "   ✅ Selectable-text check passed (${text_length} characters)"
fi

echo "✅ Slim PDF generated: $OUTPUT_PDF"
