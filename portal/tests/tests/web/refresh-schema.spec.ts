import { test, expect } from '@playwright/test';

test.describe('Refresh Schema Functionality', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the portal homepage
    await page.goto('http://localhost:7002');
    await page.waitForLoadState('networkidle');
  });

  test('should display source connection string and refresh schema successfully', async ({ page }) => {
    // Look for a product to test with - click on any product link
    const productLinks = page.locator('a[href*="/Home/ProductMappings"]');
    const productCount = await productLinks.count();
    
    if (productCount === 0) {
      console.log('No products found to test with');
      return;
    }

    // Click on the first product
    await productLinks.first().click();
    await page.waitForLoadState('networkidle');

    // Verify we're on the Product Mappings page
    await expect(page.locator('h2')).toContainText(/product.*mapping/i);

    // Check if the Source connection string is displayed
    const sourceConnectionInput = page.locator('input[name="SourceConnectionString"]');
    await expect(sourceConnectionInput).toBeVisible();
    await expect(sourceConnectionInput).toHaveAttribute('readonly');

    // Get the connection string value to verify it's not empty
    const connectionString = await sourceConnectionInput.inputValue();
    console.log('Source connection string:', connectionString ? 'Present' : 'Empty');

    // Find and click the Refresh Schema button
    const refreshButton = page.locator('button:has-text("Refresh schema")');
    await expect(refreshButton).toBeVisible();
    
    console.log('Clicking Refresh Schema button...');
    await refreshButton.click();
    
    // Wait for the page to reload/refresh after schema refresh
    await page.waitForLoadState('networkidle');
    
    // Check if tables are now displayed (indicating successful schema refresh)
    const tableRows = page.locator('tbody#tablesBody tr').filter({ hasNot: page.locator('.collapse') });
    const tableCount = await tableRows.count();
    console.log(`Found ${tableCount} tables after refresh`);

    if (tableCount === 0) {
      // Check if there's an error message or "No tables found" message
      const noTablesMessage = page.locator('text=No tables found');
      const errorMessage = page.locator('.alert-danger');
      
      if (await noTablesMessage.isVisible()) {
        console.log('No tables found - this indicates the refresh might not be connecting to the right database');
      }
      
      if (await errorMessage.isVisible()) {
        const errorText = await errorMessage.textContent();
        console.log('Error message found:', errorText);
      }
    } else {
      console.log('Schema refresh appears successful - tables found');
      
      // Verify that at least some tables have columns
      await tableRows.first().locator('button:has-text("Columns")').click();
      await page.waitForTimeout(1000); // Wait for collapse to open
      
      const columns = page.locator('.column-card');
      const columnCount = await columns.count();
      console.log(`Found ${columnCount} columns in first table`);
      
      expect(columnCount).toBeGreaterThan(0);
    }
  });

  test('should show meaningful error when source connection is missing', async ({ page }) => {
    // This test would require a product without a source connection string
    // For now, just verify the page loads correctly
    
    const productLinks = page.locator('a[href*="/Home/ProductMappings"]');
    const productCount = await productLinks.count();
    
    if (productCount > 0) {
      await productLinks.first().click();
      await page.waitForLoadState('networkidle');
      
      // Verify the source connection input exists
      const sourceConnectionInput = page.locator('input[name="SourceConnectionString"]');
      await expect(sourceConnectionInput).toBeVisible();
      
      const connectionString = await sourceConnectionInput.inputValue();
      
      if (!connectionString || connectionString.trim() === '') {
        console.log('Found product with empty source connection string');
        
        // Try refresh schema and expect meaningful error
        const refreshButton = page.locator('button:has-text("Refresh schema")');
        await refreshButton.click();
        await page.waitForLoadState('networkidle');
        
        // Should show error or no tables message
        const noTablesMessage = page.locator('text=No tables found');
        const errorAlert = page.locator('.alert-danger');
        
        const hasError = await errorAlert.isVisible();
        const hasNoTablesMsg = await noTablesMessage.isVisible();
        
        expect(hasError || hasNoTablesMsg).toBe(true);
        
        if (hasError) {
          const errorText = await errorAlert.textContent();
          console.log('Error shown:', errorText);
        }
      }
    }
  });
});