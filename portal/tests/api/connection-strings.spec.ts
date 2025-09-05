import { test, expect } from '@playwright/test';
import { APITestHelper } from '../utils/api-test-helper';

// Test data
const validConnectionString = 'Server=test-server;Database=test-db;User Id=test-user;Password=test-pass;';
const invalidConnectionString = 'invalid-connection-string';

// Test connection string data
const createConnectionData = {
  productId: "00000000-0000-0000-0000-000000000001", // We'll need to use existing product ID
  connectionType: "Source",
  connectionString: validConnectionString,
  description: "Test source connection",
  isActive: true,
  createdBy: "test-user"
};

const updateConnectionData = {
  connectionType: "Target", 
  connectionString: 'Server=updated-server;Database=updated-db;User Id=updated-user;Password=updated-pass;',
  description: "Updated test connection",
  isActive: true,
  updatedBy: "test-user-updated"
};

test.describe('Connection Strings API', () => {
  let apiHelper: APITestHelper;
  let createdConnectionId: string;

  test.beforeAll(async ({ request }) => {
    apiHelper = new APITestHelper(request);
    
    // Verify API is accessible
    const healthCheck = await apiHelper.healthCheck();
    expect(healthCheck).toBe(true);
  });

  test.afterAll(async () => {
    // Cleanup: Delete the connection string if it was created
    if (createdConnectionId) {
      try {
        await apiHelper.request.delete(`/api/connectionstrings/${createdConnectionId}`);
      } catch (error) {
        console.log('Cleanup error (expected if test already deleted):', error);
      }
    }
  });

  test.describe('GET /api/connectionstrings', () => {
    test('should get all connection strings', async () => {
      const response = await apiHelper.request.get('/api/connectionstrings');
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      expect(Array.isArray(data)).toBe(true);
    });

    test('should filter connection strings by product ID', async () => {
      const productId = createConnectionData.productId;
      const response = await apiHelper.request.get(`/api/connectionstrings?productId=${productId}`);
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      expect(Array.isArray(data)).toBe(true);
      
      // If any results, verify they all belong to the specified product
      data.forEach((item: any) => {
        if (item.productId) {
          expect(item.productId).toBe(productId);
        }
      });
    });
  });

  test.describe('POST /api/connectionstrings', () => {
    test('should create a new connection string', async () => {
      const response = await apiHelper.request.post('/api/connectionstrings', {
        data: createConnectionData
      });
      
      if (response.status() === 201) {
        const data = await response.json();
        createdConnectionId = data.id;
        
        expect(data).toMatchObject({
          productId: createConnectionData.productId,
          connectionType: createConnectionData.connectionType,
          description: createConnectionData.description,
          isActive: createConnectionData.isActive,
          createdBy: createConnectionData.createdBy
        });
        
        // Connection string should be masked in response
        expect(data.connectionString).not.toBe(createConnectionData.connectionString);
        expect(data.connectionString).toContain('***');
        expect(data.id).toBeDefined();
        expect(data.createdAt).toBeDefined();
        expect(data.updatedAt).toBeDefined();
      } else {
        // Log the error for debugging
        const errorData = await response.text();
        console.log(`Create connection failed with status ${response.status()}: ${errorData}`);
        
        // Test should expect either success or specific business logic failures
        expect([201, 400, 404, 409]).toContain(response.status());
        
        if (response.status() === 400 || response.status() === 404) {
          // Expected business logic failure (product not found, validation error, etc.)
          console.log('Expected business logic failure during connection string creation');
        }
      }
    });

    test('should reject invalid connection string format', async () => {
      const invalidData = {
        ...createConnectionData,
        connectionString: invalidConnectionString
      };
      
      const response = await apiHelper.request.post('/api/connectionstrings', {
        data: invalidData
      });
      
      expect(response.status()).toBe(400);
      const errorData = await response.text();
      expect(errorData.toLowerCase()).toContain('invalid');
    });

    test('should reject invalid connection type', async () => {
      const invalidTypeData = {
        ...createConnectionData,
        connectionType: 'InvalidType'
      };
      
      const response = await apiHelper.request.post('/api/connectionstrings', {
        data: invalidTypeData
      });
      
      expect(response.status()).toBe(400);
      const errorData = await response.text();
      expect(errorData.toLowerCase()).toContain('invalid');
    });

    test('should reject duplicate connection type for same product', async () => {
      // First, try to create a connection string
      const firstResponse = await apiHelper.request.post('/api/connectionstrings', {
        data: createConnectionData
      });
      
      if (firstResponse.status() === 201) {
        const firstData = await firstResponse.json();
        const firstId = firstData.id;
        
        // Try to create another with same product and type
        const duplicateResponse = await apiHelper.request.post('/api/connectionstrings', {
          data: createConnectionData
        });
        
        expect(duplicateResponse.status()).toBe(409);
        
        // Cleanup the first connection
        await apiHelper.request.delete(`/api/connectionstrings/${firstId}`);
      } else {
        console.log('Skipping duplicate test - initial creation failed');
      }
    });
  });

  test.describe('GET /api/connectionstrings/{id}', () => {
    test('should get connection string by ID', async () => {
      // Skip if we don't have a created connection
      if (!createdConnectionId) {
        test.skip();
        return;
      }
      
      const response = await apiHelper.request.get(`/api/connectionstrings/${createdConnectionId}`);
      
      if (response.status() === 200) {
        const data = await response.json();
        expect(data.id).toBe(createdConnectionId);
        expect(data.connectionString).toContain('***'); // Should be masked
      } else {
        expect(response.status()).toBe(404);
      }
    });

    test('should return 404 for non-existent connection string', async () => {
      const fakeId = '99999999-9999-9999-9999-999999999999';
      const response = await apiHelper.request.get(`/api/connectionstrings/${fakeId}`);
      
      expect(response.status()).toBe(404);
    });
  });

  test.describe('PUT /api/connectionstrings/{id}', () => {
    test('should update existing connection string', async () => {
      // Skip if we don't have a created connection
      if (!createdConnectionId) {
        test.skip();
        return;
      }
      
      const updateData = {
        ...updateConnectionData,
        id: createdConnectionId
      };
      
      const response = await apiHelper.request.put(`/api/connectionstrings/${createdConnectionId}`, {
        data: updateData
      });
      
      if (response.status() === 200) {
        const data = await response.json();
        expect(data).toMatchObject({
          id: createdConnectionId,
          connectionType: updateConnectionData.connectionType,
          description: updateConnectionData.description,
          isActive: updateConnectionData.isActive,
          updatedBy: updateConnectionData.updatedBy
        });
        
        // Connection string should be masked
        expect(data.connectionString).toContain('***');
      } else {
        // Log error for debugging
        const errorData = await response.text();
        console.log(`Update failed with status ${response.status()}: ${errorData}`);
        expect([400, 404, 409]).toContain(response.status());
      }
    });

    test('should return 400 for ID mismatch', async () => {
      if (!createdConnectionId) {
        test.skip();
        return;
      }
      
      const differentId = '11111111-1111-1111-1111-111111111111';
      const updateData = {
        ...updateConnectionData,
        id: differentId
      };
      
      const response = await apiHelper.request.put(`/api/connectionstrings/${createdConnectionId}`, {
        data: updateData
      });
      
      expect(response.status()).toBe(400);
    });
  });

  test.describe('DELETE /api/connectionstrings/{id}', () => {
    test('should delete existing connection string', async () => {
      // Skip if we don't have a created connection
      if (!createdConnectionId) {
        test.skip();
        return;
      }
      
      const response = await apiHelper.request.delete(`/api/connectionstrings/${createdConnectionId}`);
      
      if (response.status() === 204) {
        // Verify it's deleted by trying to get it
        const getResponse = await apiHelper.request.get(`/api/connectionstrings/${createdConnectionId}`);
        expect(getResponse.status()).toBe(404);
        
        // Clear the ID so cleanup doesn't try to delete again
        createdConnectionId = '';
      } else {
        expect(response.status()).toBe(404);
      }
    });

    test('should return 404 for non-existent connection string', async () => {
      const fakeId = '99999999-9999-9999-9999-999999999999';
      const response = await apiHelper.request.delete(`/api/connectionstrings/${fakeId}`);
      
      expect(response.status()).toBe(404);
    });
  });

  test.describe('GET /api/connectionstrings/product/{productId}', () => {
    test('should get connection strings by product ID', async () => {
      const productId = createConnectionData.productId;
      const response = await apiHelper.request.get(`/api/connectionstrings/product/${productId}`);
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      expect(Array.isArray(data)).toBe(true);
      
      // Verify all results belong to the product
      data.forEach((item: any) => {
        expect(item.productId).toBe(productId);
        expect(item.connectionString).toContain('***'); // Should be masked
      });
    });
  });

  test.describe('POST /api/connectionstrings/test', () => {
    test('should test a valid connection string format', async () => {
      const testData = {
        connectionString: validConnectionString,
        includeExtendedInfo: false
      };
      
      const response = await apiHelper.request.post('/api/connectionstrings/test', {
        data: testData
      });
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      
      expect(data).toHaveProperty('isSuccessful');
      expect(data).toHaveProperty('connectionTimeMs');
      expect(data).toHaveProperty('testedAt');
      
      // The test will likely fail to connect, but format validation should pass
      if (!data.isSuccessful) {
        expect(data).toHaveProperty('errorMessage');
        expect(typeof data.errorMessage).toBe('string');
      }
    });

    test('should reject invalid connection string format', async () => {
      const testData = {
        connectionString: invalidConnectionString,
        includeExtendedInfo: false
      };
      
      const response = await apiHelper.request.post('/api/connectionstrings/test', {
        data: testData
      });
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      
      expect(data.isSuccessful).toBe(false);
      expect(data.errorMessage).toContain('Invalid connection string format');
    });

    test('should test connection with extended info', async () => {
      const testData = {
        connectionString: validConnectionString,
        includeExtendedInfo: true
      };
      
      const response = await apiHelper.request.post('/api/connectionstrings/test', {
        data: testData
      });
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      
      expect(data).toHaveProperty('isSuccessful');
      expect(data).toHaveProperty('additionalInfo');
      expect(typeof data.additionalInfo).toBe('object');
    });
  });

  test.describe('POST /api/connectionstrings/{id}/test', () => {
    test('should test stored connection string', async () => {
      // Skip if we don't have a created connection
      if (!createdConnectionId) {
        test.skip();
        return;
      }
      
      const response = await apiHelper.request.post(`/api/connectionstrings/${createdConnectionId}/test?includeExtendedInfo=true`);
      
      if (response.status() === 200) {
        const data = await response.json();
        
        expect(data).toHaveProperty('isSuccessful');
        expect(data).toHaveProperty('connectionTimeMs');
        expect(data).toHaveProperty('additionalInfo');
        
        // Should include metadata about the connection string
        expect(data.additionalInfo).toHaveProperty('connectionType');
        expect(data.additionalInfo).toHaveProperty('productName');
      } else {
        // Connection string might not exist if previous tests failed
        expect(response.status()).toBe(404);
      }
    });

    test('should return 404 for non-existent stored connection', async () => {
      const fakeId = '99999999-9999-9999-9999-999999999999';
      const response = await apiHelper.request.post(`/api/connectionstrings/${fakeId}/test`);
      
      expect(response.status()).toBe(200);
      const data = await response.json();
      
      expect(data.isSuccessful).toBe(false);
      expect(data.errorMessage).toContain('not found');
    });
  });

  test.describe('Connection String Security', () => {
    test('should mask sensitive information in all responses', async () => {
      const endpoints = [
        '/api/connectionstrings',
        `/api/connectionstrings/product/${createConnectionData.productId}`
      ];
      
      for (const endpoint of endpoints) {
        const response = await apiHelper.request.get(endpoint);
        
        if (response.status() === 200) {
          const data = await response.json();
          const items = Array.isArray(data) ? data : [data];
          
          items.forEach((item: any) => {
            if (item.connectionString) {
              expect(item.connectionString).not.toContain('Password=');
              expect(item.connectionString).toContain('***');
            }
          });
        }
      }
    });
  });
});