import lighthouse from 'lighthouse';
import * as chromeLauncher from 'chrome-launcher';
import { chromium } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
const directory = process.env.AUDIT_DIR || 'test-results/lighthouse';
await mkdir(directory, { recursive: true });
const browser = await chromeLauncher.launch({ chromePath: process.env.CHROMIUM_PATH || chromium.executablePath(), chromeFlags: ['--headless', '--no-sandbox', '--disable-dev-shm-usage'] });
const results = [];
let failed = false;
try {
  for (const path of ['/', '/create', '/how-it-works', '/accessibility']) {
    if (process.env.BASELINE && path !== '/') continue;
    for (const formFactor of ['mobile', 'desktop']) {
      const { lhr, report } = await lighthouse((process.env.BASE_URL || 'http://localhost:8080') + path, {
        port: browser.port, output: ['json', 'html'], logLevel: 'error', onlyCategories: ['performance', 'accessibility', 'best-practices', 'seo'],
      }, { extends: 'lighthouse:default', settings: formFactor === 'desktop' ? {
        formFactor: 'desktop', screenEmulation: { mobile: false, width: 1350, height: 940, deviceScaleFactor: 1, disabled: false },
        throttling: { rttMs: 40, throughputKbps: 10240, cpuSlowdownMultiplier: 1, requestLatencyMs: 0, downloadThroughputKbps: 0, uploadThroughputKbps: 0 },
      } : {} });
      const slug = `${path === '/' ? 'home' : path.slice(1)}-${formFactor}`;
      await writeFile(`${directory}/${slug}.json`, report[0]);
      await writeFile(`${directory}/${slug}.html`, report[1]);
      const scores = Object.fromEntries(Object.entries(lhr.categories).map(([key, v]) => [key, Math.round(v.score * 100)]));
      const metrics = Object.fromEntries(['first-contentful-paint','largest-contentful-paint','cumulative-layout-shift','total-blocking-time'].map(key => [key, lhr.audits[key].numericValue]));
      const issues = Object.values(lhr.audits).filter(a => a.score !== null && a.score < 1 && a.scoreDisplayMode !== 'informative').map(a => ({ id: a.id, title: a.title, score: a.score }));
      const result = { path, formFactor, scores, metrics, issues }; results.push(result); console.log(JSON.stringify(result));
      if (!process.env.BASELINE && (scores.performance < 95 || scores.accessibility < 100 || scores['best-practices'] < 95 || scores.seo < 100)) failed = true;
    }
  }
} finally { await browser.kill(); }
await writeFile(`${directory}/summary.json`, JSON.stringify(results, null, 2));
if (failed) process.exitCode = 1;
