import { test, expect } from '@playwright/test';
import { ApiTestHelpers, TestConstants } from '../../utils/test-helpers';
import testData from '../../fixtures/test-data.json';

test.describe('Connection Strings API', () => {
  let apiHelpers: ApiTestHelpers;
  let testProductId: string;

  test.beforeAll(async ({ request }) => {
    apiHelpers = new ApiTestHelpers(request);
    
    // Create a test product
    const product = await apiHelpers.createProduct(testData.products[0]);
    testProductId = product.id;
  });

  test.afterAll(async ({ request }) => {
    // Clean up test product
    if (testProductId) {
      await apiHelpers.deleteProduct(testProductId);
    }
  });

  test('should create connection string', async ({ request }) => {
    const connectionData = {
      productId: testProductId,
      connectionType: 'Source',
      connectionString: TestConstants.TEST_CONNECTION_STRING,
      description: 'Test source connection'
    };

    const response = await request.post(`/api/products/${testProductId}/connections`, {
      data: connectionData
    });

    expect(response.ok()).toBeTruthy();
    const connection = await response.json();
    expect(connection.connectionType).toBe('Source');
    expect(connection.description).toBe('Test source connection');
    
    // Verify password is masked in response
    await apiHelpers.verifyConnectionStringMasked(connection.connectionString);
  });

  test('should get all connections for product', async ({ request }) => {
    // First create some test connections
    for (const connData of testData.connectionStrings) {
      await request.post(`/api/products/${testProductId}/connections`, {
        data: {
          productId: testProductId,
          ...connData
        }
      });
    }

    const response = await request.get(`/api/products/${testProductId}/connections`);
    expect(response.ok()).toBeTruthy();
    
    const connections = await response.json();
    expect(connections).toHaveLength(3);
    expect(connections.map((c: any) => c.connectionType)).toEqual(
      expect.arrayContaining(['Source', 'Target', 'Backup'])
    );

    // Verify all passwords are masked
    for (const connection of connections) {
      await apiHelpers.verifyConnectionStringMasked(connection.connectionString);
    }
  });

  test('should update connection string', async ({ request }) => {
    // Create a connection first
    const createResponse = await request.post(`/api/products/${testProductId}/connections`, {
      data: {
        productId: testProductId,
        connectionType: 'Source',
        connectionString: TestConstants.TEST_CONNECTION_STRING,
        description: 'Original description'
      }
    });
    
    const connection = await createResponse.json();
    const connectionId = connection.id;

    // Update the connection
    const updateData = {
      connectionType: 'Source',
      connectionString: 'Server=updated.db;Database=test;User=test;Password=newpass;',
      description: 'Updated description'
    };

    const updateResponse = await request.put(`/api/products/${testProductId}/connections/${connectionId}`, {
      data: updateData
    });

    expect(updateResponse.ok()).toBeTruthy();
    const updatedConnection = await updateResponse.json();
    expect(updatedConnection.description).toBe('Updated description');
    
    // Verify password is still masked
    await apiHelpers.verifyConnectionStringMasked(updatedConnection.connectionString);
  });

  test('should test connection', async ({ request }) => {
    // Create a test connection
    const createResponse = await request.post(`/api/products/${testProductId}/connections`, {
      data: {
        productId: testProductId,
        connectionType: 'Source',
        connectionString: TestConstants.TEST_CONNECTION_STRING,
        description: 'Test connection'
      }
    });
    
    const connection = await createResponse.json();
    const connectionId = connection.id;

    // Test the connection
    const testResponse = await request.post(`/api/products/${testProductId}/connections/${connectionId}/test`);
    
    expect(testResponse.ok()).toBeTruthy();
    const testResult = await testResponse.json();
    expect(testResult).toHaveProperty('success');
    expect(testResult).toHaveProperty('message');
    
    // Note: In real implementation, this might fail if the connection string is invalid
    // But we're testing the API structure here
  });

  test('should delete connection string', async ({ request }) => {
    // Create a connection to delete
    const createResponse = await request.post(`/api/products/${testProductId}/connections`, {
      data: {
        productId: testProductId,
        connectionType: 'Source',
        connectionString: TestConstants.TEST_CONNECTION_STRING,
        description: 'Connection to delete'
      }
    });
    
    const connection = await createResponse.json();
    const connectionId = connection.id;

    // Delete the connection
    const deleteResponse = await request.delete(`/api/products/${testProductId}/connections/${connectionId}`);
    expect(deleteResponse.ok()).toBeTruthy();

    // Verify it's deleted
    const getResponse = await request.get(`/api/products/${testProductId}/connections/${connectionId}`);
    expect(getResponse.status()).toBe(404);
  });

  test('should validate connection string format', async ({ request }) => {
    const invalidConnectionData = {
      productId: testProductId,
      connectionType: 'Source',
      connectionString: 'invalid-connection-string',
      description: 'Invalid connection'
    };

    const response = await request.post(`/api/products/${testProductId}/connections`, {
      data: invalidConnectionData
    });

    // Should return validation error
    expect(response.status()).toBe(400);
    const error = await response.json();
    expect(error).toHaveProperty('message');
  });

  test('should handle connection string encryption/decryption', async ({ request }) => {
    const connectionData = {
      productId: testProductId,
      connectionType: 'Source',
      connectionString: 'Server=secure.db;Database=test;User=test;Password=supersecret123;',
      description: 'Encrypted connection test'
    };

    // Create connection
    const createResponse = await request.post(`/api/products/${testProductId}/connections`, {
      data: connectionData
    });

    expect(createResponse.ok()).toBeTruthy();
    const connection = await createResponse.json();
    
    // Verify the password is masked but other parts are visible
    expect(connection.connectionString).toContain('Server=secure.db');
    expect(connection.connectionString).toContain('Database=test');
    expect(connection.connectionString).toContain('User=test');
    expect(connection.connectionString).toMatch(/Password=\*+/);
    expect(connection.connectionString).not.toContain('supersecret123');
  });

});