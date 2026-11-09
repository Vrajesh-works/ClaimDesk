import { defineConfig, devices } from '@playwright/test';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

// The E2E run boots its own API on a throwaway SQLite database (seeded demo users) and the
// Angular dev server, so `npm run e2e` needs nothing else running. Set E2E_BASE_URL to point
// the tests at an already running stack instead (for example `docker compose up`).
const external = process.env['E2E_BASE_URL'];
const dataDir = mkdtempSync(join(tmpdir(), 'claimdesk-e2e-'));

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? [['list'], ['html', { open: 'never' }]] : 'list',
  timeout: 60_000,
  use: {
    baseURL: external ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        // Optional: use a locally installed Chromium instead of the Playwright download.
        launchOptions: process.env['PW_CHROMIUM_PATH'] ? { executablePath: process.env['PW_CHROMIUM_PATH'] } : {},
      },
    },
  ],
  webServer: external
    ? undefined
    : [
        {
          command: 'dotnet run --project ../src/ClaimDesk.Api --no-launch-profile',
          url: 'http://localhost:5080/health/ready',
          timeout: 180_000,
          reuseExistingServer: !process.env['CI'],
          env: {
            ASPNETCORE_ENVIRONMENT: 'Development',
            ASPNETCORE_URLS: 'http://localhost:5080',
            ConnectionStrings__ClaimDesk: `Data Source=${join(dataDir, 'claimdesk.db')}`,
            EvidenceStorage__RootPath: join(dataDir, 'evidence'),
            RateLimiting__LoginPermitsPerMinute: '100',
            Sla__Enabled: 'false',
          },
        },
        {
          command: 'npx ng serve --port 4200',
          url: 'http://localhost:4200',
          timeout: 180_000,
          reuseExistingServer: !process.env['CI'],
        },
      ],
});
