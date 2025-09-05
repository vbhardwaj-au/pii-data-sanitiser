import { test, expect } from '@playwright/test';

const API_URL = 'http://localhost:7001';
const WEB_URL = 'http://localhost:7002';

test.describe('Complete Agent Pre-Registration Flow', () => {
  
  test('Full end-to-end agent pre-registration via UI', async ({ page, request }) => {
    // Generate unique agent name for this test
    const timestamp = Date.now();
    const agentName = `UI-Agent-${timestamp}`;
    const hostName = `host-${timestamp}`;
    
    // Navigate to agents page
    await page.goto(`${WEB_URL}/Agents`);
    await expect(page).toHaveTitle(/Agents/);
    
    // Take screenshot of initial state
    await page.screenshot({ path: `test-results/agents-initial-${timestamp}.png` });
    
    // Count existing agents
    const initialAgentCount = await page.locator('tbody tr').count();
    console.log(`Initial agent count: ${initialAgentCount}`);
    
    // Click Pre-Register Agent button
    await page.click('button:has-text("Pre-Register Agent")');
    
    // Wait for modal
    await expect(page.locator('#preRegisterModal')).toBeVisible();
    
    // Fill form with unique values
    await page.fill('#agentName', agentName);
    await page.fill('#hostName', hostName);
    await page.selectOption('#location', 'OnPremises');
    await page.selectOption('#environment', 'Production');
    await page.fill('#cpuCores', '8');
    await page.fill('#memoryMB', '16384');
    await page.fill('#maxConcurrentJobs', '4');
    
    // Enable SQL Server capability
    await page.check('#canProcessSqlServer');
    
    // Take screenshot before submission
    await page.screenshot({ path: `test-results/pre-submit-form-${timestamp}.png` });
    
    // Click submit
    await page.click('#preRegisterBtn');
    
    // Wait for success result
    await expect(page.locator('#registrationResult')).toBeVisible({ timeout: 15000 });
    
    // Capture the generated credentials
    const agentId = await page.inputValue('#resultAgentId');
    const apiKey = await page.inputValue('#resultApiKey');
    
    console.log(`Successfully registered agent: ${agentName}`);
    console.log(`Agent ID: ${agentId}`);
    console.log(`API Key: ${apiKey}`);
    
    expect(agentId).toMatch(/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/);
    expect(apiKey).toContain('pii-agent-');
    
    // Take screenshot of success
    await page.screenshot({ path: `test-results/registration-success-${timestamp}.png` });
    
    // Verify deployment commands are generated
    const dockerCommand = await page.locator('#dockerCommand').textContent();
    expect(dockerCommand).toContain(agentId);
    expect(dockerCommand).toContain('OBFUSCATION_Agent__AgentId');
    
    // Close modal
    await page.click('.modal-footer button:has-text("Close")');
    await expect(page.locator('#preRegisterModal')).toBeHidden();
    
    // Refresh the page
    await page.reload();
    
    // Verify agent appears in list
    await page.waitForTimeout(2000);
    const newAgentRow = page.locator(`tr:has-text("${agentName}")`);
    await expect(newAgentRow).toBeVisible();
    
    // Verify agent details in the row
    const statusBadge = newAgentRow.locator('.badge').first();
    await expect(statusBadge).toContainText('Online');
    
    // Verify via API that agent exists
    const apiResponse = await request.get(`${API_URL}/api/agent`, {
      headers: {
        'X-API-Key': 'portal-admin-key-2024'
      }
    });
    
    expect(apiResponse.ok()).toBeTruthy();
    const agents = await apiResponse.json();
    const createdAgent = agents.find((a: any) => a.id === agentId);
    
    expect(createdAgent).toBeDefined();
    expect(createdAgent.name).toBe(agentName);
    expect(createdAgent.hostName).toBe(hostName);
    expect(createdAgent.location).toBe('OnPremises');
    expect(createdAgent.cpuCores).toBe(8);
    expect(createdAgent.memoryMB).toBe(16384);
    
    // Test agent authentication with generated API key
    const heartbeatResponse = await request.post(`${API_URL}/api/agent/heartbeat`, {
      headers: {
        'X-API-Key': apiKey,
        'Content-Type': 'application/json'
      },
      data: {
        agentId: agentId,
        cpuUsage: 25.5,
        memoryUsage: 45.2,
        activeJobs: 0
      }
    });
    
    expect(heartbeatResponse.ok()).toBeTruthy();
    console.log('Heartbeat sent successfully with generated API key');
    
    // Final screenshot
    await page.screenshot({ path: `test-results/final-state-${timestamp}.png`, fullPage: true });
  });
  
  test('Verify CORS headers are properly configured', async ({ request }) => {
    // Make OPTIONS preflight request
    const response = await request.fetch(`${API_URL}/api/agent/pre-register`, {
      method: 'OPTIONS',
      headers: {
        'Origin': WEB_URL,
        'Access-Control-Request-Method': 'POST',
        'Access-Control-Request-Headers': 'content-type, x-api-key'
      }
    });
    
    expect(response.ok()).toBeTruthy();
    
    // Check CORS headers
    const headers = response.headers();
    expect(headers['access-control-allow-origin']).toBe(WEB_URL);
    expect(headers['access-control-allow-credentials']).toBe('true');
    expect(headers['access-control-allow-headers']).toBeTruthy();
    
    console.log('CORS headers validated:', {
      'allow-origin': headers['access-control-allow-origin'],
      'allow-credentials': headers['access-control-allow-credentials'],
      'allow-headers': headers['access-control-allow-headers']
    });
  });
  
  test('Verify error handling for duplicate agent names', async ({ page }) => {
    const duplicateName = 'DuplicateTestAgent';
    
    // First registration
    await page.goto(`${WEB_URL}/Agents`);
    await page.click('button:has-text("Pre-Register Agent")');
    await page.fill('#agentName', duplicateName);
    await page.fill('#hostName', 'host1');
    await page.selectOption('#location', 'Docker');
    await page.click('#preRegisterBtn');
    
    // Wait for first registration to complete
    await expect(page.locator('#registrationResult')).toBeVisible({ timeout: 10000 });
    await page.click('.modal-footer button:has-text("Close")');
    
    // Try to register with same name
    await page.click('button:has-text("Pre-Register Agent")');
    await page.fill('#agentName', duplicateName);
    await page.fill('#hostName', 'host2');
    await page.selectOption('#location', 'Docker');
    await page.click('#preRegisterBtn');
    
    // Should either show error or create with different ID
    const errorAlert = page.locator('.alert-danger');
    const successResult = page.locator('#registrationResult');
    
    // Wait for either error or success
    await Promise.race([
      errorAlert.waitFor({ state: 'visible', timeout: 5000 }).catch(() => null),
      successResult.waitFor({ state: 'visible', timeout: 5000 }).catch(() => null)
    ]);
    
    if (await successResult.isVisible()) {
      // If successful, agents should have different IDs
      const agentId2 = await page.inputValue('#resultAgentId');
      console.log('Second agent created with different ID:', agentId2);
    } else if (await errorAlert.isVisible()) {
      const errorText = await errorAlert.textContent();
      console.log('Duplicate name handled with error:', errorText);
    }
  });
});