const { test, expect } = require('@playwright/test');
const AxeBuilder = require('@axe-core/playwright').default;
async function audit(page, name) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice']).analyze();
  await test.info().attach(name, { body: JSON.stringify(results, null, 2), contentType: 'application/json' });
  expect(results.violations.map(v => ({ id: v.id, impact: v.impact, nodes: v.nodes.map(n => ({ target: n.target, summary: n.failureSummary })) }))).toEqual([]);
}

test('public content, metadata, structured answers and join work without JavaScript', async ({ browser, request }) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  const page = await context.newPage(); await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Great work starts');
  await expect(page.getByRole('heading', { name: 'Planning poker, without the setup.' })).toBeVisible();
  expect(await page.locator('script[src*="blazor"]').count()).toBe(0);
  expect(await page.locator('link[rel="canonical"]').getAttribute('href')).toMatch(/\/$/);
  expect(await page.locator('meta[name="description"]').getAttribute('content')).toContain('planning poker');
  const schema = JSON.parse(await page.locator('script[type="application/ld+json"]').textContent());
  expect(schema['@graph'][0]['@type']).toBe('WebApplication');
  for (const q of schema['@graph'][1].mainEntity) {
    await expect(page.getByText(q.name, { exact: true })).toBeVisible();
    const detail = page.locator('details').filter({ hasText: q.name }); await detail.locator('summary').click();
    await expect(detail.getByText(q.acceptedAnswer.text, { exact: true })).toBeVisible();
  }
  await page.getByLabel('Room code or invitation link').fill('000000000000');
  await page.getByRole('button', { name: 'Join room →' }).click();
  await expect(page.getByRole('alert')).toContainText('Room not found');
  // Query parameters must not enter canonical discovery URLs.
  expect(await page.locator('link[rel="canonical"]').getAttribute('href')).not.toContain('?');
  const robots = await (await request.get('/robots.txt')).text(); expect(robots).not.toContain('Disallow: /rooms/'); expect(robots).toContain('Disallow: /manage/');
  const sitemap = await (await request.get('/sitemap.xml')).text(); expect(sitemap).toContain('/how-it-works'); expect(sitemap).not.toContain('/rooms/'); expect(sitemap).not.toContain('/manage/');
  const privateResponse = await request.get('/rooms/000000000000'); expect(privateResponse.headers()['x-robots-tag']).toContain('noindex'); expect(privateResponse.headers()['cache-control']).toContain('no-store');
  const svgHref = await page.locator('link[rel="icon"][type="image/svg+xml"]').getAttribute('href');
  const icon = await request.get(svgHref); expect(icon.status()).toBe(200); expect(icon.headers()['content-type']).toContain('image/svg+xml');
  for (const [selector, size] of [['link[rel="icon"][type="image/png"]', 32], ['link[rel="apple-touch-icon"]', 180]]) {
    const image = await request.get(await page.locator(selector).getAttribute('href'));
    expect(image.status()).toBe(200); const bytes = await image.body();
    expect(bytes.readUInt32BE(16)).toBe(size); expect(bytes.readUInt32BE(20)).toBe(size);
  }
  await context.close();
});

test('light and dark public pages and editor pass automated accessibility checks', async ({ page }) => {
  for (const theme of ['light', 'dark']) {
    for (const route of ['/', '/how-it-works', '/accessibility', '/create']) {
      await page.goto(route);
      await page.evaluate(t => { document.documentElement.dataset.theme = t; localStorage.setItem('estiblazor.theme', t); }, theme);
      if (route === '/create') await expect(page.getByRole('button', { name: 'Create room ↗' })).toBeEnabled();
      await audit(page, `${theme}-${route || 'home'}`);
    }
  }
});

