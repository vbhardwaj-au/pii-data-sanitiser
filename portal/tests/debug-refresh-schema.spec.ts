import { test, expect } from '@playwright/test';

test.describe('Debug Refresh Schema', () => {
  test('debug the product mappings page', async ({ page }) => {
    console.log('Navigating to homepage...');
    await page.goto('http://localhost:7002');
    await page.waitForLoadState('networkidle');

    // Take screenshot of homepage
    await page.screenshot({ path: 'homepage.png' });

    console.log('Looking for product links...');
    const productLinks = page.locator('a[href*="/Home/ProductMappings"]');
    const productCount = await productLinks.count();
    console.log(`Found ${productCount} product links`);

    if (productCount > 0) {
      console.log('Clicking on first product...');
      await productLinks.first().click();
      await page.waitForLoadState('networkidle');

      // Take screenshot of product page
      await page.screenshot({ path: 'product-page.png' });

      // Debug what's actually on the page
      const pageTitle = await page.title();
      console.log('Page title:', pageTitle);

      const h2Text = await page.locator('h2').first().textContent();
      console.log('H2 text:', h2Text);

      // Check for any connection string inputs
      const connectionInputs = page.locator('input[name*="Connection"]');
      const inputCount = await connectionInputs.count();
      console.log(`Found ${inputCount} connection-related inputs`);

      for (let i = 0; i < inputCount; i++) {
        const input = connectionInputs.nth(i);
        const name = await input.getAttribute('name');
        const value = await input.inputValue();
        console.log(`Input ${i}: name="${name}", value="${value ? value.substring(0, 50) + '...' : 'empty'}"`);
      }

      // Look for refresh schema button
      const refreshButtons = page.locator('button:has-text("Refresh")');
      const refreshCount = await refreshButtons.count();
      console.log(`Found ${refreshCount} refresh buttons`);

      // Check if any error messages exist
      const errorAlerts = page.locator('.alert-danger');
      const errorCount = await errorAlerts.count();
      console.log(`Found ${errorCount} error alerts`);

      if (errorCount > 0) {
        const errorText = await errorAlerts.first().textContent();
        console.log('Error message:', errorText);
      }

      // Check for table content
      const tableRows = page.locator('tbody tr');
      const rowCount = await tableRows.count();
      console.log(`Found ${rowCount} table rows`);

      // Check if "No tables found" message exists
      const noTablesMsg = page.locator('text=No tables found');
      const hasNoTablesMsg = await noTablesMsg.isVisible();
      console.log('Has "No tables found" message:', hasNoTablesMsg);
    } else {
      console.log('No product links found on homepage');
      
      // Debug homepage content
      const bodyText = await page.locator('body').textContent();
      console.log('Homepage body text (first 500 chars):', bodyText?.substring(0, 500));
    }
  });
});