import { test, expect } from '@playwright/test';
import { DatabaseTestHelpers } from '../../utils/test-helpers';

test.describe('Database Schema Validation', () => {
  
  test('should have all required existing tables', async ({ request }) => {
    const expectedTables = [
      'Products',
      'DatabaseSchemas', 
      'TableColumns',
      'ColumnObfuscationMappings',
      'ObfuscationConfigurations',
      'Users',
      'Roles',
      'UserProductPermissions',
      'AuditLog',
      'SchemaAnalysisSessions',
      'SupportedDataTypes',
      'PIIDetectionRules'
    ];

    // Verify tables exist via API call that queries database
    const response = await request.get('/api/admin/database/tables');
    expect(response.ok()).toBeTruthy();
    
    const tables = await response.json();
    const tableNames = tables.map((t: any) => t.tableName);
    
    for (const tableName of expectedTables) {
      expect(tableNames).toContain(tableName, `Table ${tableName} should exist in database`);
    }
  });

  test('should have all new pipeline tables after Phase 1.1', async ({ request }) => {
    const newPipelineTables = [
      'PipelineStages',
      'ProductConnectionStrings', 
      'ProductPipelineConfigurations',
      'PipelineStageScripts',
      'PipelineExecutions',
      'StageExecutions',
      'ScriptExecutionLogs'
    ];

    // Verify new pipeline tables exist
    const response = await request.get('/api/admin/database/tables');
    expect(response.ok()).toBeTruthy();
    
    const tables = await response.json();
    const tableNames = tables.map((t: any) => t.tableName);
    
    for (const tableName of newPipelineTables) {
      expect(tableNames).toContain(tableName, `Pipeline table ${tableName} should exist after schema extension`);
    }
  });

  test('should have default pipeline stages data', async ({ request }) => {
    const expectedStages = [
      { stageName: 'DataReduction', stageOrder: 1 },
      { stageName: 'PreObfuscation', stageOrder: 2 },
      { stageName: 'Obfuscation', stageOrder: 3 },
      { stageName: 'PostObfuscation', stageOrder: 4 },
      { stageName: 'Testing', stageOrder: 5 }
    ];
    
    // Get pipeline stages from API
    const response = await request.get('/api/pipeline/stages');
    expect(response.ok()).toBeTruthy();
    
    const stages = await response.json();
    expect(stages).toHaveLength(5);
    
    // Verify each expected stage exists with correct order
    for (const expectedStage of expectedStages) {
      const stage = stages.find((s: any) => s.stageName === expectedStage.stageName);
      expect(stage).toBeDefined(`Stage ${expectedStage.stageName} should exist`);
      expect(stage.stageOrder).toBe(expectedStage.stageOrder);
      expect(stage.isSystemStage).toBe(true);
    }
  });

  test('should have pipeline views created', async ({ request }) => {
    const expectedViews = [
      'vw_PipelineSummary',
      'vw_PipelineExecutionStatus',
      'vw_ProductSummary',
      'vw_SchemaAnalysisSummary'
    ];

    // Verify views exist via database metadata API
    const response = await request.get('/api/admin/database/views');
    expect(response.ok()).toBeTruthy();
    
    const views = await response.json();
    const viewNames = views.map((v: any) => v.viewName);
    
    for (const viewName of expectedViews) {
      expect(viewNames).toContain(viewName, `View ${viewName} should exist in database`);
    }
  });

  test('should have pipeline stored procedures created', async ({ request }) => {
    const expectedProcedures = [
      'sp_GetPipelineConfiguration',
      'sp_GetProductConfiguration'
    ];

    // Verify stored procedures exist
    const response = await request.get('/api/admin/database/procedures');
    expect(response.ok()).toBeTruthy();
    
    const procedures = await response.json();
    const procedureNames = procedures.map((p: any) => p.procedureName);
    
    for (const procedureName of expectedProcedures) {
      expect(procedureNames).toContain(procedureName, `Stored procedure ${procedureName} should exist`);
    }
  });

  test('should have migration history recorded', async ({ request }) => {
    // Verify that the migration was recorded
    const response = await request.get('/api/admin/database/migrations');
    expect(response.ok()).toBeTruthy();
    
    const migrations = await response.json();
    
    // Should have at least the pipeline tables migration
    const pipelineMigration = migrations.find((m: any) => m.migrationId === '001-add-pipeline-tables');
    expect(pipelineMigration).toBeDefined('Pipeline tables migration should be recorded');
    expect(pipelineMigration.description).toContain('pipeline management tables');
  });

  test('should validate foreign key constraints exist', async ({ request }) => {
    const expectedConstraints = [
      { tableName: 'ProductConnectionStrings', referencedTable: 'Products' },
      { tableName: 'ProductPipelineConfigurations', referencedTable: 'Products' },
      { tableName: 'ProductPipelineConfigurations', referencedTable: 'PipelineStages' },
      { tableName: 'PipelineStageScripts', referencedTable: 'ProductPipelineConfigurations' },
      { tableName: 'PipelineExecutions', referencedTable: 'Products' },
      { tableName: 'StageExecutions', referencedTable: 'PipelineExecutions' },
      { tableName: 'StageExecutions', referencedTable: 'PipelineStages' },
      { tableName: 'ScriptExecutionLogs', referencedTable: 'StageExecutions' },
      { tableName: 'ScriptExecutionLogs', referencedTable: 'PipelineStageScripts' }
    ];

    // Get foreign key constraints from database
    const response = await request.get('/api/admin/database/foreign-keys');
    expect(response.ok()).toBeTruthy();
    
    const constraints = await response.json();
    
    for (const expectedConstraint of expectedConstraints) {
      const constraint = constraints.find((c: any) => 
        c.tableName === expectedConstraint.tableName && 
        c.referencedTable === expectedConstraint.referencedTable
      );
      expect(constraint).toBeDefined(
        `Foreign key constraint from ${expectedConstraint.tableName} to ${expectedConstraint.referencedTable} should exist`
      );
    }
  });

  test('should validate indexes are created for performance', async ({ request }) => {
    const expectedIndexes = [
      { table: 'PipelineStages', columns: ['StageOrder'] },
      { table: 'ProductConnectionStrings', columns: ['ProductId'] },
      { table: 'ProductPipelineConfigurations', columns: ['ProductId'] },
      { table: 'PipelineStageScripts', columns: ['PipelineConfigurationId'] },
      { table: 'PipelineExecutions', columns: ['ProductId', 'Status'] },
      { table: 'StageExecutions', columns: ['PipelineExecutionId'] },
      { table: 'ScriptExecutionLogs', columns: ['StageExecutionId'] }
    ];

    // Get database indexes
    const response = await request.get('/api/admin/database/indexes');
    expect(response.ok()).toBeTruthy();
    
    const indexes = await response.json();
    
    for (const expectedIndex of expectedIndexes) {
      const indexExists = indexes.some((idx: any) => 
        idx.tableName === expectedIndex.table && 
        expectedIndex.columns.every(col => idx.columns.includes(col))
      );
      expect(indexExists).toBeTruthy(
        `Index on ${expectedIndex.table}(${expectedIndex.columns.join(', ')}) should exist`
      );
    }
  });

});