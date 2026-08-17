#!/usr/bin/env node
// Render the slim resume with its stylesheet-controlled margins.
//
// This is intentionally separate from html_to_pdf.js: the existing renderer
// applies 15 mm PDF margins for the full GitHub README, while the ATS-safe slim
// document needs its own 0.5-inch layout without changing that behavior.
//
// Usage: node src/slim_html_to_pdf.js <input.html> <output.pdf>

const puppeteer = require('puppeteer');
const path = require('path');
const { pathToFileURL } = require('url');

async function main() {
  const [inputHtml, outputPdf] = process.argv.slice(2);

  if (!inputHtml || !outputPdf) {
    console.error('Usage: node slim_html_to_pdf.js <input.html> <output.pdf>');
    process.exit(1);
  }

  const absoluteHtml = path.resolve(inputHtml);
  const absolutePdf = path.resolve(outputPdf);

  const browser = await puppeteer.launch({
    headless: true,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
      '--disable-gpu',
    ],
  });

  try {
    const page = await browser.newPage();
    await page.goto(pathToFileURL(absoluteHtml).href, {
      waitUntil: 'networkidle0',
      timeout: 60000,
    });
    await new Promise(resolve => setTimeout(resolve, 250));

    await page.pdf({
      path: absolutePdf,
      format: 'A4',
      printBackground: true,
      // The stylesheet owns the 12.7 mm (0.5 inch) content padding.
      margin: { top: '0mm', bottom: '0mm', left: '0mm', right: '0mm' },
    });

    console.log(`   ✅ Slim PDF saved: ${absolutePdf}`);
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error('❌ Slim PDF generation failed:', error.message);
  process.exit(1);
});
