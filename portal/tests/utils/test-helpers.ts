import { APIRequestContext, expect } from '@playwright/test';

export class ApiTestHelpers {
  constructor(private request: APIRequestContext) {}

  async createProduct(productData: any) {
    const response = await this.request.post('/api/products', {
      data: productData
    });
    expect(response.ok()).toBeTruthy();
    return await response.json();
  }

  async getProduct(productId: string) {
    const response = await this.request.get(`/api/products/${productId}`);
    expect(response.ok()).toBeTruthy();
    return await response.json();
  }

  async deleteProduct(productId: string) {
    const response = await this.request.delete(`/api/products/${productId}`);
    expect(response.ok()).toBeTruthy();
  }

  async verifyConnectionStringMasked(connectionString: string) {
    expect(connectionString).toMatch(/Password=\*+/);
    expect(connectionString).not.toMatch(/Password=[^*]/);
  }

  async waitForPipelineExecution(executionId: string, expectedStatus: string, timeoutMs = 30000) {
    const startTime = Date.now();
    while (Date.now() - startTime < timeoutMs) {
      const response = await this.request.get(`/api/pipeline/executions/${executionId}`);
      const execution = await response.json();
      if (execution.status === expectedStatus) {
        return execution;
      }
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    throw new Error(`Pipeline execution did not reach ${expectedStatus} status within ${timeoutMs}ms`);
  }
}

export class DatabaseTestHelpers {
  static async validateSchemaExists(tableName: string): Promise<boolean> {
    // This would connect to test database and verify table exists
    // Implementation would depend on database connection setup
    return true;
  }

  static async cleanupTestData(productId: string): Promise<void> {
    // Clean up test data after tests
    // Implementation would depend on database connection setup
  }
}

export const TestConstants = {
  TEST_PRODUCT_NAME: 'Test Product',
  TEST_CONNECTION_STRING: 'Server=test.db;Database=test;User=test;Password=testpass;',
  MASKED_PASSWORD_PATTERN: /Password=\*+/,
  API_TIMEOUT: 30000,
  PIPELINE_TIMEOUT: 300000
};