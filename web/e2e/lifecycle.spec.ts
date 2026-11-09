import { expect, test, type Page } from '@playwright/test';

const PASSWORD = 'ClaimDesk!2026';
const USERS = {
  claimant: 'claimant@claimdesk.local',
  otherClaimant: 'claimant2@claimdesk.local',
  adjuster: 'adjuster@claimdesk.local',
  admin: 'admin@claimdesk.local',
} as const;

// 1x1 transparent PNG. The API checks the PNG file signature, not just the extension.
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
);

async function login(page: Page, email: string): Promise<void> {
  await page.goto('/login');
  await page.getByTestId('login-email').fill(email);
  await page.getByTestId('login-password').fill(PASSWORD);
  await page.getByTestId('login-submit').click();
  await expect(page.getByTestId('current-user')).toBeVisible();
}

async function logout(page: Page): Promise<void> {
  await page.getByTestId('logout').click();
  await expect(page).toHaveURL(/\/login$/);
}

async function act(page: Page, action: string, note?: string): Promise<void> {
  await page.getByTestId(`action-${action}`).click();
  if (note) {
    await page.getByTestId('action-note').fill(note);
  }
  await page.getByTestId('confirm-action').click();
}

test.describe.configure({ mode: 'serial' });

let claimUrl = '';
let claimNumber = '';
const description = `Rear-ended at a stop light on Main St (e2e ${Date.now()}).`;

test('claimant files a claim with evidence', async ({ page }) => {
  await login(page, USERS.claimant);
  await page.getByTestId('new-claim').click();

  // Reactive form validation blocks an empty submit.
  await page.getByTestId('submit-claim').click();
  await expect(page.getByTestId('form-error')).toHaveText('Fix the highlighted fields.');

  await page.getByTestId('policyNumber').fill('POL-E2E-1001');
  await page.getByTestId('incidentDate').fill(new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10));
  await page.getByTestId('claimedAmount').fill('4200');
  await page.getByTestId('description').fill(description);
  await page.getByTestId('make').fill('Honda');
  await page.getByTestId('category').selectOption('Sedan');
  await page.getByTestId('evidence-input').setInputFiles({ name: 'bumper.png', mimeType: 'image/png', buffer: PNG });
  await page.getByTestId('submit-claim').click();

  await expect(page).toHaveURL(/\/claims\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId('status-badge')).toHaveText('Submitted');
  await expect(page.getByTestId('evidence-list')).toContainText('bumper.png');
  claimUrl = new URL(page.url()).pathname;
  claimNumber = (await page.getByTestId('claim-number').textContent())!.trim();
  expect(claimNumber).toMatch(/^CLM-/);

  // A claimant has no review actions on their own submitted claim.
  await expect(page.getByTestId('no-actions')).toBeVisible();
});

test('evidence with a disallowed type is rejected', async ({ page }) => {
  await login(page, USERS.claimant);
  await page.goto(claimUrl);
  await page.getByTestId('evidence-upload').setInputFiles({ name: 'notes.txt', mimeType: 'text/plain', buffer: Buffer.from('hello') });
  await expect(page.getByTestId('upload-error')).toHaveText('Only PDF, PNG and JPEG files are accepted.');

  // Renamed file: passes the client extension check, rejected by the API signature check.
  await page.getByTestId('evidence-upload').setInputFiles({ name: 'fake.png', mimeType: 'image/png', buffer: Buffer.from('not a png') });
  await expect(page.getByTestId('upload-error')).toHaveText('File content does not match its extension.');
});

test('another claimant cannot open the claim and a claimant cannot reach the queue', async ({ page }) => {
  await login(page, USERS.otherClaimant);
  await page.goto(claimUrl);
  await expect(page.getByTestId('load-error')).toBeVisible();

  await page.goto('/queue');
  await expect(page).toHaveURL(/\/claims$/);
  await expect(page.getByTestId('nav-queue')).toHaveCount(0);
});

test('adjuster reviews from the risk queue and requests more info', async ({ page }) => {
  await login(page, USERS.adjuster);
  await expect(page).toHaveURL(/\/queue$/);
  await page.locator(`tr[data-claim-number="${claimNumber}"] a`).click();
  await expect(page.getByTestId('claim-number')).toHaveText(claimNumber);

  await act(page, 'StartReview');
  await expect(page.getByTestId('status-badge')).toHaveText('In review');

  // A note is required to request info.
  await page.getByTestId('action-RequestInfo').click();
  await page.getByTestId('confirm-action').click();
  await expect(page.getByText('A note is required for this action.')).toBeVisible();
  await page.getByTestId('action-note').fill('Please upload the repair estimate.');
  await page.getByTestId('confirm-action').click();
  await expect(page.getByTestId('status-badge')).toHaveText('More info needed');
});

test('claimant provides the requested info', async ({ page }) => {
  await login(page, USERS.claimant);
  await page.goto(claimUrl);
  await page.getByTestId('evidence-upload').setInputFiles({ name: 'estimate.png', mimeType: 'image/png', buffer: PNG });
  await expect(page.getByTestId('evidence-list')).toContainText('estimate.png');
  await act(page, 'ProvideInfo', 'Estimate from the body shop is attached.');
  await expect(page.getByTestId('status-badge')).toHaveText('In review');
});

test('adjuster approves and cannot mark paid', async ({ page }) => {
  await login(page, USERS.adjuster);
  await page.goto(claimUrl);
  await page.getByTestId('action-Approve').click();
  await page.getByTestId('approved-amount-input').fill('3900');
  await page.getByTestId('confirm-action').click();
  await expect(page.getByTestId('status-badge')).toHaveText('Approved');
  await expect(page.getByTestId('approved-amount')).toHaveText('$3,900.00');
  await expect(page.getByTestId('action-MarkPaid')).toHaveCount(0);
});

test('admin marks the claim paid and the audit history shows every step', async ({ page }) => {
  await login(page, USERS.admin);
  await page.goto(claimUrl);
  await act(page, 'MarkPaid');
  await expect(page.getByTestId('status-badge')).toHaveText('Paid');
  await expect(page.getByTestId('no-actions')).toBeVisible();

  const history = page.getByTestId('history');
  await expect(history.locator('li[data-event="Submitted"]')).toHaveCount(1);
  for (const status of ['InReview', 'MoreInfoNeeded', 'Approved', 'Paid']) {
    await expect(history.locator(`li[data-event="StatusChanged"][data-to="${status}"]`).first()).toBeVisible();
  }
  await expect(history.locator('li[data-event="StatusChanged"]')).toHaveCount(5);
  await expect(history).toContainText('Please upload the repair estimate.');
  await expect(history).toContainText('Estimate from the body shop is attached.');
  await expect(history.locator('li[data-event="EvidenceAdded"]')).toHaveCount(2);
  await logout(page);
});
