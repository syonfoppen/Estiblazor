const { test, expect } = require('@playwright/test');
const { execFile } = require('node:child_process');
const { promisify } = require('node:util');
const compose = (...args) => promisify(execFile)('docker', ['compose', ...args], { cwd: require('node:path').resolve(__dirname, '../..'), timeout: 60000 });
async function create(page, name = 'Sprint room') {
  await page.goto('/create');
  await page.getByLabel('Room name', { exact: true }).fill(name);
  await page.getByRole('button', { name: 'Create room ↗' }).click();
  await expect(page.getByRole('heading', { name: 'Your room is ready.' })).toBeVisible();
  const invite = await page.getByLabel('Invitation link', { exact: true }).inputValue();
  const management = await page.getByLabel('Secret management link').inputValue();
  await page.getByRole('link', { name: 'Enter your room →' }).click();
  await expect(page.getByText('You own this room', { exact: true })).toBeVisible();
  return { invite, management };
}
async function join(page, name = 'Alex') {
  await page.getByLabel('Display name', { exact: true }).fill(name);
  await page.getByRole('button', { name: 'Join the room →' }).click();
  await expect(page.getByRole('heading', { name: 'Take a seat.' })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'What do you think?', exact: true })).toBeFocused();
}
async function remove(page) {
  const confirmation = page.waitForEvent('dialog');
  const click = page.getByRole('button', { name: 'Delete room', exact: true }).click();
  const dialog = await confirmation;
  await dialog.accept(); await click;
  await expect(page).toHaveURL(new URL('/', page.url()).href);
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Great work starts');
}
const effort = page => page.getByRole('group', { name: 'Effort voting cards', exact: true });

