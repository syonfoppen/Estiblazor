const { test, expect } = require('@playwright/test');
const AxeBuilder = require('@axe-core/playwright').default;
async function audit(page, name) {
  const result = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice']).analyze();
  await test.info().attach(name, { body: JSON.stringify(result), contentType: 'application/json' });
  expect(result.violations.map(v => ({ id: v.id, nodes: v.nodes.map(n => ({ target: n.target, summary: n.failureSummary })) }))).toEqual([]);
}
async function create(page, name) {
  await page.goto('/create'); await page.getByLabel('Room name', { exact: true }).fill(name);
  await page.getByRole('button', { name: 'Create room ↗' }).click();
  await expect(page.getByRole('heading', { name: 'Your room is ready.' })).toBeVisible();
  const invite = await page.getByLabel('Invitation link', { exact: true }).inputValue();
  const management = await page.getByLabel('Secret management link', { exact: true }).inputValue();
  await page.getByRole('link', { name: 'Enter your room →' }).click();
  await expect(page.getByText('You own this room', { exact: true })).toBeVisible();
  return { invite, management };
}
async function join(page, name, observer = false) {
  await page.getByLabel('Display name', { exact: true }).fill(name);
  if (observer) await page.getByLabel('Join as observer (watch without voting)').check();
  await page.getByRole('button', { name: 'Join the room →' }).click();
  await expect(page.getByRole('heading', { name: 'Take a seat.' })).toHaveCount(0);
}
const effort = p => p.getByRole('group', { name: 'Effort voting cards', exact: true });
async function remove(page) {
  const confirmation = page.waitForEvent('dialog');
  const click = page.getByRole('button', { name: 'Delete room', exact: true }).click();
  const dialog = await confirmation;
  expect(dialog.message()).toContain('Permanently delete this room');
  await dialog.accept();
  await click;
  // A click only dispatches the command. Await committed deletion and the static home response.
  await expect(page).toHaveURL(new URL('/', page.url()).href);
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Great work starts');
}

