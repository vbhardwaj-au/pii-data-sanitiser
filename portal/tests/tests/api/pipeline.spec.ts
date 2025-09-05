import { test, expect } from '@playwright/test';
import { ApiTestHelpers, TestConstants } from '../../utils/test-helpers';
import testData from '../../fixtures/test-data.json';

test.describe('Pipeline API Integration Tests', () => {
  let apiHelpers: ApiTestHelpers;
  let testProductId: string;
  let pipelineId: string;
  let stageId: string;

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

  test.describe('Pipeline Execution', () => {
    test('should start a new pipeline', async ({ request }) => {
      const startRequest = {
        productId: testProductId,
        initiatedBy: 'test-user',
        isDryRun: false,
        parameters: { batchSize: 1000 }
      };

      const response = await request.post('/api/pipeline/start', {
        data: startRequest
      });

      expect(response.ok()).toBeTruthy();
      const pipeline = await response.json();
      
      expect(pipeline).toHaveProperty('id');
      expect(pipeline).toHaveProperty('productId');
      expect(pipeline).toHaveProperty('status');
      expect(pipeline).toHaveProperty('queuedAt');
      expect(pipeline.productId).toBe(testProductId);
      expect(pipeline.status).toBe('Queued');
      expect(pipeline.initiatedBy).toBe('test-user');
      expect(pipeline.isDryRun).toBe(false);
      
      pipelineId = pipeline.id;
    });

    test('should get pipeline execution details', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const response = await request.get(`/api/pipeline/execution/${pipelineId}`);
      
      expect(response.ok()).toBeTruthy();
      const pipeline = await response.json();
      
      expect(pipeline.id).toBe(pipelineId);
      expect(pipeline.productId).toBe(testProductId);
      expect(pipeline).toHaveProperty('stageExecutions');
      expect(Array.isArray(pipeline.stageExecutions)).toBeTruthy();
    });

    test('should get pipeline executions for product', async ({ request }) => {
      const response = await request.get(`/api/pipeline/executions/${testProductId}?pageSize=10&page=1`);
      
      expect(response.ok()).toBeTruthy();
      const pipelines = await response.json();
      
      expect(Array.isArray(pipelines)).toBeTruthy();
      expect(pipelines.length).toBeGreaterThan(0);
      expect(pipelines[0]).toHaveProperty('id');
      expect(pipelines[0]).toHaveProperty('productId');
      expect(pipelines[0].productId).toBe(testProductId);
    });

    test('should validate pagination parameters', async ({ request }) => {
      // Test invalid page size
      const invalidPageSizeResponse = await request.get(`/api/pipeline/executions/${testProductId}?pageSize=101`);
      expect(invalidPageSizeResponse.status()).toBe(400);

      // Test invalid page number
      const invalidPageResponse = await request.get(`/api/pipeline/executions/${testProductId}?page=0`);
      expect(invalidPageResponse.status()).toBe(400);
    });

    test('should get pipeline progress', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const response = await request.get(`/api/pipeline/execution/${pipelineId}/progress`);
      
      expect(response.ok()).toBeTruthy();
      const progress = await response.json();
      
      expect(progress).toHaveProperty('id');
      expect(progress).toHaveProperty('status');
      expect(progress).toHaveProperty('progressPercentage');
      expect(progress).toHaveProperty('stageProgress');
      expect(Array.isArray(progress.stageProgress)).toBeTruthy();
      expect(progress.id).toBe(pipelineId);
    });

    test('should handle pipeline not found', async ({ request }) => {
      const nonExistentId = '00000000-0000-0000-0000-000000000000';
      
      const response = await request.get(`/api/pipeline/execution/${nonExistentId}`);
      
      expect(response.status()).toBe(404);
      const error = await response.json();
      expect(error).toHaveProperty('title');
      expect(error).toHaveProperty('detail');
    });
  });

  test.describe('Pipeline Control', () => {
    test('should pause pipeline', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const response = await request.post(`/api/pipeline/pause/${pipelineId}`);
      
      // May succeed or fail depending on pipeline state
      if (response.ok()) {
        const result = await response.json();
        expect(result).toHaveProperty('message');
      } else {
        expect(response.status()).toBe(400);
      }
    });

    test('should resume pipeline', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const response = await request.post(`/api/pipeline/resume/${pipelineId}`);
      
      // May succeed or fail depending on pipeline state
      if (response.ok()) {
        const result = await response.json();
        expect(result).toHaveProperty('message');
      } else {
        expect(response.status()).toBe(400);
      }
    });

    test('should cancel pipeline', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const cancelRequest = {
        cancellationReason: 'Integration test cancellation'
      };

      const response = await request.post(`/api/pipeline/cancel/${pipelineId}`, {
        data: cancelRequest
      });
      
      // May succeed or fail depending on pipeline state
      if (response.ok()) {
        const result = await response.json();
        expect(result).toHaveProperty('message');
      } else {
        expect(response.status()).toBe(400);
      }
    });

    test('should validate cancellation reason', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const invalidRequest = {
        cancellationReason: '' // Empty reason should be invalid
      };

      const response = await request.post(`/api/pipeline/cancel/${pipelineId}`, {
        data: invalidRequest
      });
      
      expect(response.status()).toBe(400);
    });
  });

  test.describe('Pipeline Status Updates', () => {
    test('should update pipeline status', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const statusUpdate = {
        status: 'Running',
        errorMessage: null
      };

      const response = await request.patch(`/api/pipeline/status/${pipelineId}`, {
        data: statusUpdate
      });
      
      // May succeed or fail depending on pipeline state
      if (response.ok()) {
        const updatedPipeline = await response.json();
        expect(updatedPipeline).toHaveProperty('id');
        expect(updatedPipeline).toHaveProperty('status');
      } else {
        expect(response.status()).toBe(400);
      }
    });

    test('should validate status transitions', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const invalidStatusUpdate = {
        status: 'InvalidStatus',
        errorMessage: null
      };

      const response = await request.patch(`/api/pipeline/status/${pipelineId}`, {
        data: invalidStatusUpdate
      });
      
      expect(response.status()).toBe(400);
      const error = await response.json();
      expect(error).toHaveProperty('title');
    });

    test('should get next executable stages', async ({ request }) => {
      expect(pipelineId).toBeTruthy();

      const response = await request.get(`/api/pipeline/execution/${pipelineId}/next-stages`);
      
      expect(response.ok()).toBeTruthy();
      const stages = await response.json();
      
      expect(Array.isArray(stages)).toBeTruthy();
      // Stages array may be empty if none are executable
    });
  });

  test.describe('Request Validation', () => {
    test('should validate required fields for pipeline start', async ({ request }) => {
      const invalidRequest = {
        // Missing required fields
        isDryRun: false
      };

      const response = await request.post('/api/pipeline/start', {
        data: invalidRequest
      });
      
      expect(response.status()).toBe(400);
    });

    test('should validate GUID format', async ({ request }) => {
      const invalidGuid = 'not-a-guid';
      
      const response = await request.get(`/api/pipeline/execution/${invalidGuid}`);
      
      expect(response.status()).toBe(404);
    });

    test('should validate string length limits', async ({ request }) => {
      const longInitiatedBy = 'a'.repeat(300); // Exceeds 255 char limit
      
      const invalidRequest = {
        productId: testProductId,
        initiatedBy: longInitiatedBy,
        isDryRun: false
      };

      const response = await request.post('/api/pipeline/start', {
        data: invalidRequest
      });
      
      expect(response.status()).toBe(400);
    });
  });
});