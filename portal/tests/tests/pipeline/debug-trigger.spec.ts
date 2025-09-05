import { test, expect } from '@playwright/test';

test.describe('Debug Pipeline Trigger', () => {
  test('capture console errors', async ({ page }) => {
    // Collect console messages
    const consoleMessages: string[] = [];
    page.on('console', msg => {
      consoleMessages.push(`${msg.type()}: ${msg.text()}`);
    });
    
    // Collect page errors
    page.on('pageerror', error => {
      consoleMessages.push(`PAGE ERROR: ${error.message}`);
    });
    
    // Navigate to page
    await page.goto('http://localhost:7002/Agents/ProductAssignments');
    await page.waitForTimeout(1000);
    
    // Intercept the API request
    page.on('response', response => {
      if (response.url().includes('/api/')) {
        console.log(`API Call: ${response.url()} - Status: ${response.status()}`);
      }
    });
    
    // Try to click Run Test Obfuscation
    await page.evaluate(() => {
      console.log('About to call runTestObfuscation function');
      try {
        (window as any).runTestObfuscation();
      } catch (e: any) {
        console.error('Error calling runTestObfuscation:', e.message);
      }
    });
    
    await page.waitForTimeout(3000);
    
    // Print all console messages
    console.log('\n=== Console Messages ===');
    consoleMessages.forEach(msg => console.log(msg));
    
    // Check the test status
    const testStatus = await page.locator('#testStatus').textContent();
    console.log('\nTest Status:', testStatus);
    
    // Take screenshot
    await page.screenshot({ path: 'debug-trigger.png', fullPage: true });
  });
});