test('skip link, keyboard, 320px reflow, text spacing and forced colors stay usable', async ({ page }) => {
  await page.goto('/'); await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Skip to main content' })).toBeFocused();
  await page.keyboard.press('Enter'); await expect(page.locator('#main-content')).toBeFocused();
  await page.setViewportSize({ width: 320, height: 800 });
  for (const route of ['/', '/create', '/how-it-works', '/accessibility']) {
    await page.goto(route);
    await page.addStyleTag({ content: '* { line-height: 1.5 !important; letter-spacing: .12em !important; word-spacing: .16em !important; } p { margin-bottom: 2em !important; }' });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), route).toBeTruthy();
  }
  await page.setViewportSize({ width: 1280, height: 800 });
  for (const route of ['/', '/create']) {
    await page.goto(route);
    await page.evaluate(() => {
      const sizes = [...document.querySelectorAll('body *')].map(e => [e, parseFloat(getComputedStyle(e).fontSize)]);
      for (const [element, size] of sizes) element.style.fontSize = `${size * 2}px`;
    });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `200% text ${route}`).toBeTruthy();
  }
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' }); await page.goto('/create');
  await expect(page.getByLabel('Room name', { exact: true })).toBeEnabled();
  await page.getByLabel('Room name', { exact: true }).focus();
  await expect(page.getByLabel('Room name', { exact: true })).toBeFocused();
  const outline = await page.getByLabel('Room name', { exact: true }).evaluate(e => getComputedStyle(e).outlineStyle);
  expect(outline).not.toBe('none');
  await page.screenshot({ path: 'test-results/accessibility-forced-colors.png', fullPage: true });
  await page.evaluate(() => document.getElementById('components-reconnect-modal').className = 'components-reconnect-show');
  await expect(page.locator('#components-reconnect-modal')).toBeVisible();
  expect(await page.locator('#components-reconnect-modal').evaluate(e => getComputedStyle(e).position)).toBe('static');
  await expect(page.getByLabel('Room name', { exact: true })).toBeEnabled();
  await page.getByLabel('Room name', { exact: true }).focus();
  await expect(page.getByLabel('Room name', { exact: true })).toBeFocused();
});

test('owner, guest, reveal, editor, errors and history pass accessibility checks', async ({ page, browser }) => {
  await page.goto('/create'); await page.getByLabel('Room name', { exact: true }).fill('Accessibility test');
  await page.getByRole('button', { name: 'Create room ↗' }).click();
  await expect(page.getByRole('heading', { name: 'Your room is ready.' })).toBeFocused();
  await audit(page, 'created-room');
  const invite = await page.getByLabel('Invitation link', { exact: true }).inputValue();
  await page.getByRole('link', { name: 'Enter your room →' }).click(); await audit(page, 'owner-unjoined');
  await page.getByLabel('Display name', { exact: true }).fill('Alex'); await page.getByRole('button', { name: 'Join the room →' }).click();
  const guestContext = await browser.newContext(); const guest = await guestContext.newPage(); await guest.goto(invite); await audit(guest, 'guest-unjoined');
  await guest.getByLabel('Display name', { exact: true }).fill('Guest'); await guest.getByRole('button', { name: 'Join the room →' }).click();
  await page.getByRole('group', { name: 'Effort voting cards' }).getByRole('button', { name: '3', exact: true }).click();
  await expect(page.locator('.sr-only[role="status"]')).toHaveText('Your vote was saved.');
  await audit(page, 'owner-voted'); await audit(guest, 'guest-private');
  await page.getByRole('button', { name: 'Reveal votes', exact: true }).click(); await audit(page, 'revealed');
  await page.getByRole('button', { name: 'Edit configuration', exact: true }).click(); await audit(page, 'room-editor');
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  await page.getByRole('button', { name: 'New round →' }).click(); await audit(page, 'next-round-form');
  await page.getByRole('button', { name: 'Start next round', exact: true }).click(); await page.locator('.history summary').click(); await audit(page, 'history');
  const dark = await page.evaluate(() => { document.documentElement.dataset.theme = 'dark'; return true; }); expect(dark).toBeTruthy(); await audit(page, 'history-dark');
  page.once('dialog', d => d.accept()); await page.getByRole('button', { name: 'Delete room', exact: true }).click();
  await expect(guest.getByRole('alert')).toContainText('Room not found'); await audit(guest, 'deleted-room-error');
  await guestContext.close();
});

test('validation identifies duplicate fields and focuses a linked error summary', async ({ page }) => {
  await page.goto('/create');
  await page.getByLabel('Phase 1 option 1', { exact: true }).fill('duplicate');
  await page.getByLabel('Phase 1 option 2', { exact: true }).fill('duplicate');
  await page.getByRole('button', { name: 'Create room ↗' }).click();
  await expect(page.locator('.editor .error')).toBeFocused();
  await expect(page.getByLabel('Phase 1 option 1', { exact: true })).toHaveAttribute('aria-invalid', 'true');
  await page.locator('.editor .error a').first().click();
  await expect(page.getByLabel('Phase 1 option 1', { exact: true })).toBeFocused();
  await audit(page, 'validation-errors');
  await page.getByLabel('Phase 1 option 1', { exact: true }).fill('unique');
  await page.getByRole('button', { name: 'Create room ↗' }).click();
  await expect(page.getByRole('heading', { name: 'Your room is ready.' })).toBeFocused();
  await page.getByRole('link', { name: 'Enter your room →' }).click();
  page.once('dialog', d => d.accept()); await page.getByRole('button', { name: 'Delete room', exact: true }).click();
});
