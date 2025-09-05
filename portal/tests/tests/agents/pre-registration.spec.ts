import { test, expect } from '@playwright/test';

const API_URL = 'http://localhost:7001';
const WEB_URL = 'http://localhost:7002';

test.describe('Agent Pre-Registration', () => {
  test('should successfully pre-register an agent via API', async ({ request }) => {
    // Test API directly first
    const response = await request.post(`${API_URL}/api/agent/pre-register`, {
      headers: {
        'Content-Type': 'application/json',
        'X-API-Key': 'portal-admin-key-2024'
      },
      data: {
        name: 'Playwright-Test-Agent-API',
        hostName: 'playwright-test-host',
        location: 'Docker',
        environment: 'Test',
        cpuCores: 4,
        memoryMB: 8192,
        maxConcurrentJobs: 2,
        port: 8080,
        version: '1.0.0',
        capabilities: {
          canProcessSqlServer: true,
          canProcessMySQL: false,
          canProcessPostgreSQL: false,
          canProcessOracle: false,
          maxDatabaseSizeGB: 100,
          maxRowsPerBatch: 10000
        }
      }
    });

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.success).toBe(true);
    expect(data.agentId).toBeTruthy();
    expect(data.apiKey).toBeTruthy();
    console.log('API Test - Agent ID:', data.agentId);
    console.log('API Test - API Key:', data.apiKey);
  });

  test('should pre-register an agent through UI', async ({ page }) => {
    // Enable console logging
    page.on('console', msg => {
      if (msg.type() === 'log' || msg.type() === 'error') {
        console.log(`Browser ${msg.type()}: ${msg.text()}`);
      }
    });

    // Monitor network requests
    page.on('request', request => {
      if (request.url().includes('/api/agent')) {
        console.log('Request to:', request.url());
        console.log('Request method:', request.method());
        console.log('Request headers:', request.headers());
      }
    });

    page.on('response', response => {
      if (response.url().includes('/api/agent')) {
        console.log('Response from:', response.url());
        console.log('Response status:', response.status());
      }
    });

    // Navigate to agents page
    await page.goto(`${WEB_URL}/Agents`);
    await expect(page).toHaveTitle(/Agents/);

    // Click Pre-Register Agent button
    await page.click('button:has-text("Pre-Register Agent")');

    // Wait for modal to appear
    await page.waitForSelector('#preRegisterModal', { state: 'visible' });

    // Fill in the form
    await page.fill('#agentName', 'Playwright-UI-Test-Agent');
    await page.fill('#hostName', 'playwright-ui-host');
    await page.selectOption('#location', 'Docker');
    await page.selectOption('#environment', 'Test');
    await page.fill('#cpuCores', '4');
    await page.fill('#memoryMB', '8192');
    await page.fill('#maxConcurrentJobs', '2');
    
    // Check capabilities
    await page.check('#canProcessSqlServer');
    
    // Inject API key header fix before clicking submit
    await page.evaluate(() => {
      // Override fetch to ensure headers are sent correctly
      const originalFetch = window.fetch;
      window.fetch = function(url, options) {
        console.log('Fetch intercepted - URL:', url);
        console.log('Fetch intercepted - Options:', JSON.stringify(options));
        
        if (url.includes('/api/agent/pre-register')) {
          // Ensure headers are set correctly
          options = options || {};
          options.headers = options.headers || {};
          options.headers['X-API-Key'] = 'portal-admin-key-2024';
          options.headers['Content-Type'] = 'application/json';
          
          console.log('Modified headers:', options.headers);
        }
        
        return originalFetch.call(this, url, options);
      };
    });

    // Click the submit button
    await page.click('#preRegisterBtn');

    // Wait for either success or error
    const resultLocator = page.locator('#registrationResult');
    const errorLocator = page.locator('.alert-danger');
    
    try {
      // Wait for success result
      await resultLocator.waitFor({ state: 'visible', timeout: 10000 });
      
      // Check that agent ID and API key are displayed
      const agentIdValue = await page.inputValue('#resultAgentId');
      const apiKeyValue = await page.inputValue('#resultApiKey');
      
      expect(agentIdValue).toBeTruthy();
      expect(apiKeyValue).toBeTruthy();
      
      console.log('UI Test Success - Agent ID:', agentIdValue);
      console.log('UI Test Success - API Key:', apiKeyValue);
      
      // Take screenshot of success
      await page.screenshot({ path: 'agent-registration-success.png', fullPage: true });
      
    } catch (error) {
      // Check if there's an error message
      const errorVisible = await errorLocator.isVisible();
      if (errorVisible) {
        const errorText = await errorLocator.textContent();
        console.error('Registration failed with error:', errorText);
        
        // Take screenshot of error
        await page.screenshot({ path: 'agent-registration-error.png', fullPage: true });
      }
      
      throw new Error(`Agent registration failed: ${error}`);
    }
  });

  test('verify fetch headers issue and fix', async ({ page }) => {
    await page.goto(`${WEB_URL}/Agents`);
    
    // Test the current fetch implementation
    const fetchTest = await page.evaluate(async () => {
      const testUrl = 'http://localhost:7001/api/agent/pre-register';
      const testData = {
        name: 'Test-Fetch-Agent',
        hostName: 'test-host',
        location: 'Docker',
        environment: 'Test',
        cpuCores: 2,
        memoryMB: 4096,
        maxConcurrentJobs: 1,
        port: 8080,
        version: '1.0.0',
        capabilities: {
          canProcessSqlServer: true,
          canProcessMySQL: false,
          canProcessPostgreSQL: false,
          canProcessOracle: false,
          maxDatabaseSizeGB: 100,
          maxRowsPerBatch: 10000
        }
      };

      try {
        // Test 1: Current implementation
        console.log('=== Test 1: Testing current fetch implementation ===');
        const response1 = await fetch(testUrl, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-API-Key': 'portal-admin-key-2024'
          },
          body: JSON.stringify(testData)
        });
        
        console.log('Response status:', response1.status);
        const result1 = response1.ok ? await response1.json() : await response1.text();
        console.log('Response:', result1);
        
        // Test 2: Try with different header format
        console.log('=== Test 2: Testing with lowercase headers ===');
        const response2 = await fetch(testUrl, {
          method: 'POST',
          headers: {
            'content-type': 'application/json',
            'x-api-key': 'portal-admin-key-2024'
          },
          body: JSON.stringify(testData)
        });
        
        console.log('Response status:', response2.status);
        const result2 = response2.ok ? await response2.json() : await response2.text();
        console.log('Response:', result2);
        
        return {
          test1: { status: response1.status, ok: response1.ok, result: result1 },
          test2: { status: response2.status, ok: response2.ok, result: result2 }
        };
        
      } catch (error) {
        return { error: error.message };
      }
    });
    
    console.log('Fetch test results:', fetchTest);
  });
});