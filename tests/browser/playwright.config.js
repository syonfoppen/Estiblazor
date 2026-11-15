const { defineConfig } = require('@playwright/test');
module.exports = defineConfig({
  testDir: '.', testMatch: '**/*.spec.js', workers: 1, timeout: 90000,
  use: { baseURL: process.env.BASE_URL || 'http://localhost:8080', headless: true,
    launchOptions: process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {},
    screenshot: 'only-on-failure', trace: 'retain-on-failure' },
});
