import { test, expect } from '@playwright/test';

test.describe('Stage API Direct Integration Tests', () => {
  test.describe('Stage Endpoint Structure Validation', () => {
    test('should have correct endpoint structure for stage operations', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      // Test start stage endpoint structure
      const startResponse = await request.post(`/api/pipeline/stage/start/${testStageId}`);
      expect([400, 404]).toContain(startResponse.status());
      expect(startResponse.status()).not.toBe(405); // Method Not Allowed

      // Test execute stage endpoint structure  
      const executeResponse = await request.post(`/api/pipeline/stage/execute/${testStageId}`);
      expect([400, 404]).toContain(executeResponse.status());
      expect(executeResponse.status()).not.toBe(405);

      // Test get stage endpoint structure
      const getResponse = await request.get(`/api/pipeline/stage/${testStageId}`);
      expect(getResponse.status()).toBe(404);
      
      const error = await getResponse.json();
      expect(error).toHaveProperty('title');
      expect(error.title).toBe('Stage Not Found');
    });

    test('should validate status update request format', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const invalidStatusUpdate = {
        status: 'InvalidStatusThatIsTooLong', // Exceeds 20 char limit
        errorMessage: null
      };

      const response = await request.patch(`/api/pipeline/stage/status/${testStageId}`, {
        data: invalidStatusUpdate
      });
      
      expect(response.status()).toBe(400);
    });

    test('should validate progress update request format', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const invalidProgressUpdate = {
        progressPercentage: 150, // Invalid: > 100
        rowsProcessed: 1000
      };

      const response = await request.patch(`/api/pipeline/stage/progress/${testStageId}`, {
        data: invalidProgressUpdate
      });
      
      expect(response.status()).toBe(400);
    });

    test('should validate negative rows processed', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const invalidProgressUpdate = {
        progressPercentage: 50,
        rowsProcessed: -100 // Invalid: negative value
      };

      const response = await request.patch(`/api/pipeline/stage/progress/${testStageId}`, {
        data: invalidProgressUpdate
      });
      
      expect(response.status()).toBe(400);
    });

    test('should validate metrics update request format', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      // Test with negative values
      const invalidMetricsUpdate = {
        rowsProcessed: -1, // Invalid: negative
        rowsAffected: 1000,
        memoryUsageMB: 256.5,
        cpuUsagePercentage: 150 // Invalid: > 100
      };

      const response = await request.patch(`/api/pipeline/stage/metrics/${testStageId}`, {
        data: invalidMetricsUpdate
      });
      
      expect(response.status()).toBe(400);
    });

    test('should require all metrics fields', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const incompleteMetricsUpdate = {
        rowsProcessed: 1000
        // Missing required fields: rowsAffected, memoryUsageMB, cpuUsagePercentage
      };

      const response = await request.patch(`/api/pipeline/stage/metrics/${testStageId}`, {
        data: incompleteMetricsUpdate
      });
      
      expect(response.status()).toBe(400);
    });

    test('should validate error message length limit', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const longErrorMessage = {
        status: 'Failed',
        errorMessage: 'A'.repeat(1001) // Exceeds 1000 char limit
      };

      const response = await request.patch(`/api/pipeline/stage/status/${testStageId}`, {
        data: longErrorMessage
      });
      
      expect(response.status()).toBe(400);
    });

    test('should have transition check endpoint structure', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      const newStatus = 'Running';
      
      const response = await request.get(`/api/pipeline/stage/${testStageId}/can-transition/${newStatus}`);
      
      // Should return 500 (service error) for non-existent stage, not 405 (method not allowed)
      expect(response.status()).toBe(500);
      expect(response.status()).not.toBe(405);
    });

    test('should validate GUID format in URLs', async ({ request }) => {
      const invalidGuid = 'not-a-valid-guid-format';
      
      const response = await request.get(`/api/pipeline/stage/${invalidGuid}`);
      
      // Should return 404 for invalid GUID format
      expect(response.status()).toBe(404);
    });
  });

  test.describe('Stage Status and Progress Validation', () => {
    test('should validate valid stage status values', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const validStatuses = ['Pending', 'Running', 'Completed', 'Failed', 'Cancelled'];
      
      for (const status of validStatuses) {
        const statusUpdate = {
          status: status,
          errorMessage: null
        };

        const response = await request.patch(`/api/pipeline/stage/status/${testStageId}`, {
          data: statusUpdate
        });
        
        // Should not fail due to invalid status (404 or 500 are acceptable for non-existent stage)
        expect([404, 500]).toContain(response.status());
        expect(response.status()).not.toBe(400); // Not a validation error
      }
    });

    test('should reject invalid stage status values', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const invalidStatuses = ['InvalidStatus', 'RUNNING', 'random', ''];
      
      for (const status of invalidStatuses) {
        const statusUpdate = {
          status: status,
          errorMessage: null
        };

        const response = await request.patch(`/api/pipeline/stage/status/${testStageId}`, {
          data: statusUpdate
        });
        
        // Should fail with validation error for invalid status
        expect(response.status()).toBe(400);
      }
    });

    test('should validate progress percentage bounds', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      // Test boundary values
      const testCases = [
        { progressPercentage: 0, shouldPass: true },
        { progressPercentage: 100, shouldPass: true },
        { progressPercentage: 50.5, shouldPass: true },
        { progressPercentage: -0.1, shouldPass: false },
        { progressPercentage: 100.1, shouldPass: false },
        { progressPercentage: 200, shouldPass: false }
      ];
      
      for (const testCase of testCases) {
        const progressUpdate = {
          progressPercentage: testCase.progressPercentage,
          rowsProcessed: 1000
        };

        const response = await request.patch(`/api/pipeline/stage/progress/${testStageId}`, {
          data: progressUpdate
        });
        
        if (testCase.shouldPass) {
          // Should not fail due to validation (404 or 500 acceptable for non-existent stage)
          expect([404, 500]).toContain(response.status());
        } else {
          // Should fail with validation error
          expect(response.status()).toBe(400);
        }
      }
    });

    test('should validate metrics value bounds', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const validMetrics = {
        rowsProcessed: 1000,
        rowsAffected: 900,
        memoryUsageMB: 256.5,
        cpuUsagePercentage: 75.2
      };

      const response = await request.patch(`/api/pipeline/stage/metrics/${testStageId}`, {
        data: validMetrics
      });
      
      // Should not fail due to validation (404 or 500 acceptable for non-existent stage)
      expect([404, 500]).toContain(response.status());
      expect(response.status()).not.toBe(400);
    });
  });

  test.describe('API Response Format Validation', () => {
    test('should return consistent error format for validation errors', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const invalidRequest = {
        status: 'TooLongStatusThatExceedsLimit',
        errorMessage: null
      };

      const response = await request.patch(`/api/pipeline/stage/status/${testStageId}`, {
        data: invalidRequest
      });
      
      expect(response.status()).toBe(400);
      
      if (response.headers()['content-type']?.includes('json')) {
        const error = await response.json();
        
        // Should follow Problem Details RFC format
        expect(error).toHaveProperty('title');
        expect(error).toHaveProperty('status');
        expect(error.status).toBe(400);
      }
    });

    test('should return consistent error format for not found errors', async ({ request }) => {
      const nonExistentStageId = '00000000-0000-0000-0000-000000000000';
      
      const response = await request.get(`/api/pipeline/stage/${nonExistentStageId}`);
      
      expect(response.status()).toBe(404);
      
      if (response.headers()['content-type']?.includes('json')) {
        const error = await response.json();
        
        // Should follow Problem Details RFC format
        expect(error).toHaveProperty('title');
        expect(error).toHaveProperty('detail');
        expect(error.title).toBe('Stage Not Found');
      }
    });

    test('should handle CORS for stage endpoints', async ({ request }) => {
      const testStageId = '00000000-0000-0000-0000-000000000000';
      
      const response = await request.get(`/api/pipeline/stage/${testStageId}`);
      
      // Verify CORS headers are present (configured in Program.cs)
      const headers = response.headers();
      // Note: In test environment, CORS headers might not be visible
      // This test mainly ensures the endpoint is accessible cross-origin
      expect(response.status()).not.toBe(403); // Not forbidden due to CORS
    });
  });
});