test('owner, same-name guests, tabs, recovery, immutable history and deletion', async ({ browser, page }) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Great work starts');
  await page.screenshot({ path: 'test-results/home-desktop.png', fullPage: true });
  await page.getByLabel('Room code or invitation link').fill('000000000000');
  await page.getByRole('button', { name: 'Join room →' }).click();
  await expect(page.getByRole('alert')).toContainText('Room not found');
  const { invite, management } = await create(page);
  await join(page);
  const aContext = await browser.newContext(); const bContext = await browser.newContext();
  const a = await aContext.newPage(); const b = await bContext.newPage();
  await a.goto(invite); await join(a); await b.goto(invite); await join(b);
  await expect(page.locator('.person')).toHaveCount(3);
  await expect(a.getByRole('button', { name: 'Reveal votes', exact: true })).toHaveCount(0);
  await effort(a).getByRole('button', { name: '5', exact: true }).click();
  await effort(b).getByRole('button', { name: '8', exact: true }).click();
  await expect(page.locator('.person .badge').filter({ hasText: '1/2' })).toHaveCount(2);
  await expect(page.locator('.vote-card[aria-pressed="true"]')).toHaveCount(0);
  await effort(page).getByRole('button', { name: '3', exact: true }).click();
  await effort(page).getByRole('button', { name: '3', exact: true }).click();
  await expect(page.locator('.vote-card[aria-pressed="true"]')).toHaveCount(0);
  await effort(page).getByRole('button', { name: '2', exact: true }).focus();
  await page.keyboard.press('Space');
  await expect(effort(page).getByRole('button', { name: '2', exact: true })).toHaveAttribute('aria-pressed', 'true');
  const secondTab = await aContext.newPage(); await secondTab.goto(invite);
  await expect(secondTab.locator('.person')).toHaveCount(3);
  await expect(effort(secondTab).getByRole('button', { name: '5', exact: true })).toHaveAttribute('aria-pressed', 'true');
  // Discard confirmation can be cancelled, keeping all votes in place.
  await page.getByRole('button', { name: 'New round →' }).click();
  page.once('dialog', d => d.dismiss()); await page.getByRole('button', { name: 'Start next round', exact: true }).click();
  await expect(effort(page).getByRole('button', { name: '2', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Reveal votes', exact: true }).click();
  await expect(a.getByText('Revealed · voting closed', { exact: true })).toBeVisible();
  await expect(a.locator('.result')).toHaveCount(3);
  await expect(effort(a).getByRole('button', { name: '5', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Edit configuration', exact: true }).click();
  await page.getByLabel('Room name', { exact: true }).fill('Next sprint room');
  await page.getByRole('button', { name: 'Save for next round', exact: true }).click();
  await expect(page.getByText('Configuration saved. It takes effect in the next round.')).toBeVisible();
  await page.getByLabel('Next round title (optional)').fill('Story two');
  await page.getByRole('button', { name: 'Start next round', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Next sprint room', exact: true })).toBeVisible();
  await expect(page.locator('.vote-card[aria-pressed="true"]')).toHaveCount(0);
  await expect(page.locator('.history details')).toHaveCount(1);
  await page.locator('.history summary').click();
  await expect(page.locator('.history details')).toContainText('Sprint room');
  await expect(page.locator('.history-vote')).toHaveCount(6);
  await page.screenshot({ path: 'test-results/room-desktop.png', fullPage: true });
  const recoveryContext = await browser.newContext(); const recovery = await recoveryContext.newPage();
  await recovery.goto(management);
  await expect(recovery).toHaveURL(invite);
  await expect(recovery.getByText('You own this room', { exact: true })).toBeVisible();
  const cookie = (await recoveryContext.cookies()).find(c => c.name === 'estiblazor.browser');
  expect(cookie.httpOnly).toBeTruthy(); expect(cookie.sameSite).toBe('Lax');
  await recovery.reload(); await expect(recovery.getByText('You own this room', { exact: true })).toBeVisible();
  await aContext.setOffline(true); await aContext.setOffline(false); await a.reload();
  await expect(a.getByRole('heading', { name: 'Next sprint room', exact: true })).toBeVisible();
  await secondTab.close();
  await expect(page.locator('.person .online')).toHaveCount(3);
  await aContext.close();
  await expect(page.locator('.person .online')).toHaveCount(2, { timeout: 15000 });
  await remove(recovery);
  await expect(recovery).toHaveURL(/\/$/);
  await expect(b.getByRole('alert')).toContainText('Room not found');
  expect(errors).toEqual([]);
  await aContext.close(); await bContext.close(); await recoveryContext.close();
});

test('mobile editor safely renders custom text and supports phase and option ordering', async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
  const page = await context.newPage();
  await page.goto('/create');
  await page.getByLabel('Room name', { exact: true }).fill('Mobile room');
  await page.getByRole('combobox').selectOption({ label: 'Like / dislike' });
  await expect(page.getByLabel('Phase name', { exact: true })).toHaveCount(1);
  await page.getByRole('button', { name: '+ Add phase', exact: true }).click();
  await page.getByLabel('Phase name', { exact: true }).nth(1).fill('Custom');
  await page.getByLabel('Phase 2 option 1', { exact: true }).fill('<script>alert(1)</script>');
  await page.getByRole('button', { name: 'Move phase up', exact: true }).nth(1).click();
  await expect(page.getByLabel('Phase name', { exact: true }).first()).toHaveValue('Custom');
  await page.getByRole('button', { name: 'Move option right', exact: true }).first().click();
  await expect(page.getByLabel('Phase 1 option 2', { exact: true })).toHaveValue('<script>alert(1)</script>');
  await page.getByRole('button', { name: 'Create room ↗' }).click();
  await page.getByRole('link', { name: 'Enter your room →' }).click(); await join(page, 'Mobile');
  const custom = page.getByRole('group', { name: 'Custom voting cards' });
  await expect(custom.getByRole('button', { name: '<script>alert(1)</script>', exact: true })).toBeVisible();
  expect(await custom.locator('script').count()).toBe(0);
  await custom.getByRole('button', { name: 'No', exact: true }).click();
  await expect(custom.getByRole('button', { name: 'No', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Toggle light or dark theme' }).click();
  await page.screenshot({ path: 'test-results/room-mobile.png', fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await remove(page);
  await context.close();
});

test('app and Redis restart preserve owner, configuration, current votes and history; outage is recoverable', async ({ page, request }) => {
  test.setTimeout(180000);
  const { invite } = await create(page, 'Persistent sprint'); await join(page);
  await effort(page).getByRole('button', { name: '5', exact: true }).click();
  await page.getByRole('button', { name: 'Reveal votes', exact: true }).click();
  await page.getByRole('button', { name: 'New round →' }).click();
  await page.getByLabel('Next round title (optional)').fill('Saved round');
  await page.getByRole('button', { name: 'Start next round', exact: true }).click();
  await effort(page).getByRole('button', { name: '8', exact: true }).click();
  await expect(effort(page).getByRole('button', { name: '8', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await compose('restart', 'app', 'redis');
  await expect.poll(async () => { try { return (await request.get('/health')).status(); } catch { return 0; } }, { timeout: 45000 }).toBe(200);
  await page.goto(invite);
  await expect(page.getByText('You own this room', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Persistent sprint', exact: true })).toBeVisible();
  await expect(page.getByText('Saved round', { exact: true })).toBeVisible();
  await expect(effort(page).getByRole('button', { name: '8', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.history details')).toHaveCount(1);
  await page.locator('.history summary').click(); await expect(page.locator('.history details')).toContainText('5');
  try {
    await compose('stop', 'redis');
    await effort(page).getByRole('button', { name: '13', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('temporarily unavailable', { timeout: 15000 });
    await expect(effort(page).getByRole('button', { name: '13', exact: true })).toHaveAttribute('aria-pressed', 'false');
  } finally { await compose('start', 'redis'); }
  await expect.poll(async () => { try { return (await request.get('/health')).status(); } catch { return 0; } }, { timeout: 30000 }).toBe(200);
  await page.getByRole('button', { name: 'Retry / refresh' }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await effort(page).getByRole('button', { name: '13', exact: true }).click();
  await expect(effort(page).getByRole('button', { name: '13', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await remove(page);
});

test('template catalog creates independent drafts and supports an effort-and-risk room', async ({ page }) => {
  await page.goto('/create');
  await page.getByLabel('Room name', { exact: true }).fill('Template room');
  const templates = page.getByRole('combobox', { name: 'Start from a template' });
  await expect(templates.getByRole('option')).toHaveCount(13); // Placeholder and 12 built-ins.
  await templates.selectOption({ label: 'T-shirt sizes' });
  await expect(page.getByLabel('Room name', { exact: true })).toHaveValue('Template room');
  // The server round trip can leave the previous two-phase deck in the DOM briefly.
  await expect(page.getByLabel('Phase name', { exact: true })).toHaveCount(1);
  await expect(page.getByLabel('Phase name', { exact: true })).toHaveValue('Size');
  await page.getByLabel('Phase 1 option 1', { exact: true }).fill('Tiny');
  await templates.selectOption({ label: 'Fibonacci' });
  await templates.selectOption({ label: 'T-shirt sizes' });
  await expect(page.getByLabel('Phase 1 option 1', { exact: true })).toHaveValue('XXS');
  await templates.selectOption({ label: 'Effort and risk' });
  await expect(page.getByLabel('Phase name', { exact: true })).toHaveCount(2);
  await expect(page.locator('#template-description')).toContainText('two-phase');
  await page.getByRole('button', { name: 'Create room ↗' }).click();
  await page.getByRole('link', { name: 'Enter your room →' }).click(); await join(page, 'Template tester');
  await page.getByRole('group', { name: 'Effort voting cards' }).getByRole('button', { name: 'Unsure', exact: true }).click();
  await page.getByRole('group', { name: 'Risk voting cards' }).getByRole('button', { name: 'High', exact: true }).click();
  await expect(page.locator('.person .badge')).toHaveText('2/2');
  await page.getByRole('button', { name: 'Reveal votes', exact: true }).click();
  await expect(page.locator('.result')).toHaveCount(2);
  await remove(page);
});
