import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration.
 *
 * LoanTracker_Stack_Rules.md [E2E_FRAMEWORK] requires headed, single-worker and
 * slowed **via this config**, not via CLI flags — so a Runtime Validation
 * watch-run is reproducible from the committed configuration rather than from
 * whatever someone typed.
 *
 * Both dev servers must already be running (`dotnet run` + `ng serve`). That is
 * deliberate: Runtime Validation drives the app the way a real user starts it,
 * not a test-managed harness, so `webServer` is intentionally not configured.
 */
export default defineConfig({
  testDir: './e2e',
  outputDir: './test-results',

  // Tests self-isolate but share one database, so they run in process/flow order
  // rather than in parallel.
  fullyParallel: false,
  workers: 1,
  retries: 0,

  // A failing test is investigated, never re-run until green. Flaky cases are
  // quarantined with a `flaky` marker plus a requirements-folder item.
  forbidOnly: true,

  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],

  use: {
    baseURL: 'http://localhost:4200',
    headless: false,
    launchOptions: {
      slowMo: 250,
    },
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    trace: 'retain-on-failure',
    actionTimeout: 15_000,
navigationTimeout: 30_000,
  },

  projects: [
    {
      name: 'chromium-desktop',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      // A floor, not a full compatibility matrix — one mobile viewport per
      // LoanTracker_Stack_Rules.md [TEST_STACK_RULES].
      name: 'mobile-chrome',
      use: { ...devices['Pixel 5'] },
    },
  ],

  expect: {
    timeout: 10_000,
  },
});
