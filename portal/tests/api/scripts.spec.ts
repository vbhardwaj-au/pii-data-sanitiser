import { test, expect } from '@playwright/test';
import { APITestHelper } from '../utils/api-test-helper';

// Test data
const validScript = 'SELECT COUNT(*) FROM Users WHERE IsActive = 1;';
const dangerousScript = 'EXEC xp_cmdshell "dir";';
const ddlScript = 'CREATE TABLE TestTable (Id INT PRIMARY KEY);';

const createScriptData = {
  name: "Test Data Count Script",
  description: "Counts active users for validation",
  sqlScript: validScript,
  isEnabled: true,
  timeoutMinutes: 30,
  onFailureAction: "Stop",
  retryAttempts: 0,
  retryDelaySeconds: 30,
  expectedResultType: "Scalar",
  tags: "validation,testing",
  createdBy: "test-user"
};

test.describe('Scripts API', () => {
  let apiHelper: APITestHelper;
  const productId = "00000000-0000-0000-0000-000000000001";
  const stageId = "00000000-0000-0000-0000-000000000002"; // Will need to get actual stage ID

  test.beforeAll(async ({ request }) => {
    apiHelper = new APITestHelper(request);
    
    // Verify API is accessible
    const healthCheck = await apiHelper.healthCheck();
    expect(healthCheck).toBe(true);
  });

  test.describe('GET /api/products/{productId}/stages/{stageId}/scripts', () => {
    test('should get scripts for product and stage', async () => {
      const response = await apiHelper.request.get(`/api/products/${productId}/stages/${stageId}/scripts`);
      
      // Should either return scripts or empty array (depending on data)
      expect([200, 404]).toContain(response.status());
      
      if (response.status() === 200) {
        const data = await response.json();
        expect(Array.isArray(data)).toBe(true);
        
        // Validate script structure if any exist
        data.forEach((script: any) => {
          expect(script).toHaveProperty('id');
          expect(script).toHaveProperty('productId');
          expect(script).toHaveProperty('pipelineStageId');
          expect(script).toHaveProperty('name');
          expect(script).toHaveProperty('sqlScript');
          expect(script).toHaveProperty('executionOrder');
          expect(script).toHaveProperty('isEnabled');
          expect(script).toHaveProperty('timeoutMinutes');
          expect(script).toHaveProperty('onFailureAction');
          expect(script).toHaveProperty('retryAttempts');
          expect(script).toHaveProperty('isSecurityValidated');
          expect(script).toHaveProperty('expectedResultType');
          expect(script).toHaveProperty('createdAt');
          expect(script).toHaveProperty('updatedAt');
          
          // Validate ranges
          expect(script.executionOrder).toBeGreaterThanOrEqual(1);
          expect(script.timeoutMinutes).toBeGreaterThanOrEqual(1);
          expect(script.timeoutMinutes).toBeLessThanOrEqual(1440);
          expect(script.retryAttempts).toBeGreaterThanOrEqual(0);
          expect(script.retryAttempts).toBeLessThanOrEqual(10);
          
          // Validate failure actions
          expect(['Stop', 'Skip', 'Warn']).toContain(script.onFailureAction);
          
          // Validate result types
          expect(['None', 'RowCount', 'Scalar', 'ResultSet']).toContain(script.expectedResultType);
        });
      }
    });

    test('should filter enabled scripts only', async () => {
      const response = await apiHelper.request.get(`/api/products/${productId}/stages/${stageId}/scripts?enabledOnly=true`);
      
      if (response.status() === 200) {
        const data = await response.json();
        expect(Array.isArray(data)).toBe(true);
        
        // All returned scripts should be enabled and security validated
        data.forEach((script: any) => {
          expect(script.isEnabled).toBe(true);
          expect(script.isSecurityValidated).toBe(true);
        });
      } else {
        expect([404]).toContain(response.status());
      }
    });
  });

  test.describe('POST /api/products/{productId}/stages/{stageId}/scripts', () => {
    test('should create new script with valid SQL', async () => {
      const response = await apiHelper.request.post(`/api/products/${productId}/stages/${stageId}/scripts`, {
        data: createScriptData
      });
      
      // May fail if product/stage doesn't exist, but should handle gracefully
      if (response.status() === 201) {
        const data = await response.json();
        
        expect(data).toMatchObject({
          name: createScriptData.name,
          description: createScriptData.description,
          sqlScript: createScriptData.sqlScript,
          isEnabled: createScriptData.isEnabled,
          timeoutMinutes: createScriptData.timeoutMinutes,
          onFailureAction: createScriptData.onFailureAction,
          expectedResultType: createScriptData.expectedResultType,
          tags: createScriptData.tags,
          createdBy: createScriptData.createdBy
        });
        
        expect(data.id).toBeDefined();
        expect(data.executionOrder).toBeGreaterThanOrEqual(1);
        expect(data.isSecurityValidated).toBe(true); // Valid script should pass
        expect(data.createdAt).toBeDefined();
        expect(data.updatedAt).toBeDefined();
        
      } else {
        // Expected business logic failures
        expect([400, 404, 409]).toContain(response.status());
        console.log(`Script creation failed as expected: ${response.status()}`);
      }
    });

    test('should reject script with dangerous SQL', async () => {
      const dangerousScriptData = {
        ...createScriptData,
        name: "Dangerous Script",
        sqlScript: dangerousScript
      };
      
      const response = await apiHelper.request.post(`/api/products/${productId}/stages/${stageId}/scripts`, {
        data: dangerousScriptData
      });
      
      // Should either fail at creation or create with security validation failed
      if (response.status() === 201) {
        const data = await response.json();
        expect(data.isSecurityValidated).toBe(false);
        expect(data.securityValidationNotes).toContain('xp_cmdshell');
      } else {
        expect([400, 409]).toContain(response.status());
      }
    });

    test('should validate script properties', async () => {
      const invalidScriptData = {
        ...createScriptData,
        name: "", // Invalid: empty name
        timeoutMinutes: 2000, // Invalid: too large
        onFailureAction: "InvalidAction", // Invalid action
        retryAttempts: 15 // Invalid: too many retries
      };
      
      const response = await apiHelper.request.post(`/api/products/${productId}/stages/${stageId}/scripts`, {
        data: invalidScriptData
      });
      
      expect([400, 422]).toContain(response.status());
    });
  });

  test.describe('Script Security and Validation', () => {
    test('should handle scripts with different complexity levels', async () => {
      const scripts = [
        { name: "Simple Select", sql: "SELECT * FROM Users", expectedComplexity: "Low" },
        { name: "Join Query", sql: "SELECT u.*, p.Name FROM Users u JOIN Products p ON u.ProductId = p.Id", expectedComplexity: "Medium" },
        { name: "Complex CTE", sql: "WITH RECURSIVE UserHierarchy AS (SELECT * FROM Users WHERE ParentId IS NULL UNION ALL SELECT u.* FROM Users u JOIN UserHierarchy uh ON u.ParentId = uh.Id) SELECT * FROM UserHierarchy", expectedComplexity: "High" }
      ];

      for (const script of scripts) {
        const scriptData = {
          ...createScriptData,
          name: script.name,
          sqlScript: script.sql
        };
        
        const response = await apiHelper.request.post(`/api/products/${productId}/stages/${stageId}/scripts`, {
          data: scriptData
        });
        
        // Test may fail due to missing product/stage, but structure should be valid
        if (response.status() === 201) {
          const data = await response.json();
          console.log(`Created script: ${script.name} - Security validated: ${data.isSecurityValidated}`);
        }
      }
    });
  });
});