import { test, expect } from '@playwright/test';
import { APITestHelper } from '../utils/api-test-helper';

test.describe('SQL Script Security Validation', () => {
  let apiHelper: APITestHelper;

  test.beforeAll(async ({ request }) => {
    apiHelper = new APITestHelper(request);
    
    // Verify API is accessible
    const healthCheck = await apiHelper.healthCheck();
    expect(healthCheck).toBe(true);
  });

  test.describe('Security Pattern Detection', () => {
    test('should detect dangerous xp_cmdshell operations', async () => {
      const dangerousScript = 'EXEC xp_cmdshell "dir"; SELECT * FROM Users;';
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: dangerousScript }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.isValid).toBe(false);
      expect(result.riskLevel).toBe('Critical');
      expect(result.securityIssues).toEqual(
        expect.arrayContaining([
          expect.objectContaining({
            issueType: 'DangerousOperation',
            severity: 'Critical',
            description: expect.stringContaining('xp_cmdshell')
          })
        ])
      );
    });

    test('should detect OPENROWSET operations', async () => {
      const dangerousScript = "SELECT * FROM OPENROWSET('SQLNCLI', 'Server=server;Trusted_Connection=yes;', 'SELECT * FROM table')";
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: dangerousScript }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.isValid).toBe(false);
      expect(result.riskLevel).toBe('Critical');
      expect(result.securityIssues.some((issue: any) => 
        issue.description.toLowerCase().includes('openrowset')
      )).toBe(true);
    });

    test('should detect BULK INSERT operations', async () => {
      const dangerousScript = "BULK INSERT Users FROM 'c:\\users.txt' WITH (FIELDTERMINATOR = ',');";
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: dangerousScript }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.isValid).toBe(false);
      expect(result.securityIssues.some((issue: any) => 
        issue.description.toLowerCase().includes('bulk insert')
      )).toBe(true);
    });

    test('should detect SHUTDOWN commands', async () => {
      const dangerousScript = 'SELECT * FROM Users; SHUTDOWN;';
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: dangerousScript }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.isValid).toBe(false);
      expect(result.securityIssues.some((issue: any) => 
        issue.description.toLowerCase().includes('shutdown')
      )).toBe(true);
    });
  });

  test.describe('DDL and DCL Operation Detection', () => {
    test('should detect DDL operations when not allowed', async () => {
      const ddlScript = 'CREATE TABLE TestTable (Id INT, Name VARCHAR(50)); ALTER TABLE Users ADD NewColumn INT;';
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { 
          sqlScript: ddlScript,
          allowDDL: false
        }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.containsDDL).toBe(true);
      expect(result.isValid).toBe(false);
      expect(result.securityIssues.some((issue: any) => 
        issue.issueType === 'DDLNotAllowed'
      )).toBe(true);
    });

    test('should allow DDL operations when explicitly permitted', async () => {
      const ddlScript = 'CREATE TABLE TestTable (Id INT, Name VARCHAR(50));';
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { 
          sqlScript: ddlScript,
          allowDDL: true
        }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.containsDDL).toBe(true);
      // Should be valid since DDL is allowed and no dangerous operations
      expect(result.isValid).toBe(true);
    });

    test('should detect DCL operations', async () => {
      const dclScript = 'GRANT SELECT ON Users TO TestUser; REVOKE INSERT ON Products FROM TestUser;';
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { 
          sqlScript: dclScript,
          allowDCL: false
        }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.containsDCL).toBe(true);
      expect(result.isValid).toBe(false);
      expect(result.securityIssues.some((issue: any) => 
        issue.issueType === 'DCLNotAllowed'
      )).toBe(true);
    });
  });

  test.describe('Script Complexity Analysis', () => {
    test('should classify simple scripts as low complexity', async () => {
      const simpleScript = 'SELECT COUNT(*) FROM Users WHERE IsActive = 1';
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: simpleScript }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.executionComplexity).toBe('Low');
      expect(result.isValid).toBe(true);
      expect(result.containsDML).toBe(true);
      expect(result.containsDDL).toBe(false);
      expect(result.containsDCL).toBe(false);
    });

    test('should classify complex scripts appropriately', async () => {
      const complexScript = `
        WITH RECURSIVE UserHierarchy AS (
          SELECT Id, Name, ParentId, 1 as Level FROM Users WHERE ParentId IS NULL
          UNION ALL
          SELECT u.Id, u.Name, u.ParentId, uh.Level + 1 
          FROM Users u 
          JOIN UserHierarchy uh ON u.ParentId = uh.Id
        )
        SELECT * FROM UserHierarchy WHERE Level > 2
      `;
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: complexScript }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(['Medium', 'High']).toContain(result.executionComplexity);
    });
  });

  test.describe('Script Sanitization', () => {
    test('should handle scripts with comments', async () => {
      const scriptWithComments = `
        -- This is a comment
        SELECT COUNT(*) FROM Users 
        /* Multi-line comment
           spanning multiple lines */
        WHERE IsActive = 1; -- End comment
      `;
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: scriptWithComments }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      // Should warn about comments but still be valid
      expect(result.isValid).toBe(true);
      expect(result.warnings.some((warning: string) => 
        warning.toLowerCase().includes('comment')
      )).toBe(true);
    });

    test('should handle empty or whitespace-only scripts', async () => {
      const emptyScripts = ['', '   ', '\n\n\t  \n'];
      
      for (const emptyScript of emptyScripts) {
        const response = await apiHelper.request.post('/api/script-validation/validate', {
          data: { sqlScript: emptyScript }
        });
        
        expect(response.status()).toBe(200);
        const result = await response.json();
        
        expect(result.isValid).toBe(false);
        expect(result.securityIssues.some((issue: any) => 
          issue.issueType === 'EmptyScript'
        )).toBe(true);
      }
    });
  });

  test.describe('Dry-Run Validation', () => {
    test('should perform dry-run validation with connection string', async () => {
      const validConnectionString = 'Server=test-server;Database=test-db;User Id=test-user;Password=test-pass;';
      const safeScript = 'SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES';
      
      const response = await apiHelper.request.post('/api/script-validation/dry-run', {
        data: {
          sqlScript: safeScript,
          connectionString: validConnectionString,
          allowDDL: false,
          allowDCL: false
        }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result).toHaveProperty('isValid');
      expect(result).toHaveProperty('notes');
      
      // Dry run will likely fail due to connection, but should handle gracefully
      if (!result.isValid && result.securityIssues.some((issue: any) => issue.issueType === 'ConnectionError')) {
        console.log('Expected connection error during dry-run test');
      }
    });

    test('should reject dangerous scripts in dry-run', async () => {
      const connectionString = 'Server=test;Database=test;User Id=test;Password=test;';
      
      const response = await apiHelper.request.post('/api/script-validation/dry-run', {
        data: {
          sqlScript: dangerousScript,
          connectionString: connectionString
        }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.isValid).toBe(false);
      expect(result.notes).toContain('security validation');
    });
  });

  test.describe('Performance and Edge Cases', () => {
    test('should handle very long scripts', async () => {
      const longScript = 'SELECT * FROM Users WHERE Id IN (' + 
        Array.from({length: 1000}, (_, i) => i).join(',') + ')';
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: longScript }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result).toHaveProperty('isValid');
      expect(result).toHaveProperty('executionComplexity');
    });

    test('should handle scripts with special characters', async () => {
      const specialScript = "SELECT * FROM Users WHERE Name LIKE '%O''Reilly%' AND Email CONTAINS '@domain.com'";
      
      const response = await apiHelper.request.post('/api/script-validation/validate', {
        data: { sqlScript: specialScript }
      });
      
      expect(response.status()).toBe(200);
      const result = await response.json();
      
      expect(result.isValid).toBe(true);
    });
  });
});