#!/usr/bin/env node
// Convert a local HTML file to PDF using Puppeteer (headless Chromium).
//
// Usage: node scripts/html_to_pdf.js <input.html> <output.pdf>
//
// Waits for all network requests (images, fonts) to settle before printing,
// which ensures shields.io badges and skillicons.dev images are rendered.

const puppeteer = require('puppeteer');
const path = require('path');

async function main() {
  const [inputHtml, outputPdf] = process.argv.slice(2);

  if (!inputHtml || !outputPdf) {
    console.error('Usage: node html_to_pdf.js <input.html> <output.pdf>');
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

    // Navigate to the local HTML file and wait for all network activity to stop
    await page.goto(`file://${absoluteHtml}`, {
      waitUntil: 'networkidle0',   // wait until 0 network connections for 500ms
      timeout: 30000,
    });

    // Extra wait for any remaining rendering/layout
    await new Promise(r => setTimeout(r, 2000));

    // Generate PDF
    await page.pdf({
      path: absolutePdf,
      format: 'A4',
      printBackground: true,
      margin: { top: '15mm', bottom: '15mm', left: '15mm', right: '15mm' },
    });

    console.log(`   ✅ PDF saved: ${absolutePdf}`);
  } finally {
    await browser.close();
  }
}

main().catch(err => {
  console.error('❌ PDF generation failed:', err.message);
  process.exit(1);
});
