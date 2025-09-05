import { test, expect } from '@playwright/test';

const WEB_URL = 'http://localhost:7002';

test.describe('Agent Deployment Commands Display', () => {
  
  test('should display deployment commands for existing agents', async ({ page }) => {
    // Navigate to agents page
    await page.goto(`${WEB_URL}/Agents`);
    await expect(page).toHaveTitle(/Agents/);
    
    // Check if there are any agents
    const agentRows = page.locator('tbody tr');
    const count = await agentRows.count();
    
    if (count === 0 || (count === 1 && (await agentRows.first().textContent())?.includes('No agents registered'))) {
      console.log('No agents found, creating one first');
      
      // Create an agent first
      await page.click('button:has-text("Pre-Register Agent")');
      await page.fill('#agentName', 'Test-Deploy-Agent');
      await page.fill('#hostName', 'test-deploy-host');
      await page.selectOption('#location', 'Docker');
      await page.click('#preRegisterBtn');
      await page.waitForSelector('#registrationResult', { state: 'visible', timeout: 10000 });
      await page.click('.modal-footer button:has-text("Close")');
      await page.reload();
    }
    
    // Find the terminal button for the first agent
    const terminalButton = page.locator('button[title="Deployment Commands"]').first();
    await expect(terminalButton).toBeVisible();
    
    // Click the terminal button
    await terminalButton.click();
    
    // Verify deployment modal opens
    const deployModal = page.locator('#deploymentCommandsModal');
    await expect(deployModal).toBeVisible();
    
    // Check modal title contains agent name
    const modalTitle = deployModal.locator('.modal-title');
    await expect(modalTitle).toContainText('Deployment Commands');
    
    // Verify Agent ID field is populated
    const agentIdField = page.locator('#deployAgentId');
    const agentIdValue = await agentIdField.inputValue();
    expect(agentIdValue).toMatch(/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/);
    console.log('Agent ID displayed:', agentIdValue);
    
    // Test Docker tab
    await page.click('#deploy-docker-tab');
    const dockerCommand = await page.locator('#deployDockerCommand').textContent();
    expect(dockerCommand).toContain('docker run');
    expect(dockerCommand).toContain(agentIdValue);
    expect(dockerCommand).toContain('OBFUSCATION_Agent__AgentId');
    expect(dockerCommand).toContain('obfuscation-agent:latest');
    console.log('Docker command verified');
    
    // Test Kubernetes tab
    await page.click('#deploy-k8s-tab');
    const k8sYaml = await page.locator('#deployK8sYaml').textContent();
    expect(k8sYaml).toContain('apiVersion: apps/v1');
    expect(k8sYaml).toContain('kind: Deployment');
    expect(k8sYaml).toContain(agentIdValue);
    expect(k8sYaml).toContain('obfuscation-agent:latest');
    console.log('Kubernetes YAML verified');
    
    // Test Azure tab
    await page.click('#deploy-azure-tab');
    const azureCommand = await page.locator('#deployAzureCommand').textContent();
    expect(azureCommand).toContain('az container create');
    expect(azureCommand).toContain(agentIdValue);
    expect(azureCommand).toContain('--resource-group obfuscation-rg');
    
    const azureAppService = await page.locator('#deployAzureAppService').textContent();
    expect(azureAppService).toContain('az webapp create');
    expect(azureAppService).toContain(agentIdValue);
    console.log('Azure commands verified');
    
    // Test Docker Compose tab
    await page.click('#deploy-compose-tab');
    const composeYaml = await page.locator('#deployComposeYaml').textContent();
    expect(composeYaml).toContain('version:');
    expect(composeYaml).toContain('services:');
    expect(composeYaml).toContain(agentIdValue);
    expect(composeYaml).toContain('obfuscation-agent:latest');
    console.log('Docker Compose YAML verified');
    
    // Test copy functionality
    await page.click('#deploy-docker-tab');
    const copyButton = page.locator('button:has-text("Copy Command")').first();
    await copyButton.click();
    console.log('Copy button clicked successfully');
    
    // Test download functionality for YAML files
    await page.click('#deploy-k8s-tab');
    const downloadButton = page.locator('button:has-text("Download YAML")').first();
    
    // Set up download promise before clicking
    const downloadPromise = page.waitForEvent('download');
    await downloadButton.click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe('agent-deployment.yaml');
    console.log('Download functionality verified');
    
    // Close modal
    await page.click('#deploymentCommandsModal .btn-close');
    await expect(deployModal).toBeHidden();
    
    // Take screenshot
    await page.screenshot({ 
      path: 'test-results/deployment-commands-test.png', 
      fullPage: true 
    });
    
    console.log('All deployment command features verified successfully');
  });
  
  test('should show warning about API key requirement', async ({ page }) => {
    await page.goto(`${WEB_URL}/Agents`);
    
    // Open deployment modal for first agent
    const terminalButton = page.locator('button[title="Deployment Commands"]').first();
    
    if (await terminalButton.isVisible()) {
      await terminalButton.click();
      
      // Check for API key warning
      const warning = page.locator('.alert-warning');
      await expect(warning).toBeVisible();
      await expect(warning).toContainText('API key');
      await expect(warning).toContainText('OBFUSCATION_Agent__ApiKey');
      
      console.log('API key warning is properly displayed');
    } else {
      console.log('No agents available to test');
    }
  });
});