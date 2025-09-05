import { test, expect } from '@playwright/test';

const WEB_URL = 'http://localhost:7002';

test('Verify terminal icon is visible in agent grid', async ({ page }) => {
  // Navigate to agents page
  await page.goto(`${WEB_URL}/Agents`);
  await expect(page).toHaveTitle(/Agents/);
  
  // Wait for the page to load
  await page.waitForLoadState('networkidle');
  
  // Check if there are any agent rows
  const agentRows = page.locator('tbody tr').filter({ hasNot: page.locator('text=No agents registered') });
  const agentCount = await agentRows.count();
  
  console.log(`Found ${agentCount} agents in the grid`);
  
  if (agentCount > 0) {
    // Look for terminal buttons
    const terminalButtons = page.locator('button[title="Deployment Commands"]');
    const terminalButtonCount = await terminalButtons.count();
    
    console.log(`Found ${terminalButtonCount} terminal buttons`);
    
    // Check the icon inside the buttons
    const terminalIcons = page.locator('button[title="Deployment Commands"] i.bi-terminal');
    const iconCount = await terminalIcons.count();
    
    console.log(`Found ${iconCount} terminal icons`);
    
    // Verify at least one terminal button is visible
    if (terminalButtonCount > 0) {
      await expect(terminalButtons.first()).toBeVisible();
      console.log('✅ Terminal button is visible');
      
      // Hover over the button to show tooltip
      await terminalButtons.first().hover();
      await page.waitForTimeout(500);
      
      // Click the terminal button
      await terminalButtons.first().click();
      
      // Verify modal opens
      const modal = page.locator('#deploymentCommandsModal');
      await expect(modal).toBeVisible();
      console.log('✅ Deployment commands modal opens successfully');
      
      // Verify modal has content
      const modalTitle = modal.locator('.modal-title');
      await expect(modalTitle).toContainText('Deployment Commands');
      
      // Verify agent ID field
      const agentIdField = page.locator('#deployAgentId');
      const agentId = await agentIdField.inputValue();
      expect(agentId).toBeTruthy();
      console.log(`✅ Agent ID displayed: ${agentId}`);
      
      // Take screenshot showing the terminal button and modal
      await page.screenshot({ 
        path: 'test-results/terminal-icon-and-modal.png', 
        fullPage: true 
      });
      
      console.log('Screenshot saved: test-results/terminal-icon-and-modal.png');
    } else {
      throw new Error('No terminal buttons found even though agents exist');
    }
  } else {
    console.log('No agents found, creating one to test');
    
    // Create an agent first
    await page.click('button:has-text("Pre-Register Agent")');
    await page.fill('#agentName', 'Icon-Test-Agent');
    await page.fill('#hostName', 'icon-test-host');
    await page.selectOption('#location', 'Docker');
    await page.click('#preRegisterBtn');
    
    // Wait for registration to complete
    await page.waitForSelector('#registrationResult', { state: 'visible', timeout: 10000 });
    await page.click('.modal-footer button:has-text("Close")');
    
    // Reload to see the new agent
    await page.reload();
    await page.waitForLoadState('networkidle');
    
    // Now verify the terminal button exists
    const terminalButton = page.locator('button[title="Deployment Commands"]').first();
    await expect(terminalButton).toBeVisible();
    console.log('✅ Terminal button visible after creating agent');
    
    // Take screenshot
    await page.screenshot({ 
      path: 'test-results/terminal-icon-after-create.png', 
      fullPage: true 
    });
  }
  
  // Also check the HTML source
  const pageContent = await page.content();
  const hasTerminalClass = pageContent.includes('bi-terminal');
  console.log(`HTML contains 'bi-terminal' class: ${hasTerminalClass}`);
  
  // Check button structure
  const firstAgentRow = agentRows.first();
  if (await firstAgentRow.isVisible()) {
    const actionsCell = firstAgentRow.locator('td').last();
    const buttons = actionsCell.locator('button');
    const buttonCount = await buttons.count();
    console.log(`Buttons in actions cell: ${buttonCount}`);
    
    for (let i = 0; i < buttonCount; i++) {
      const button = buttons.nth(i);
      const title = await button.getAttribute('title');
      const iconClass = await button.locator('i').getAttribute('class');
      console.log(`Button ${i + 1}: title="${title}", icon="${iconClass}"`);
    }
  }
});