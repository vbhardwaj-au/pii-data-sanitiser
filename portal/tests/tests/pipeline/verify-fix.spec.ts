import { test, expect } from '@playwright/test';

test.describe('Verify Pipeline Trigger Fix', () => {
  test('verify the page has updated JavaScript', async ({ page }) => {
    // Go to the page
    await page.goto('http://localhost:7002/Agents/ProductAssignments');
    
    // Get the page source to check if it has the correct endpoint
    const pageContent = await page.content();
    
    // Check if the old endpoint exists
    const hasOldEndpoint = pageContent.includes('/api/pipeline/trigger');
    const hasNewEndpoint = pageContent.includes('/api/PipelineExecution/start');
    
    console.log('Page has old endpoint (/api/pipeline/trigger):', hasOldEndpoint);
    console.log('Page has new endpoint (/api/PipelineExecution/start):', hasNewEndpoint);
    
    // The page should have the new endpoint and not the old one
    if (hasOldEndpoint && !hasNewEndpoint) {
      console.error('ERROR: Page still has old JavaScript! Browser cache needs to be cleared.');
      console.log('\nTo fix this:');
      console.log('1. Clear browser cache for localhost:7002');
      console.log('2. Do a hard refresh: Cmd+Shift+R');
      console.log('3. Or restart the browser');
    }
    
    // Check the actual function in the page
    const functionCode = await page.evaluate(() => {
      return typeof (window as any).runTestObfuscation === 'function' 
        ? (window as any).runTestObfuscation.toString() 
        : 'Function not found';
    });
    
    console.log('\n=== runTestObfuscation function ===');
    console.log(functionCode.substring(0, 500) + '...');
    
    // Verify the function uses the correct endpoint
    if (functionCode.includes('/api/pipeline/trigger')) {
      console.error('\nERROR: The runTestObfuscation function still uses the OLD endpoint!');
    }
    if (functionCode.includes('/api/PipelineExecution/start')) {
      console.log('\n✓ The runTestObfuscation function uses the CORRECT endpoint!');
    }
  });
  
  test('test with cache disabled', async ({ browser }) => {
    // Create a new context with cache disabled
    const context = await browser.newContext({
      bypassCSP: true,
      ignoreHTTPSErrors: true,
      extraHTTPHeaders: {
        'Cache-Control': 'no-cache',
        'Pragma': 'no-cache'
      }
    });
    
    const page = await context.newPage();
    
    // Clear any session storage/local storage
    await page.addInitScript(() => {
      window.localStorage.clear();
      window.sessionStorage.clear();
    });
    
    // Navigate with cache disabled
    await page.goto('http://localhost:7002/Agents/ProductAssignments', {
      waitUntil: 'networkidle'
    });
    
    // Wait for the page to fully load
    await page.waitForSelector('.pipeline-trigger-card');
    
    // Intercept network requests to see what's being called
    const apiCalls: string[] = [];
    page.on('response', response => {
      if (response.url().includes('/api/')) {
        apiCalls.push(`${response.url()} - ${response.status()}`);
      }
    });
    
    // Click the test button
    await page.click('button:has-text("Run Test Obfuscation")');
    
    // Wait for API call
    await page.waitForTimeout(2000);
    
    // Check what API was called
    console.log('\n=== API Calls Made ===');
    apiCalls.forEach(call => console.log(call));
    
    // Check the result
    const testStatus = await page.locator('#testStatus').textContent();
    console.log('\nTest Status:', testStatus);
    
    // Take screenshot
    await page.screenshot({ path: 'cache-disabled-test.png', fullPage: true });
    
    await context.close();
  });
  
  test('force reload and test', async ({ page }) => {
    // Navigate with forced reload
    await page.goto('http://localhost:7002/Agents/ProductAssignments', {
      waitUntil: 'networkidle'
    });
    
    // Force reload the page
    await page.reload({ waitUntil: 'networkidle' });
    
    // Wait for elements
    await page.waitForSelector('.pipeline-trigger-card');
    
    // Monitor API calls
    let apiEndpoint = '';
    page.on('response', response => {
      if (response.url().includes('/api/PipelineExecution') || response.url().includes('/api/pipeline')) {
        apiEndpoint = response.url();
        console.log(`API Called: ${response.url()} - Status: ${response.status()}`);
      }
    });
    
    // Try triggering the pipeline
    await page.selectOption('#triggerProduct', 'b1808c08-39e0-47b0-bd5e-40fee9a85b4b');
    await page.click('button:has-text("Trigger Pipeline")');
    
    await page.waitForTimeout(2000);
    
    // Check if correct endpoint was used
    if (apiEndpoint.includes('/api/PipelineExecution/start')) {
      console.log('✓ Correct endpoint used for trigger!');
    } else if (apiEndpoint.includes('/api/pipeline/trigger')) {
      console.log('✗ Old endpoint still being used!');
    }
    
    const triggerVisible = await page.locator('#triggerResult').isVisible();
    console.log('Trigger result visible:', triggerVisible);
  });
});