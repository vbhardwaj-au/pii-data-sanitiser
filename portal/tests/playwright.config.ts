import { defineConfig, devices } from '@playwright/test';

/**
 * @see https://playwright.dev/docs/test-configuration
 */
export default defineConfig({
  testDir: './tests',
  /* Run tests in files in parallel */
  fullyParallel: true,
  /* Fail the build on CI if you accidentally left test.only in the source code. */
  forbidOnly: !!process.env.CI,
  /* Retry on CI only */
  retries: process.env.CI ? 2 : 0,
  /* Opt out of parallel tests on CI. */
  workers: process.env.CI ? 1 : undefined,
  /* Reporter to use. See https://playwright.dev/docs/test-reporters */
  reporter: 'html',
  /* Shared settings for all the projects below. See https://playwright.dev/docs/api/class-testoptions. */
  use: {
    /* Base URL to use in actions like `await page.goto('/')`. */
    baseURL: 'http://localhost:7002',
    /* Collect trace when retrying the failed test. See https://playwright.dev/docs/trace-viewer */
    trace: 'on-first-retry',
    /* Screenshot on failure */
    screenshot: 'only-on-failure',
    /* Video recording */
    video: 'retain-on-failure'
  },

  /* Configure projects for major browsers */
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
    /* Test against mobile viewports. */
    {
      name: 'Mobile Chrome',
      use: { ...devices['Pixel 5'] },
    },
    {
      name: 'Mobile Safari',
      use: { ...devices['iPhone 12'] },
    },
    /* API Testing Configuration */
    {
      name: 'API Tests',
      testMatch: '**/api/**/*.spec.ts',
      use: {
        baseURL: 'http://localhost:7001',
        extraHTTPHeaders: {
          'Accept': 'application/json',
          'Content-Type': 'application/json'
        }
      }
    },
    /* Database Testing Configuration */
    {
      name: 'Database Tests',
      testMatch: '**/database/**/*.spec.ts',
      use: {
        baseURL: 'http://localhost:7001',
        extraHTTPHeaders: {
          'Accept': 'application/json',
          'Content-Type': 'application/json'
        }
      }
    },
    /* Security Testing Configuration */
    {
      name: 'Security Tests',
      testMatch: '**/security/**/*.spec.ts',
      use: {
        baseURL: 'http://localhost:7001',
        extraHTTPHeaders: {
          'Accept': 'application/json',
          'Content-Type': 'application/json'
        }
      }
    }
  ],

  /* Run your local dev server before starting the tests */
  webServer: [
    {
      command: 'cd ../api && dotnet run',
      port: 7001,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development'
      }
    },
    {
      command: 'cd ../web && dotnet run',
      port: 7002,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development'
      }
    }
  ]
});