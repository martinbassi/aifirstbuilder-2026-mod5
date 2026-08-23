#!/usr/bin/env node
/**
 * Standalone theme verification script (FEAT-002).
 *
 * Runs OUTSIDE `ng test` / Vitest on purpose: `styles.css` (the global stylesheet where
 * `@font-face`, the `html,body` font-family rule and the `:root` CSS variables live) is not
 * injected into the DOM of the Angular/Vitest test runner in this project, so
 * `getComputedStyle(document.body)` cannot observe it inside a component spec. This script reads
 * `index.html` and `styles.css` as plain text instead, with only Node built-ins (`node:fs`,
 * `node:path`, `node:assert/strict`) — no new dependency.
 *
 * Covers:
 *   - AC-01: Quicksand is applied globally, without depending on Google Fonts.
 *   - AC-02: a reasonable fallback stack exists if the font fails to load.
 *   - AC-07: theme colors live as CSS variables in `:root`, not in an isolated class.
 *
 * Does NOT cover AC-06 (primary `nz-button` renders `#FE6944`): that requires a real browser
 * render of ng-zorro's stylesheet, which cannot be verified by reading text without faking a
 * result. AC-06 is verified manually in VERIFY (see `docs/daw/reports/verify-FEAT-002.md`), the
 * same way AC-05 (favicon) is documented in the spec's Block 4.
 *
 * Usage: node scripts/verify-theme.mjs   (also wired as `npm run verify-theme`)
 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const INDEX_HTML_PATH = join(__dirname, '../src/index.html');
const STYLES_CSS_PATH = join(__dirname, '../src/styles.css');

const GENERIC_FALLBACK_FONTS = [
  '-apple-system',
  'BlinkMacSystemFont',
  'Segoe UI',
  'Helvetica',
  'Arial',
  'sans-serif',
  'system-ui',
];

function extractHtmlBodyFontFamily(stylesCss) {
  const ruleMatch = stylesCss.match(/html\s*,\s*body\s*\{([^}]*)\}/);
  if (!ruleMatch) return null;
  const declMatch = ruleMatch[1].match(/font-family\s*:\s*([^;]+);/);
  if (!declMatch) return null;
  return declMatch[1].trim();
}

function extractRootBlock(stylesCss) {
  const match = stylesCss.match(/:root\s*\{([^}]*)\}/);
  return match ? match[1] : null;
}

export function checkAc01NoGoogleFontsLink(indexHtml) {
  assert.ok(
    !/fonts\.googleapis\.com/.test(indexHtml),
    'AC-01 FAILED: index.html contains a reference to fonts.googleapis.com — Quicksand must be self-hosted, not loaded from Google Fonts.',
  );
  assert.ok(
    !/fonts\.gstatic\.com/.test(indexHtml),
    'AC-01 FAILED: index.html contains a reference to fonts.gstatic.com — Quicksand must be self-hosted, not loaded from Google Fonts.',
  );
}

export function checkAc01FontFamilyIsQuicksand(stylesCss) {
  const fontFamily = extractHtmlBodyFontFamily(stylesCss);
  assert.ok(fontFamily !== null, 'AC-01 FAILED: styles.css has no font-family rule for html,body.');
  assert.ok(
    /^['"]Quicksand['"]/.test(fontFamily),
    `AC-01 FAILED: html,body font-family does not start with 'Quicksand'. Found: ${fontFamily}`,
  );
}

export function checkAc02HasFallbackStack(stylesCss) {
  const fontFamily = extractHtmlBodyFontFamily(stylesCss);
  assert.ok(fontFamily !== null, 'AC-02 FAILED: styles.css has no font-family rule for html,body.');
  const fonts = fontFamily.split(',').map((font) => font.trim());
  assert.ok(
    fonts.length > 1,
    `AC-02 FAILED: html,body font-family only declares 'Quicksand', with no fallback stack. Found: ${fontFamily}`,
  );
  const hasGenericFallback = fonts
    .slice(1)
    .some((font) =>
      GENERIC_FALLBACK_FONTS.some((generic) => font.toLowerCase().includes(generic.toLowerCase())),
    );
  assert.ok(
    hasGenericFallback,
    `AC-02 FAILED: html,body font-family has no recognizable system/generic fallback font after 'Quicksand'. Found: ${fontFamily}`,
  );
}

export function checkAc07CssVariablesInRoot(stylesCss) {
  const rootBlock = extractRootBlock(stylesCss);
  assert.ok(rootBlock !== null, 'AC-07 FAILED: styles.css has no :root { ... } block.');
  assert.ok(
    /--ant-primary-color\s*:/.test(rootBlock),
    'AC-07 FAILED: :root block does not define --ant-primary-color.',
  );
  assert.ok(
    /--app-color-secondary\s*:/.test(rootBlock),
    'AC-07 FAILED: :root block does not define --app-color-secondary.',
  );
}

export function runAllChecks(indexHtml, stylesCss) {
  const checks = [
    ['AC-01 (no Google Fonts link in index.html)', () => checkAc01NoGoogleFontsLink(indexHtml)],
    [
      "AC-01 (html,body font-family starts with 'Quicksand')",
      () => checkAc01FontFamilyIsQuicksand(stylesCss),
    ],
    ['AC-02 (fallback font stack after Quicksand)', () => checkAc02HasFallbackStack(stylesCss)],
    [
      'AC-07 (theme colors are CSS variables in :root)',
      () => checkAc07CssVariablesInRoot(stylesCss),
    ],
  ];

  const failures = [];
  for (const [name, check] of checks) {
    try {
      check();
      console.log(`✅ ${name}`);
    } catch (error) {
      console.error(`❌ ${name}`);
      console.error(`   ${error.message}`);
      failures.push(name);
    }
  }
  return failures;
}

function readSourceFile(path) {
  try {
    return readFileSync(path, 'utf-8');
  } catch (error) {
    if (error.code === 'ENOENT') {
      throw new Error(
        `Could not find ${path} — run this script from the frontend/ directory (npm run verify-theme).`,
      );
    }
    throw error;
  }
}

function main() {
  const indexHtml = readSourceFile(INDEX_HTML_PATH);
  const stylesCss = readSourceFile(STYLES_CSS_PATH);
  const failures = runAllChecks(indexHtml, stylesCss);

  if (failures.length > 0) {
    console.error(`\n${failures.length} check(s) failed.`);
    process.exit(1);
  }
  console.log('\nAll theme checks passed.');
  process.exit(0);
}

const isMainModule = process.argv[1] === fileURLToPath(import.meta.url);
if (isMainModule) {
  main();
}
