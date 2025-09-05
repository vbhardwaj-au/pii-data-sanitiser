import { test, expect } from '@playwright/test';

test.describe('Pipeline Execution History UI Tests', () => {
  test('should display execution records in the UI', async ({ page }) => {
    // Navigate to the execution history page
    await page.goto('http://localhost:7002/Pipeline/ExecutionHistory');
    
    // Wait for the page to load
    await page.waitForLoadState('networkidle');
    
    // Check if the table exists
    const table = await page.locator('table').first();
    await expect(table).toBeVisible();
    
    // Get all rows in the table body
    const rows = await page.locator('tbody tr').all();
    
    // Log the number of rows found
    console.log(`Found ${rows.length} execution records in the UI`);
    
    // Verify we have some execution records
    expect(rows.length).toBeGreaterThan(0);
    
    // Check the first row has expected columns
    if (rows.length > 0) {
      const firstRow = rows[0];
      
      // Check for expected columns in the first row
      const cells = await firstRow.locator('td').all();
      console.log(`First row has ${cells.length} columns`);
      
      // Log the content of first row for debugging
      for (let i = 0; i < cells.length; i++) {
        const text = await cells[i].textContent();
        console.log(`Column ${i}: ${text}`);
      }
    }
    
    // Take a screenshot for verification
    await page.screenshot({ path: 'execution-history-page.png', fullPage: true });
    console.log('Screenshot saved as execution-history-page.png');
  });

  test('should display data in Dashboard page', async ({ page }) => {
    // Navigate to the dashboard
    await page.goto('http://localhost:7002/Pipeline/Dashboard');
    
    // Wait for the page to load
    await page.waitForLoadState('networkidle');
    
    // Check for dashboard content
    const dashboardContent = await page.locator('.container').first();
    await expect(dashboardContent).toBeVisible();
    
    // Take a screenshot
    await page.screenshot({ path: 'dashboard-page.png', fullPage: true });
    console.log('Screenshot saved as dashboard-page.png');
  });

  test('should display data in Monitor page', async ({ page }) => {
    // Navigate to the monitor page
    await page.goto('http://localhost:7002/Pipeline/Monitor');
    
    // Wait for the page to load
    await page.waitForLoadState('networkidle');
    
    // Check for monitor content
    const monitorContent = await page.locator('.container').first();
    await expect(monitorContent).toBeVisible();
    
    // Take a screenshot
    await page.screenshot({ path: 'monitor-page.png', fullPage: true });
    console.log('Screenshot saved as monitor-page.png');
  });
});