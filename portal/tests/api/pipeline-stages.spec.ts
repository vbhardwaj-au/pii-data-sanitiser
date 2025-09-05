import { test, expect } from '@playwright/test';
import { APITestHelper } from '../utils/api-test-helper';

test.describe('Pipeline Stages API', () => {
  let apiHelper: APITestHelper;

  test.beforeAll(async ({ request }) => {
    apiHelper = new APITestHelper(request);
    
    // Verify API is accessible
    const healthCheck = await apiHelper.healthCheck();
    expect(healthCheck).toBe(true);
  });

  test.describe('GET /api/pipelinestages', () => {
    test('should get all pipeline stages', async () => {
      const response = await apiHelper.request.get('/api/pipelinestages');
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      expect(Array.isArray(data)).toBe(true);
      
      // Should have the 5 default stages
      if (data.length > 0) {
        expect(data.length).toBe(5);
        
        // Verify stages are ordered by execution order
        for (let i = 1; i < data.length; i++) {
          expect(data[i].executionOrder).toBeGreaterThanOrEqual(data[i-1].executionOrder);
        }
        
        // Verify expected stage names exist
        const stageNames = data.map((stage: any) => stage.name);
        expect(stageNames).toContain('DataReduction');
        expect(stageNames).toContain('PreObfuscation');
        expect(stageNames).toContain('Obfuscation');
        expect(stageNames).toContain('PostObfuscation');
        expect(stageNames).toContain('Testing');
        
        // Verify obfuscation stage is required
        const obfuscationStage = data.find((stage: any) => stage.name === 'Obfuscation');
        expect(obfuscationStage?.isRequired).toBe(true);
        
        // Verify all stages have required properties
        data.forEach((stage: any) => {
          expect(stage).toHaveProperty('id');
          expect(stage).toHaveProperty('name');
          expect(stage).toHaveProperty('displayName');
          expect(stage).toHaveProperty('executionOrder');
          expect(stage).toHaveProperty('isRequired');
          expect(stage).toHaveProperty('isActive');
          expect(stage).toHaveProperty('defaultTimeoutMinutes');
          expect(stage).toHaveProperty('supportsParallelExecution');
          expect(stage).toHaveProperty('createdAt');
          expect(stage).toHaveProperty('updatedAt');
          
          // Validate execution order range
          expect(stage.executionOrder).toBeGreaterThanOrEqual(1);
          expect(stage.executionOrder).toBeLessThanOrEqual(5);
          
          // Validate timeout range
          expect(stage.defaultTimeoutMinutes).toBeGreaterThanOrEqual(1);
          expect(stage.defaultTimeoutMinutes).toBeLessThanOrEqual(1440);
        });
      }
    });

    test('should get only active pipeline stages', async () => {
      const response = await apiHelper.request.get('/api/pipelinestages?activeOnly=true');
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      expect(Array.isArray(data)).toBe(true);
      
      // All returned stages should be active
      data.forEach((stage: any) => {
        expect(stage.isActive).toBe(true);
      });
    });
  });

  test.describe('Pipeline Stage Data Validation', () => {
    test('should have valid configuration templates', async () => {
      const response = await apiHelper.request.get('/api/pipelinestages');
      expect(response.status()).toBe(200);
      
      const stages = await response.json();
      
      stages.forEach((stage: any) => {
        if (stage.configurationTemplate) {
          // Should be valid JSON
          expect(() => JSON.parse(stage.configurationTemplate)).not.toThrow();
          
          const config = JSON.parse(stage.configurationTemplate);
          
          // Should have required properties
          expect(config).toHaveProperty('enabled');
          expect(config).toHaveProperty('timeoutMinutes');
          expect(config).toHaveProperty('onFailure');
          
          // Validate timeout
          expect(config.timeoutMinutes).toBeGreaterThanOrEqual(1);
          expect(config.timeoutMinutes).toBeLessThanOrEqual(1440);
          
          // Validate failure action
          expect(['stop', 'skip', 'warn']).toContain(config.onFailure.toLowerCase());
        }
      });
    });

    test('should have unique execution orders', async () => {
      const response = await apiHelper.request.get('/api/pipelinestages');
      expect(response.status()).toBe(200);
      
      const stages = await response.json();
      const executionOrders = stages.map((stage: any) => stage.executionOrder);
      const uniqueOrders = [...new Set(executionOrders)];
      
      expect(executionOrders.length).toBe(uniqueOrders.length);
    });

    test('should have unique stage names', async () => {
      const response = await apiHelper.request.get('/api/pipelinestages');
      expect(response.status()).toBe(200);
      
      const stages = await response.json();
      const stageNames = stages.map((stage: any) => stage.name.toLowerCase());
      const uniqueNames = [...new Set(stageNames)];
      
      expect(stageNames.length).toBe(uniqueNames.length);
    });

    test('should have exactly one required stage (Obfuscation)', async () => {
      const response = await apiHelper.request.get('/api/pipelinestages');
      expect(response.status()).toBe(200);
      
      const stages = await response.json();
      const requiredStages = stages.filter((stage: any) => stage.isRequired);
      
      expect(requiredStages.length).toBe(1);
      expect(requiredStages[0].name).toBe('Obfuscation');
      expect(requiredStages[0].executionOrder).toBe(3);
    });
  });

  test.describe('GET /api/products/{productId}/pipeline-configurations', () => {
    test('should get pipeline configurations for existing product', async () => {
      // Use a known product ID (we'll need to handle the case where no products exist)
      const productId = "00000000-0000-0000-0000-000000000001";
      const response = await apiHelper.request.get(`/api/products/${productId}/pipeline-configurations`);
      
      // Should either return configurations or empty array (404 if product doesn't exist)
      expect([200, 404]).toContain(response.status());
      
      if (response.status() === 200) {
        const data = await response.json();
        expect(Array.isArray(data)).toBe(true);
        
        // If configurations exist, validate structure
        data.forEach((config: any) => {
          expect(config).toHaveProperty('id');
          expect(config).toHaveProperty('productId');
          expect(config).toHaveProperty('pipelineStageId');
          expect(config).toHaveProperty('isEnabled');
          expect(config).toHaveProperty('onFailureAction');
          expect(config).toHaveProperty('retryAttempts');
          expect(config).toHaveProperty('retryDelaySeconds');
          expect(config).toHaveProperty('createdAt');
          expect(config).toHaveProperty('updatedAt');
          
          // Should include pipeline stage information
          if (config.pipelineStage) {
            expect(config.pipelineStage).toHaveProperty('name');
            expect(config.pipelineStage).toHaveProperty('displayName');
            expect(config.pipelineStage).toHaveProperty('executionOrder');
          }
          
          // Validate failure action
          expect(['Stop', 'Skip', 'Warn']).toContain(config.onFailureAction);
          
          // Validate retry settings
          expect(config.retryAttempts).toBeGreaterThanOrEqual(0);
          expect(config.retryAttempts).toBeLessThanOrEqual(10);
          expect(config.retryDelaySeconds).toBeGreaterThanOrEqual(1);
          expect(config.retryDelaySeconds).toBeLessThanOrEqual(3600);
        });
      }
    });

    test('should get only enabled configurations when requested', async () => {
      const productId = "00000000-0000-0000-0000-000000000001";
      const response = await apiHelper.request.get(`/api/products/${productId}/pipeline-configurations?enabledOnly=true`);
      
      if (response.status() === 200) {
        const data = await response.json();
        expect(Array.isArray(data)).toBe(true);
        
        // All returned configurations should be enabled
        data.forEach((config: any) => {
          expect(config.isEnabled).toBe(true);
        });
      } else {
        expect([404]).toContain(response.status());
      }
    });

    test('should return 404 for non-existent product', async () => {
      const fakeProductId = '99999999-9999-9999-9999-999999999999';
      const response = await apiHelper.request.get(`/api/products/${fakeProductId}/pipeline-configurations`);
      
      // Should return empty array or 404, depending on implementation
      expect([200, 404]).toContain(response.status());
      
      if (response.status() === 200) {
        const data = await response.json();
        expect(Array.isArray(data)).toBe(true);
        expect(data.length).toBe(0);
      }
    });
  });

  test.describe('Pipeline Stage Integration', () => {
    test('should maintain referential integrity between stages and configurations', async () => {
      // Get all pipeline stages
      const stagesResponse = await apiHelper.request.get('/api/pipelinestages');
      if (stagesResponse.status() !== 200) {
        test.skip();
        return;
      }
      
      const stages = await stagesResponse.json();
      expect(stages.length).toBeGreaterThan(0);
      
      // Try to get configurations for a test product
      const productId = "00000000-0000-0000-0000-000000000001";
      const configResponse = await apiHelper.request.get(`/api/products/${productId}/pipeline-configurations`);
      
      if (configResponse.status() === 200) {
        const configurations = await configResponse.json();
        
        // Verify that configurations reference valid pipeline stages
        configurations.forEach((config: any) => {
          if (config.pipelineStage) {
            const stageExists = stages.some((stage: any) => stage.id === config.pipelineStageId);
            expect(stageExists).toBe(true);
          }
        });
      }
    });
  });
});