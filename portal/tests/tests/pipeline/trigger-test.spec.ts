import { test, expect } from '@playwright/test';

test.describe('Pipeline Trigger Tests', () => {
  test('should trigger pipeline execution', async ({ page }) => {
    // Navigate to Product Assignments page
    await page.goto('http://localhost:7002/Agents/ProductAssignments');
    
    // Wait for page to load
    await page.waitForSelector('.pipeline-trigger-card');
    
    // Select product from dropdown
    await page.selectOption('#triggerProduct', 'b1808c08-39e0-47b0-bd5e-40fee9a85b4b');
    
    // Click trigger button
    await page.click('button:has-text("Trigger Pipeline")');
    
    // Wait for response
    await page.waitForTimeout(2000);
    
    // Check for success or error
    const triggerResult = await page.locator('#triggerResult').isVisible();
    const testStatus = await page.locator('#testStatus').textContent();
    
    console.log('Trigger Result Visible:', triggerResult);
    console.log('Test Status:', testStatus);
    
    // Take screenshot for debugging
    await page.screenshot({ path: 'pipeline-trigger-test.png', fullPage: true });
  });
  
  test('should test obfuscation', async ({ page }) => {
    // Navigate to Product Assignments page
    await page.goto('http://localhost:7002/Agents/ProductAssignments');
    
    // Wait for page to load
    await page.waitForSelector('.card:has-text("Test Obfuscation")');
    
    // Click Run Test Obfuscation button
    await page.click('button:has-text("Run Test Obfuscation")');
    
    // Wait for response
    await page.waitForTimeout(2000);
    
    // Check test results
    const testResults = await page.locator('#testResults').isVisible();
    const testStatus = await page.locator('#testStatus').textContent();
    
    console.log('Test Results Visible:', testResults);
    console.log('Test Status:', testStatus);
    
    // Take screenshot
    await page.screenshot({ path: 'test-obfuscation.png', fullPage: true });
  });
  
  test('API endpoint test - direct call', async ({ request }) => {
    // Test the API endpoint directly
    const response = await request.post('http://localhost:7001/api/PipelineExecution/start', {
      headers: {
        'Content-Type': 'application/json',
        'X-API-Key': 'portal-admin-key-2024'
      },
      data: {
        productId: 'b1808c08-39e0-47b0-bd5e-40fee9a85b4b',
        initiatedBy: 'Test User',
        isDryRun: false
      }
    });
    
    console.log('API Response Status:', response.status());
    
    if (response.ok()) {
      const json = await response.json();
      console.log('API Response:', JSON.stringify(json, null, 2));
    } else {
      console.log('API Error:', await response.text());
    }
    
    expect(response.status()).toBe(200);
  });
});