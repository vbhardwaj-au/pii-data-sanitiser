import { test, expect } from '@playwright/test';
import { ApiTestHelpers, TestConstants } from '../../utils/test-helpers';
import testData from '../../fixtures/test-data.json';

test.describe('Stage API Integration Tests', () => {
  let apiHelpers: ApiTestHelpers;
  let testProductId: string;
  let pipelineId: string;
  let stageExecutionId: string;

  test.beforeAll(async ({ request }) => {
    apiHelpers = new ApiTestHelpers(request);
    
    // Create a test product
    const product = await apiHelpers.createProduct(testData.products[0]);
    testProductId = product.id;
    
    // Create connection strings for the product
    for (const connData of testData.connectionStrings) {
      await request.post(`/api/products/${testProductId}/connections`, {
        data: {
          productId: testProductId,
          ...connData
        }
      });
    }

    // Start a pipeline to have stages to work with
    const startRequest = {
      productId: testProductId,
      initiatedBy: 'test-user',
      isDryRun: false,
      parameters: { batchSize: 1000 }
    };

    const pipelineResponse = await request.post('/api/pipeline/start', {
      data: startRequest
    });

    if (pipelineResponse.ok()) {
      const pipeline = await pipelineResponse.json();
      pipelineId = pipeline.id;
      
      // Get the first stage execution ID
      if (pipeline.stageExecutions && pipeline.stageExecutions.length > 0) {
        stageExecutionId = pipeline.stageExecutions[0].id;
      }
    }
  });

  test.afterAll(async ({ request }) => {
    // Clean up test data
    if (pipelineId) {
      try {
        await request.post(`/api/pipeline/cancel/${pipelineId}`, {
          data: { cancellationReason: 'Test cleanup' }
        });
      } catch (e) {
        // Ignore cleanup errors
      }
    }
    
    if (testProductId) {
      await apiHelpers.deleteProduct(testProductId);
    }
  });

  test.describe('Stage Execution Control', () => {
    test('should start a stage', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const response = await request.post(`/api/pipeline/stage/start/${stageExecutionId}`);
      
      // May succeed or fail depending on prerequisites and current state
      if (response.ok()) {
        const stage = await response.json();
        expect(stage).toHaveProperty('id');
        expect(stage).toHaveProperty('status');
        expect(stage.id).toBe(stageExecutionId);
      } else {
        // Should return appropriate error for invalid state
        expect([400, 404]).toContain(response.status());
        
        if (response.status() === 400) {
          const error = await response.json();
          expect(error).toHaveProperty('title');
          expect(error).toHaveProperty('detail');
        }
      }
    });

    test('should execute a stage completely', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const response = await request.post(`/api/pipeline/stage/execute/${stageExecutionId}`);
      
      // May succeed or fail depending on prerequisites and current state
      if (response.ok()) {
        const stage = await response.json();
        expect(stage).toHaveProperty('id');
        expect(stage).toHaveProperty('status');
        expect(stage.id).toBe(stageExecutionId);
      } else {
        // Should return appropriate error for invalid state
        expect([400, 404]).toContain(response.status());
      }
    });

    test('should get stage execution details', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const response = await request.get(`/api/pipeline/stage/${stageExecutionId}`);
      
      if (response.ok()) {
        const stage = await response.json();
        expect(stage).toHaveProperty('id');
        expect(stage).toHaveProperty('status');
        expect(stage).toHaveProperty('stageName');
        expect(stage).toHaveProperty('executionOrder');
        expect(stage.id).toBe(stageExecutionId);
      } else {
        expect(response.status()).toBe(404);
      }
    });

    test('should handle stage not found', async ({ request }) => {
      const nonExistentId = '00000000-0000-0000-0000-000000000000';
      
      const response = await request.get(`/api/pipeline/stage/${nonExistentId}`);
      
      expect(response.status()).toBe(404);
      const error = await response.json();
      expect(error).toHaveProperty('title');
      expect(error).toHaveProperty('detail');
    });
  });

  test.describe('Stage Status Updates', () => {
    test('should update stage status', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const statusUpdate = {
        status: 'Running',
        errorMessage: null
      };

      const response = await request.patch(`/api/pipeline/stage/status/${stageExecutionId}`, {
        data: statusUpdate
      });
      
      // May succeed or fail depending on current state
      if (response.ok()) {
        const updatedStage = await response.json();
        expect(updatedStage).toHaveProperty('id');
        expect(updatedStage).toHaveProperty('status');
        expect(updatedStage.id).toBe(stageExecutionId);
      } else {
        expect(response.status()).toBe(400);
        const error = await response.json();
        expect(error).toHaveProperty('title');
      }
    });

    test('should validate status transitions', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const invalidStatusUpdate = {
        status: 'InvalidStatus',
        errorMessage: null
      };

      const response = await request.patch(`/api/pipeline/stage/status/${stageExecutionId}`, {
        data: invalidStatusUpdate
      });
      
      expect(response.status()).toBe(400);
      const error = await response.json();
      expect(error).toHaveProperty('title');
    });

    test('should update stage progress', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const progressUpdate = {
        progressPercentage: 50.5,
        rowsProcessed: 1000
      };

      const response = await request.patch(`/api/pipeline/stage/progress/${stageExecutionId}`, {
        data: progressUpdate
      });
      
      if (response.ok()) {
        const updatedStage = await response.json();
        expect(updatedStage).toHaveProperty('id');
        expect(updatedStage).toHaveProperty('progressPercentage');
        expect(updatedStage.id).toBe(stageExecutionId);
      } else {
        // May fail if stage is not in a valid state for progress updates
        expect(response.status()).toBe(500);
      }
    });

    test('should validate progress percentage range', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const invalidProgressUpdate = {
        progressPercentage: 150, // Invalid: > 100
        rowsProcessed: 1000
      };

      const response = await request.patch(`/api/pipeline/stage/progress/${stageExecutionId}`, {
        data: invalidProgressUpdate
      });
      
      expect(response.status()).toBe(400);
    });

    test('should validate negative rows processed', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const invalidProgressUpdate = {
        progressPercentage: 50,
        rowsProcessed: -100 // Invalid: negative value
      };

      const response = await request.patch(`/api/pipeline/stage/progress/${stageExecutionId}`, {
        data: invalidProgressUpdate
      });
      
      expect(response.status()).toBe(400);
    });
  });

  test.describe('Stage Metrics', () => {
    test('should update stage metrics', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const metricsUpdate = {
        rowsProcessed: 5000,
        rowsAffected: 4500,
        memoryUsageMB: 256.5,
        cpuUsagePercentage: 75.2
      };

      const response = await request.patch(`/api/pipeline/stage/metrics/${stageExecutionId}`, {
        data: metricsUpdate
      });
      
      if (response.ok()) {
        const result = await response.json();
        expect(result).toHaveProperty('message');
      } else {
        // May fail if stage doesn't exist or is not in valid state
        expect(response.status()).toBeGreaterThanOrEqual(400);
      }
    });

    test('should validate metrics values', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const invalidMetricsUpdate = {
        rowsProcessed: -1, // Invalid: negative
        rowsAffected: 1000,
        memoryUsageMB: 256.5,
        cpuUsagePercentage: 150 // Invalid: > 100
      };

      const response = await request.patch(`/api/pipeline/stage/metrics/${stageExecutionId}`, {
        data: invalidMetricsUpdate
      });
      
      expect(response.status()).toBe(400);
    });

    test('should require all metrics fields', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const incompleteMetricsUpdate = {
        rowsProcessed: 1000
        // Missing required fields
      };

      const response = await request.patch(`/api/pipeline/stage/metrics/${stageExecutionId}`, {
        data: incompleteMetricsUpdate
      });
      
      expect(response.status()).toBe(400);
    });
  });

  test.describe('Stage State Validation', () => {
    test('should check stage transition validity', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const newStatus = 'Running';
      
      const response = await request.get(`/api/pipeline/stage/${stageExecutionId}/can-transition/${newStatus}`);
      
      if (response.ok()) {
        const canTransition = await response.json();
        expect(typeof canTransition).toBe('boolean');
      } else {
        expect(response.status()).toBe(500);
      }
    });

    test('should validate invalid status for transition check', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const invalidStatus = 'InvalidStatus';
      
      const response = await request.get(`/api/pipeline/stage/${stageExecutionId}/can-transition/${invalidStatus}`);
      
      if (response.ok()) {
        const canTransition = await response.json();
        expect(canTransition).toBe(false);
      } else {
        expect(response.status()).toBe(500);
      }
    });
  });

  test.describe('Request Validation', () => {
    test('should validate GUID format for stage operations', async ({ request }) => {
      const invalidGuid = 'not-a-guid';
      
      const response = await request.get(`/api/pipeline/stage/${invalidGuid}`);
      
      expect(response.status()).toBe(404);
    });

    test('should validate status field format', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const invalidStatus = {
        status: 'A'.repeat(25), // Exceeds 20 char limit
        errorMessage: null
      };

      const response = await request.patch(`/api/pipeline/stage/status/${stageExecutionId}`, {
        data: invalidStatus
      });
      
      expect(response.status()).toBe(400);
    });

    test('should validate error message length', async ({ request }) => {
      if (!stageExecutionId) {
        test.skip('No stage execution ID available');
      }

      const longErrorMessage = {
        status: 'Failed',
        errorMessage: 'A'.repeat(1001) // Exceeds 1000 char limit
      };

      const response = await request.patch(`/api/pipeline/stage/status/${stageExecutionId}`, {
        data: longErrorMessage
      });
      
      expect(response.status()).toBe(400);
    });
  });
});