test('owned and recent rooms are browser-private; duplicate copies the deck, QR shares only the invite', async ({ page, browser }) => {
  const { invite, management } = await create(page, 'Reusable custom deck'); await join(page, 'Host');
  await effort(page).getByRole('button', { name: '5', exact: true }).click();
  await page.getByRole('button', { name: 'Show QR invitation' }).click();
  const qr = page.getByRole('img', { name: /QR invitation to room/ });
  await expect(qr).toBeVisible(); expect(await qr.evaluate(e => e.complete && e.naturalWidth > 0)).toBeTruthy();
  await expect(page.getByLabel('Room invitation link', { exact: true })).toHaveValue(invite);
  expect(invite).not.toContain('secret'); expect(invite).not.toBe(management);
  await audit(page, 'qr-invitation');
  const home = await page.goto('/'); expect(home.headers()['x-robots-tag']).toContain('noindex');
  const card = page.locator('.recent-rooms li').filter({ hasText: 'Reusable custom deck' });
  await expect(card.locator('.badge')).toHaveText('Owner'); await audit(page, 'owned-room-list');
  const visitorContext = await browser.newContext(); const visitor = await visitorContext.newPage();
  await visitor.goto('/'); await expect(visitor.locator('.recent-rooms')).toHaveCount(0);
  await visitor.goto(invite); await expect(visitor.getByRole('heading', { name: 'Reusable custom deck', exact: true })).toBeVisible();
  await visitor.goto('/'); await expect(visitor.locator('.recent-rooms li .badge')).toHaveText('Visited');
  await expect(visitor.getByText('Your rooms', { exact: true })).toHaveCount(0);
  await card.getByRole('link', { name: 'Duplicate room', exact: true }).click();
  await expect(page.getByLabel('Room name', { exact: true })).toHaveValue('Reusable custom deck (copy)');
  await expect(page.getByLabel('Phase name', { exact: true })).toHaveCount(2);
  expect(await page.locator('meta[name="robots"]').getAttribute('content')).toContain('noindex');
  await page.getByRole('button', { name: 'Create room ↗' }).click();
  const copiedInvite = await page.getByLabel('Invitation link', { exact: true }).inputValue(); expect(copiedInvite).not.toBe(invite);
  await page.getByRole('link', { name: 'Enter your room →' }).click();
  await expect(page.locator('.person')).toHaveCount(0); await expect(page.locator('.history details')).toHaveCount(0);
  await expect(page.locator('.vote-card[aria-pressed="true"]')).toHaveCount(0);
  await remove(page);
  await page.goto(invite); await expect(effort(page).getByRole('button', { name: '5', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await remove(page); await visitor.goto('/'); await expect(visitor.locator('.recent-rooms')).toHaveCount(0);
  await visitorContext.close();
});

test('active phase follows the owner; observers are excluded and revealed distributions stay frozen', async ({ page, browser }) => {
  const { invite } = await create(page, 'Guided session'); await join(page, 'Host');
  const guestContext = await browser.newContext(); const watcherContext = await browser.newContext();
  const guest = await guestContext.newPage(); const watcher = await watcherContext.newPage();
  await guest.goto(invite); await join(guest, 'Alex'); await watcher.goto(invite); await join(watcher, 'Watcher', true);
  await expect(watcher.locator('.vote-card:enabled')).toHaveCount(0);
  await expect(guest.getByRole('button', { name: 'Make Complexity active', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Make Complexity active', exact: true }).click();
  await expect(guest.locator('.phase-navigation [role="status"]')).toContainText('Complexity');
  await effort(page).getByRole('button', { name: '3', exact: true }).click();
  await effort(guest).getByRole('button', { name: '8', exact: true }).click();
  await expect(effort(guest).getByRole('button', { name: '8', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await guest.getByLabel('Observe without voting').check();
  guest.once('dialog', d => d.dismiss()); await guest.getByRole('button', { name: 'Save participation', exact: true }).click();
  await expect(guest.getByText('Mode change canceled. Your votes are unchanged.', { exact: true })).toBeVisible();
  await expect(effort(guest).getByRole('button', { name: '8', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await guest.getByLabel('Observe without voting').check();
  guest.once('dialog', d => d.accept()); await guest.getByRole('button', { name: 'Save participation', exact: true }).click();
  await expect(guest.locator('.vote-card[aria-pressed="true"]')).toHaveCount(0);
  await expect(guest.locator('.vote-card:enabled')).toHaveCount(0);
  await guest.getByLabel('Observe without voting').uncheck(); await guest.getByRole('button', { name: 'Save participation', exact: true }).click();
  await effort(guest).getByRole('button', { name: '8', exact: true }).click();
  await expect(watcher.locator('.results')).toHaveCount(0); await audit(watcher, 'observer-active-phase');
  await page.getByRole('button', { name: 'Reveal votes', exact: true }).click();
  await expect(watcher.locator('.result-overview strong').first()).toHaveText('2/2 voted');
  await expect(watcher.getByText('Different cards — discuss the assumptions', { exact: true })).toBeVisible();
  await expect(watcher.locator('.result')).toHaveCount(2);
  await watcher.getByLabel('Observe without voting').uncheck(); await watcher.getByRole('button', { name: 'Save participation', exact: true }).click();
  await guest.getByLabel('Your display name', { exact: true }).fill('Later name');
  await guest.getByRole('button', { name: 'Save participation', exact: true }).click();
  await expect(page.locator('.results').first()).not.toContainText('Later name');
  await expect(page.locator('.result-overview strong').first()).toHaveText('2/2 voted');
  await audit(page, 'revealed-distribution');
  await page.getByRole('button', { name: 'New round →' }).click(); await page.getByRole('button', { name: 'Start next round', exact: true }).click();
  await expect(watcher.locator('.phase-navigation [role="status"]')).toContainText('Effort');
  await page.locator('.history summary').click(); await expect(page.locator('.history')).not.toContainText('Later name');
  await remove(page); await guestContext.close(); await watcherContext.close();
});

test('co-owners and transfer use participant identities; transfer revokes old links and the new owner can recover', async ({ page, browser }) => {
  const { invite, management } = await create(page, 'Ownership session'); await join(page, 'Host');
  const coContext = await browser.newContext(); const targetContext = await browser.newContext();
  const co = await coContext.newPage(); const target = await targetContext.newPage();
  await co.goto(invite); await join(co, 'Alex'); await target.goto(invite); await join(target, 'Alex', true);
  const coId = await co.locator('.person').filter({ hasText: 'Alex (you)' }).getAttribute('data-user-id');
  const targetId = await target.locator('.person').filter({ hasText: 'Alex (you)' }).getAttribute('data-user-id');
  expect(coId).not.toBe(targetId);
  await page.getByText('Manage ownership', { exact: true }).click();
  await page.getByLabel('Participant for ownership').selectOption(coId);
  page.once('dialog', d => d.accept()); await page.getByRole('button', { name: 'Add co-owner', exact: true }).click();
  await expect(co.getByText('You own this room', { exact: true })).toBeVisible();
  await co.getByText('Manage ownership', { exact: true }).click(); await co.getByLabel('Participant for ownership').selectOption(targetId);
  await audit(co, 'ownership-controls');
  co.once('dialog', d => d.accept()); await co.getByRole('button', { name: 'Transfer ownership', exact: true }).click();
  await expect(co.getByText('You own this room', { exact: true })).toHaveCount(0);
  await expect(page.getByText('You own this room', { exact: true })).toHaveCount(0);
  await expect(target.getByText('You own this room', { exact: true })).toBeVisible();
  await expect(target.locator('.vote-card:enabled')).toHaveCount(0);
  await page.goto(management); await expect(page.getByRole('alert')).toContainText('Invalid management link');
  await page.goto(invite); await expect(page.getByText('You own this room', { exact: true })).toHaveCount(0);
  await target.getByText('Manage ownership', { exact: true }).click();
  target.once('dialog', d => d.accept()); await target.getByRole('button', { name: 'Create new management link', exact: true }).click();
  const newLink = await target.getByLabel('New secret management link', { exact: true }).inputValue(); expect(newLink).not.toBe(management);
  await expect(page.getByLabel('New secret management link', { exact: true })).toHaveCount(0);
  const recoveryContext = await browser.newContext(); const recovery = await recoveryContext.newPage();
  await recovery.goto(newLink); await expect(recovery).toHaveURL(invite); await expect(recovery.getByText('You own this room', { exact: true })).toBeVisible();
  await recovery.goto('/'); await expect(recovery.locator('.recent-rooms li .badge')).toHaveText('Owner');
  await target.reload(); await expect(target.getByText('You own this room', { exact: true })).toBeVisible();
  await remove(target); await coContext.close(); await targetContext.close(); await recoveryContext.close();
});
