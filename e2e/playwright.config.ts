// The end-to-end test of the learning flow (#39). `npm test` from e2e/, with the compose PostgreSQL up
// (scripts/dev-up.sh) and nothing else on ports 5173, 5199 and 5299. Playwright starts three servers:
// the fake GitHub (fake-provider.mjs); the API (start-api.mjs, which first creates and migrates the run's
// database); and the production build of the pages, served by `vite preview`.
import { resolve } from 'node:path';
import { defineConfig, devices } from '@playwright/test';
import { apiUrl, pagesUrl, providerUrl, root } from './stack.mjs';

const ci = Boolean(process.env.CI);

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.ts',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  forbidOnly: ci,
  retries: 0,
  workers: 1,
  reporter: ci ? [['list'], ['github'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: pagesUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'node fake-provider.mjs',
      url: `${providerUrl}/health`,
      reuseExistingServer: false,
    },
    {
      command: 'node start-api.mjs',
      // Not /health/ready: content is seeded after the host starts, and the run needs the task.
      url: `${apiUrl}/api/v1/tasks/flower-shop-daily-revenue`,
      reuseExistingServer: false,
      timeout: 240_000,
    },
    {
      command: 'npm run build && npx vite preview --port 5173 --strictPort',
      cwd: resolve(root, 'frontend'),
      url: pagesUrl,
      reuseExistingServer: false,
      timeout: 180_000,
      env: {
        VITE_API_BASE_URL: `${apiUrl}/api/v1`,
        VITE_UMAMI_SCRIPT_URL: '',
        VITE_UMAMI_WEBSITE_ID: '',
      },
    },
  ],
});
