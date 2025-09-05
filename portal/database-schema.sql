-- =============================================
-- Database Schema for PII Obfuscation Portal
-- =============================================

-- Create the database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'obfuscate')
BEGIN
    CREATE DATABASE obfuscate;
END
GO

USE obfuscate;
GO

-- =============================================
-- Drop existing objects if they exist
-- =============================================

-- Drop triggers first
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'tr_Products_Audit')
    DROP TRIGGER tr_Products_Audit;
GO

-- Drop stored procedures
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPipelineConfiguration')
    DROP PROCEDURE sp_GetPipelineConfiguration;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetProductConfiguration')
    DROP PROCEDURE sp_GetProductConfiguration;
GO

-- Drop views
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_PipelineExecutionStatus')
    DROP VIEW vw_PipelineExecutionStatus;
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_PipelineSummary')
    DROP VIEW vw_PipelineSummary;
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_SchemaAnalysisSummary')
    DROP VIEW vw_SchemaAnalysisSummary;
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ProductSummary')
    DROP VIEW vw_ProductSummary;
GO

-- Drop tables in reverse dependency order (Pipeline and Agent tables first)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'JobExecutionLogs')
    DROP TABLE JobExecutionLogs;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ObfuscationJobs')
    DROP TABLE ObfuscationJobs;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ObfuscationAgents')
    DROP TABLE ObfuscationAgents;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ScriptExecutionLogs')
    DROP TABLE ScriptExecutionLogs;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'StageExecutions')
    DROP TABLE StageExecutions;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PipelineExecutions')
    DROP TABLE PipelineExecutions;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PipelineStageScripts')
    DROP TABLE PipelineStageScripts;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductPipelineConfigurations')
    DROP TABLE ProductPipelineConfigurations;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductConnectionStrings')
    DROP TABLE ProductConnectionStrings;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PipelineStages')
    DROP TABLE PipelineStages;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ColumnObfuscationMappings')
    DROP TABLE ColumnObfuscationMappings;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ObfuscationConfigurations')
    DROP TABLE ObfuscationConfigurations;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TableColumns')
    DROP TABLE TableColumns;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DatabaseSchemas')
    DROP TABLE DatabaseSchemas;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaAnalysisSessions')
    DROP TABLE SchemaAnalysisSessions;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLog')
    DROP TABLE AuditLog;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'UserProductPermissions')
    DROP TABLE UserProductPermissions;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles')
    DROP TABLE Roles;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
    DROP TABLE Users;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PIIDetectionRules')
    DROP TABLE PIIDetectionRules;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SupportedDataTypes')
    DROP TABLE SupportedDataTypes;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
    DROP TABLE Products;
GO

-- =============================================
-- Core Tables
-- =============================================

-- Products/Organizations
CREATE TABLE Products (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    ConnectionString NVARCHAR(MAX) NOT NULL,
    DatabaseTechnology NVARCHAR(50) NOT NULL, -- SqlServer, PostgreSQL, MySQL, etc.
    GlobalSeed NVARCHAR(255),
    BatchSize INT DEFAULT 2000,
    SqlBatchSize INT DEFAULT 500,
    ParallelThreads INT DEFAULT 8,
    MaxCacheSize INT DEFAULT 500000,
    CommandTimeoutSeconds INT DEFAULT 600,
    MappingCacheDirectory NVARCHAR(500),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(255),
    UpdatedBy NVARCHAR(255)
);
GO

-- Database Schemas (for each product)
CREATE TABLE DatabaseSchemas (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    SchemaName NVARCHAR(255) NOT NULL,
    TableName NVARCHAR(255) NOT NULL,
    FullTableName NVARCHAR(500) NOT NULL,
    PrimaryKeyColumns NVARCHAR(MAX), -- JSON array of primary key column names
    [RowCount] BIGINT DEFAULT 0,
    IsAnalyzed BIT DEFAULT 0,
    AnalyzedAt DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);
GO

-- Table Columns
CREATE TABLE TableColumns (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DatabaseSchemaId UNIQUEIDENTIFIER NOT NULL,
    ColumnName NVARCHAR(255) NOT NULL,
    SqlDataType NVARCHAR(100) NOT NULL,
    MaxLength INT,
    IsNullable BIT DEFAULT 1,
    IsPrimaryKey BIT DEFAULT 0,
    IsForeignKey BIT DEFAULT 0,
    IsIdentity BIT DEFAULT 0,
    IsComputed BIT DEFAULT 0,
    DefaultValue NVARCHAR(MAX),
    OrdinalPosition INT,
    NumericPrecision TINYINT,
    NumericScale TINYINT,
    CharacterSet NVARCHAR(100),
    Collation NVARCHAR(100),
    IsRowGuid BIT DEFAULT 0,
    IsFileStream BIT DEFAULT 0,
    IsSparse BIT DEFAULT 0,
    IsXmlDocument BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (DatabaseSchemaId) REFERENCES DatabaseSchemas(Id) ON DELETE CASCADE
);
GO

-- Obfuscation Configurations
CREATE TABLE ObfuscationConfigurations (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    ConfigurationJson NVARCHAR(MAX) NOT NULL, -- Full JSON configuration
    IsActive BIT DEFAULT 1,
    IsDefault BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(255),
    UpdatedBy NVARCHAR(255),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);
GO

-- Column Obfuscation Mappings
CREATE TABLE ColumnObfuscationMappings (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    TableColumnId UNIQUEIDENTIFIER NOT NULL,
    ObfuscationDataType NVARCHAR(100) NOT NULL, -- e.g., DriverName, ContactEmail, etc.
    IsEnabled BIT DEFAULT 1,
    PreserveLength BIT DEFAULT 1,
    ConfidenceScore DECIMAL(3,2) DEFAULT 0.0,
    DetectionReasons NVARCHAR(MAX), -- JSON array of detection reasons
    IsManuallyConfigured BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(255),
    UpdatedBy NVARCHAR(255),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    FOREIGN KEY (TableColumnId) REFERENCES TableColumns(Id) ON DELETE NO ACTION
);
GO

-- =============================================
-- User Management Tables
-- =============================================

-- Users
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(255) NOT NULL UNIQUE,
    FirstName NVARCHAR(100),
    LastName NVARCHAR(100),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- User Roles
CREATE TABLE Roles (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- User Product Permissions
CREATE TABLE UserProductPermissions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NOT NULL,
    RoleId UNIQUEIDENTIFIER NOT NULL,
    GrantedAt DATETIME2 DEFAULT GETDATE(),
    GrantedBy NVARCHAR(255),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
);
GO

-- =============================================
-- Audit and Logging Tables
-- =============================================

-- Audit Log
CREATE TABLE AuditLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER,
    ProductId UNIQUEIDENTIFIER,
    Action NVARCHAR(100) NOT NULL,
    EntityType NVARCHAR(100) NOT NULL,
    EntityId NVARCHAR(100),
    OldValues NVARCHAR(MAX),
    NewValues NVARCHAR(MAX),
    IpAddress NVARCHAR(45),
    UserAgent NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL,
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE SET NULL
);
GO

-- Schema Analysis Sessions
CREATE TABLE SchemaAnalysisSessions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    StartedAt DATETIME2 DEFAULT GETDATE(),
    CompletedAt DATETIME2,
    Status NVARCHAR(50) DEFAULT 'Running', -- Running, Completed, Failed
    ErrorMessage NVARCHAR(MAX),
    TablesAnalyzed INT DEFAULT 0,
    ColumnsAnalyzed INT DEFAULT 0,
    PIIColumnsFound INT DEFAULT 0,
    CreatedBy NVARCHAR(255),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);
GO

-- =============================================
-- Configuration and Settings Tables
-- =============================================

-- Supported Data Types
CREATE TABLE SupportedDataTypes (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- PII Detection Rules
CREATE TABLE PIIDetectionRules (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DataType NVARCHAR(100) NOT NULL,
    ObfuscationDataType NVARCHAR(100) NOT NULL,
    ColumnNamePatterns NVARCHAR(MAX), -- JSON array of patterns
    SqlDataTypes NVARCHAR(MAX), -- JSON array of SQL data types
    TableNamePatterns NVARCHAR(MAX), -- JSON array of table name patterns
    BaseConfidence DECIMAL(3,2) DEFAULT 0.5,
    PreserveLength BIT DEFAULT 1,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- =============================================
-- PIPELINE MANAGEMENT TABLES
-- =============================================

-- Pipeline Stages Definition (Master Data)
CREATE TABLE PipelineStages (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    StageName NVARCHAR(50) NOT NULL UNIQUE,
    DisplayName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    StageOrder INT NOT NULL UNIQUE,
    IsSystemStage BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- Product Connection Strings (extends existing Products table)
CREATE TABLE ProductConnectionStrings (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    ConnectionType NVARCHAR(50) NOT NULL, -- 'Source', 'Target', 'Backup'
    ConnectionString NVARCHAR(MAX) NOT NULL, -- Encrypted in production
    Description NVARCHAR(500),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(255),
    UpdatedBy NVARCHAR(255),
    CONSTRAINT FK_ProductConnectionStrings_Products FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ProductConnectionStrings_Type UNIQUE (ProductId, ConnectionType)
);
GO

-- Product Pipeline Configuration
CREATE TABLE ProductPipelineConfigurations (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    StageId UNIQUEIDENTIFIER NOT NULL,
    IsEnabled BIT NOT NULL DEFAULT 1,
    EstimatedDurationMinutes INT,
    MaxRetryCount INT DEFAULT 3,
    ContinueOnFailure BIT DEFAULT 0,
    StageConfiguration NVARCHAR(MAX), -- JSON configuration for the stage
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(255),
    UpdatedBy NVARCHAR(255),
    CONSTRAINT FK_ProductPipelineConfigurations_Products FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ProductPipelineConfigurations_PipelineStages FOREIGN KEY (StageId) REFERENCES PipelineStages(Id),
    CONSTRAINT UQ_ProductPipelineConfigurations_Stage UNIQUE (ProductId, StageId)
);
GO

-- SQL Scripts for Pipeline Stages
CREATE TABLE PipelineStageScripts (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PipelineConfigurationId UNIQUEIDENTIFIER NOT NULL,
    ScriptName NVARCHAR(200) NOT NULL,
    ScriptContent NVARCHAR(MAX) NOT NULL,
    ScriptType NVARCHAR(50) DEFAULT 'SQL', -- SQL, PowerShell, etc.
    ExecutionOrder INT NOT NULL,
    IsEnabled BIT NOT NULL DEFAULT 1,
    Parameters NVARCHAR(MAX), -- JSON parameters
    ValidationQuery NVARCHAR(MAX), -- Query to validate script success
    TimeoutSeconds INT DEFAULT 600,
    CreatedBy NVARCHAR(255),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CONSTRAINT FK_PipelineStageScripts_ProductPipelineConfigurations FOREIGN KEY (PipelineConfigurationId) REFERENCES ProductPipelineConfigurations(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_PipelineStageScripts_Order UNIQUE (PipelineConfigurationId, ExecutionOrder)
);
GO

-- Pipeline Executions (Updated with Phase 2.4 columns)
CREATE TABLE PipelineExecutions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    ExecutionName NVARCHAR(200),
    Status NVARCHAR(50) NOT NULL DEFAULT 'Queued', -- Queued, Running, Completed, Failed, Cancelled, Paused
    StartedAt DATETIME2,
    CompletedAt DATETIME2,
    PausedAt DATETIME2,
    InitiatedBy NVARCHAR(255),
    ConfigurationSnapshot NVARCHAR(MAX), -- JSON snapshot of configuration at execution time
    TotalStages INT DEFAULT 0,
    CompletedStages INT DEFAULT 0,
    FailedStages INT DEFAULT 0,
    CurrentStageId UNIQUEIDENTIFIER,
    ProgressPercentage DECIMAL(5,2) DEFAULT 0.00,
    EstimatedCompletionAt DATETIME2,
    ResultSummary NVARCHAR(MAX), -- JSON result summary
    ErrorMessage NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    -- Phase 2.4 Added Columns
    QueuedAt DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    IsDryRun BIT NOT NULL DEFAULT 0,
    DurationMs BIGINT NULL,
    Priority INT NOT NULL DEFAULT 5,
    CurrentStage INT NULL,
    StagesCompleted INT NOT NULL DEFAULT 0,
    StagesFailed INT NOT NULL DEFAULT 0,
    StagesSkipped INT NOT NULL DEFAULT 0,
    TotalRowsProcessed BIGINT NULL DEFAULT 0,
    TotalRowsAffected BIGINT NULL DEFAULT 0,
    ConnectionType NVARCHAR(50) NULL,
    Tags NVARCHAR(500) NULL,
    HangfireJobId NVARCHAR(255) NULL,
    CancellationReason NVARCHAR(MAX) NULL,
    ErrorStackTrace NVARCHAR(MAX) NULL,
    ExecutionParameters NVARCHAR(MAX) NULL,
    CONSTRAINT FK_PipelineExecutions_Products FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    CONSTRAINT CHK_PipelineExecutions_Status CHECK (Status IN ('Queued', 'Running', 'Completed', 'Failed', 'Cancelled', 'Paused')),
    CONSTRAINT CHK_PipelineExecutions_Progress CHECK (ProgressPercentage >= 0.00 AND ProgressPercentage <= 100.00),
    CONSTRAINT CHK_PipelineExecutions_Priority CHECK (Priority >= 1 AND Priority <= 10)
);
GO

-- Stage Executions within Pipeline (Updated with Phase 2.4 columns)
CREATE TABLE StageExecutions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PipelineExecutionId UNIQUEIDENTIFIER NOT NULL,
    PipelineStageId UNIQUEIDENTIFIER NOT NULL, -- Renamed from StageId
    Status NVARCHAR(50) NOT NULL DEFAULT 'Queued', -- Queued, Running, Completed, Failed, Skipped
    StartedAt DATETIME2,
    CompletedAt DATETIME2,
    DurationSeconds INT,
    RowsProcessed BIGINT DEFAULT 0,
    ScriptsExecuted INT DEFAULT 0,
    ScriptsFailed INT DEFAULT 0,
    StageOutput NVARCHAR(MAX), -- JSON output data
    ErrorMessage NVARCHAR(MAX),
    RetryCount INT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    -- Phase 2.4 Added Columns
    QueuedAt DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    ExecutionOrder INT NOT NULL DEFAULT 1,
    StageName NVARCHAR(100) NOT NULL DEFAULT 'Unknown',
    DurationMs BIGINT NULL,
    TotalScripts INT NOT NULL DEFAULT 0,
    ScriptsCompleted INT NOT NULL DEFAULT 0,
    ScriptsSkipped INT NOT NULL DEFAULT 0,
    ProgressPercentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    RowsAffected BIGINT NULL,
    OnFailureAction NVARCHAR(20) NULL DEFAULT 'Continue',
    RetryAttempts INT NOT NULL DEFAULT 0,
    MaxRetries INT NOT NULL DEFAULT 3,
    TimeoutMinutes INT NOT NULL DEFAULT 60,
    SupportsParallelExecution BIT NOT NULL DEFAULT 0,
    ExecutedInParallel BIT NOT NULL DEFAULT 0,
    MemoryUsageMB DECIMAL(10,2) NULL,
    CpuUsagePercentage DECIMAL(5,2) NULL,
    ConfigurationSnapshot NVARCHAR(MAX) NULL,
    ExecutionMetadata NVARCHAR(MAX) NULL,
    OutputSummary NVARCHAR(MAX) NULL,
    Warnings NVARCHAR(MAX) NULL,
    ErrorStackTrace NVARCHAR(MAX) NULL,
    CONSTRAINT FK_StageExecutions_PipelineExecutions FOREIGN KEY (PipelineExecutionId) REFERENCES PipelineExecutions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_StageExecutions_PipelineStages FOREIGN KEY (PipelineStageId) REFERENCES PipelineStages(Id) ON DELETE NO ACTION,
    CONSTRAINT CHK_StageExecutions_Status CHECK (Status IN ('Queued', 'Running', 'Completed', 'Failed', 'Cancelled', 'Skipped')),
    CONSTRAINT CHK_StageExecutions_ProgressPercentage CHECK (ProgressPercentage >= 0 AND ProgressPercentage <= 100),
    CONSTRAINT CHK_StageExecutions_ExecutionOrder CHECK (ExecutionOrder >= 1 AND ExecutionOrder <= 100)
);
GO

-- Script Execution Logs
CREATE TABLE ScriptExecutionLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    StageExecutionId UNIQUEIDENTIFIER NOT NULL,
    ScriptId UNIQUEIDENTIFIER NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Queued', -- Queued, Running, Completed, Failed
    StartedAt DATETIME2,
    CompletedAt DATETIME2,
    DurationSeconds INT,
    RowsAffected BIGINT DEFAULT 0,
    ExecutionOutput NVARCHAR(MAX),
    ErrorMessage NVARCHAR(MAX),
    RetryCount INT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CONSTRAINT FK_ScriptExecutionLogs_StageExecutions FOREIGN KEY (StageExecutionId) REFERENCES StageExecutions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScriptExecutionLogs_PipelineStageScripts FOREIGN KEY (ScriptId) REFERENCES PipelineStageScripts(Id),
    CONSTRAINT CHK_ScriptExecutionLogs_Status CHECK (Status IN ('Queued', 'Running', 'Completed', 'Failed'))
);
GO

-- =============================================
-- Distributed Agent Tables
-- =============================================

-- ObfuscationAgents table
CREATE TABLE ObfuscationAgents (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    HostName NVARCHAR(255) NOT NULL,
    IpAddress NVARCHAR(45) NOT NULL,
    Port INT NOT NULL DEFAULT 8080,
    Version NVARCHAR(20) NOT NULL,
    Environment NVARCHAR(50) DEFAULT 'Production',
    Location NVARCHAR(50) NULL,
    Status INT NOT NULL DEFAULT 0, -- 0=Offline, 1=Online, 2=Busy, 3=Error, 4=Maintenance
    Capabilities NVARCHAR(MAX) NULL, -- JSON
    LastHeartbeat DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    RegisteredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastJobStarted DATETIME2 NULL,
    LastJobCompleted DATETIME2 NULL,
    
    -- Performance metrics
    CpuCores INT NOT NULL DEFAULT 1,
    MemoryMB BIGINT NOT NULL DEFAULT 1024,
    CurrentCpuUsage INT NOT NULL DEFAULT 0,
    CurrentMemoryUsage BIGINT NOT NULL DEFAULT 0,
    ActiveJobs INT NOT NULL DEFAULT 0,
    CompletedJobs INT NOT NULL DEFAULT 0,
    FailedJobs INT NOT NULL DEFAULT 0,
    
    -- Configuration
    MaxConcurrentJobs INT NOT NULL DEFAULT 1,
    IsEnabled BIT NOT NULL DEFAULT 1,
    Priority INT NOT NULL DEFAULT 1,
    Notes NVARCHAR(500) NULL,
    
    CONSTRAINT UQ_ObfuscationAgents_HostPort UNIQUE (HostName, Port)
);
GO

-- ObfuscationJobs table
CREATE TABLE ObfuscationJobs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    AgentId UNIQUEIDENTIFIER NOT NULL,
    JobName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    Status INT NOT NULL DEFAULT 0, -- 0=Pending, 1=Assigned, 2=Running, 3=Completed, 4=Failed, 5=Cancelled, 6=Retrying
    Priority INT NOT NULL DEFAULT 1, -- 0=Low, 1=Normal, 2=High, 3=Critical
    
    MappingVersion NVARCHAR(50) NOT NULL,
    MappingSasUrl NVARCHAR(MAX) NOT NULL,
    Configuration NVARCHAR(MAX) NULL, -- JSON
    
    -- Timing
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    LastUpdateAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    -- Progress tracking
    ProgressPercentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    TotalRows BIGINT NOT NULL DEFAULT 0,
    ProcessedRows BIGINT NOT NULL DEFAULT 0,
    TablesCompleted INT NOT NULL DEFAULT 0,
    TotalTables INT NOT NULL DEFAULT 0,
    
    -- Results and errors
    ErrorMessage NVARCHAR(1000) NULL,
    ResultSummary NVARCHAR(4000) NULL,
    ResultsBlobUrl NVARCHAR(MAX) NULL,
    
    -- Performance metrics
    Duration TIME NULL,
    RowsPerSecond DECIMAL(18,2) NULL,
    PeakMemoryUsageMB BIGINT NULL,
    
    -- Retry logic
    RetryCount INT NOT NULL DEFAULT 0,
    MaxRetries INT NOT NULL DEFAULT 3,
    
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'System',
    
    CONSTRAINT FK_ObfuscationJobs_Products FOREIGN KEY (ProductId) 
        REFERENCES Products(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ObfuscationJobs_ObfuscationAgents FOREIGN KEY (AgentId) 
        REFERENCES ObfuscationAgents(Id)
);
GO

-- JobExecutionLogs table
CREATE TABLE JobExecutionLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    JobId UNIQUEIDENTIFIER NOT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LogLevel INT NOT NULL, -- 0=Trace, 1=Debug, 2=Information, 3=Warning, 4=Error, 5=Critical
    Component NVARCHAR(100) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    Details NVARCHAR(MAX) NULL,
    Exception NVARCHAR(MAX) NULL,
    
    TableName NVARCHAR(100) NULL,
    ColumnName NVARCHAR(100) NULL,
    RowsProcessed BIGINT NULL,
    ProgressPercentage DECIMAL(5,2) NULL,
    ElapsedTime TIME NULL,
    
    -- Performance metrics
    CpuUsagePercentage DECIMAL(5,2) NULL,
    MemoryUsageMB BIGINT NULL,
    RowsPerSecond DECIMAL(18,2) NULL,
    
    CONSTRAINT FK_JobExecutionLogs_ObfuscationJobs FOREIGN KEY (JobId) 
        REFERENCES ObfuscationJobs(Id) ON DELETE CASCADE
);
GO

-- =============================================
-- Indexes for Performance
-- =============================================

-- Products
CREATE INDEX IX_Products_Name ON Products(Name);
CREATE INDEX IX_Products_IsActive ON Products(IsActive);
GO

-- Database Schemas
CREATE INDEX IX_DatabaseSchemas_ProductId ON DatabaseSchemas(ProductId);
CREATE INDEX IX_DatabaseSchemas_FullTableName ON DatabaseSchemas(FullTableName);
CREATE INDEX IX_DatabaseSchemas_IsAnalyzed ON DatabaseSchemas(IsAnalyzed);
GO

-- Table Columns
CREATE INDEX IX_TableColumns_DatabaseSchemaId ON TableColumns(DatabaseSchemaId);
CREATE INDEX IX_TableColumns_ColumnName ON TableColumns(ColumnName);
CREATE INDEX IX_TableColumns_IsPrimaryKey ON TableColumns(IsPrimaryKey);
GO

-- Obfuscation Configurations
CREATE INDEX IX_ObfuscationConfigurations_ProductId ON ObfuscationConfigurations(ProductId);
CREATE INDEX IX_ObfuscationConfigurations_IsActive ON ObfuscationConfigurations(IsActive);
CREATE INDEX IX_ObfuscationConfigurations_IsDefault ON ObfuscationConfigurations(IsDefault);
GO

-- Column Obfuscation Mappings
CREATE INDEX IX_ColumnObfuscationMappings_ProductId ON ColumnObfuscationMappings(ProductId);
CREATE INDEX IX_ColumnObfuscationMappings_TableColumnId ON ColumnObfuscationMappings(TableColumnId);
CREATE INDEX IX_ColumnObfuscationMappings_IsEnabled ON ColumnObfuscationMappings(IsEnabled);
GO

-- User Product Permissions
CREATE INDEX IX_UserProductPermissions_UserId ON UserProductPermissions(UserId);
CREATE INDEX IX_UserProductPermissions_ProductId ON UserProductPermissions(ProductId);
GO

-- Audit Log
CREATE INDEX IX_AuditLog_UserId ON AuditLog(UserId);
CREATE INDEX IX_AuditLog_ProductId ON AuditLog(ProductId);
CREATE INDEX IX_AuditLog_CreatedAt ON AuditLog(CreatedAt);
GO

-- Pipeline Tables Indexes
CREATE INDEX IX_PipelineStages_StageOrder ON PipelineStages(StageOrder);
CREATE INDEX IX_PipelineStages_IsSystemStage ON PipelineStages(IsSystemStage);
GO

-- ProductConnectionStrings indexes
CREATE INDEX IX_ProductConnectionStrings_ProductId ON ProductConnectionStrings(ProductId);
CREATE INDEX IX_ProductConnectionStrings_ConnectionType ON ProductConnectionStrings(ConnectionType);
CREATE INDEX IX_ProductConnectionStrings_IsActive ON ProductConnectionStrings(IsActive);
GO

-- ProductPipelineConfigurations indexes
CREATE INDEX IX_ProductPipelineConfigurations_ProductId ON ProductPipelineConfigurations(ProductId);
CREATE INDEX IX_ProductPipelineConfigurations_StageId ON ProductPipelineConfigurations(StageId);
CREATE INDEX IX_ProductPipelineConfigurations_IsEnabled ON ProductPipelineConfigurations(IsEnabled);
GO

-- PipelineStageScripts indexes
CREATE INDEX IX_PipelineStageScripts_PipelineConfigurationId ON PipelineStageScripts(PipelineConfigurationId);
CREATE INDEX IX_PipelineStageScripts_ExecutionOrder ON PipelineStageScripts(ExecutionOrder);
CREATE INDEX IX_PipelineStageScripts_IsEnabled ON PipelineStageScripts(IsEnabled);
GO

-- PipelineExecutions indexes (Updated with Phase 2.4 indexes)
CREATE INDEX IX_PipelineExecutions_ProductId ON PipelineExecutions(ProductId);
CREATE INDEX IX_PipelineExecutions_Status ON PipelineExecutions(Status);
CREATE INDEX IX_PipelineExecutions_StartedAt ON PipelineExecutions(StartedAt);
CREATE INDEX IX_PipelineExecutions_CreatedAt ON PipelineExecutions(CreatedAt);
-- Phase 2.4 Additional Indexes
CREATE INDEX IX_PipelineExecutions_QueuedAt ON PipelineExecutions(QueuedAt);
CREATE INDEX IX_PipelineExecutions_Priority ON PipelineExecutions(Priority);
CREATE INDEX IX_PipelineExecutions_HangfireJobId ON PipelineExecutions(HangfireJobId);
CREATE INDEX IX_PipelineExecutions_IsDryRun ON PipelineExecutions(IsDryRun);
-- Composite Indexes for Query Optimization
CREATE INDEX IX_PipelineExecutions_ProductId_Status_QueuedAt ON PipelineExecutions(ProductId, Status, QueuedAt DESC);
CREATE INDEX IX_PipelineExecutions_ProductId_QueuedAt_Desc ON PipelineExecutions(ProductId, QueuedAt DESC) INCLUDE (Status, StartedAt, CompletedAt, ProgressPercentage);
GO

-- StageExecutions indexes (Updated with Phase 2.4 indexes)
CREATE INDEX IX_StageExecutions_PipelineExecutionId ON StageExecutions(PipelineExecutionId);
CREATE INDEX IX_StageExecutions_PipelineStageId ON StageExecutions(PipelineStageId);
CREATE INDEX IX_StageExecutions_Status ON StageExecutions(Status);
CREATE INDEX IX_StageExecutions_StartedAt ON StageExecutions(StartedAt);
-- Phase 2.4 Additional Indexes
CREATE INDEX IX_StageExecutions_ExecutionOrder ON StageExecutions(ExecutionOrder);
CREATE INDEX IX_StageExecutions_QueuedAt ON StageExecutions(QueuedAt);
-- Composite Indexes for Query Optimization
CREATE INDEX IX_StageExecutions_PipelineExec_Order_Status ON StageExecutions(PipelineExecutionId, ExecutionOrder, Status);
GO

-- ScriptExecutionLogs indexes
CREATE INDEX IX_ScriptExecutionLogs_StageExecutionId ON ScriptExecutionLogs(StageExecutionId);
CREATE INDEX IX_ScriptExecutionLogs_ScriptId ON ScriptExecutionLogs(ScriptId);
CREATE INDEX IX_ScriptExecutionLogs_Status ON ScriptExecutionLogs(Status);
CREATE INDEX IX_ScriptExecutionLogs_CreatedAt ON ScriptExecutionLogs(CreatedAt);
GO

-- ObfuscationAgents indexes
CREATE INDEX IX_ObfuscationAgents_Status ON ObfuscationAgents(Status);
CREATE INDEX IX_ObfuscationAgents_IsEnabled ON ObfuscationAgents(IsEnabled);
CREATE INDEX IX_ObfuscationAgents_LastHeartbeat ON ObfuscationAgents(LastHeartbeat);
CREATE INDEX IX_ObfuscationAgents_Location ON ObfuscationAgents(Location);
CREATE INDEX IX_ObfuscationAgents_Priority_ActiveJobs ON ObfuscationAgents(Priority DESC, ActiveJobs);
GO

-- ObfuscationJobs indexes
CREATE INDEX IX_ObfuscationJobs_ProductId ON ObfuscationJobs(ProductId);
CREATE INDEX IX_ObfuscationJobs_AgentId ON ObfuscationJobs(AgentId);
CREATE INDEX IX_ObfuscationJobs_Status ON ObfuscationJobs(Status);
CREATE INDEX IX_ObfuscationJobs_Priority ON ObfuscationJobs(Priority);
CREATE INDEX IX_ObfuscationJobs_CreatedAt ON ObfuscationJobs(CreatedAt);
CREATE INDEX IX_ObfuscationJobs_StartedAt ON ObfuscationJobs(StartedAt);
CREATE INDEX IX_ObfuscationJobs_Status_Priority_CreatedAt ON ObfuscationJobs(Status, Priority DESC, CreatedAt);
GO

-- JobExecutionLogs indexes
CREATE INDEX IX_JobExecutionLogs_JobId ON JobExecutionLogs(JobId);
CREATE INDEX IX_JobExecutionLogs_Timestamp ON JobExecutionLogs(Timestamp);
CREATE INDEX IX_JobExecutionLogs_LogLevel ON JobExecutionLogs(LogLevel);
CREATE INDEX IX_JobExecutionLogs_JobId_Timestamp ON JobExecutionLogs(JobId, Timestamp DESC);
GO

-- =============================================
-- Initial Data
-- =============================================

-- Insert default roles
INSERT INTO Roles (Name, Description) VALUES
('Admin', 'Full access to all products and configurations'),
('ProductAdmin', 'Admin access to specific products'),
('Analyst', 'Can view and analyze schemas'),
('Viewer', 'Read-only access to assigned products');
GO

-- Insert default pipeline stages (Updated with Phase 2.4 stages)
INSERT INTO PipelineStages (StageName, DisplayName, Description, StageOrder, IsSystemStage) VALUES
('Schema Analysis', 'Database Schema Analysis', 'Analyzes database schema and identifies PII columns', 1, 1),
('Mapping Generation', 'PII Mapping Generation', 'Generates deterministic obfuscation mappings', 2, 1),
('Data Validation', 'Pre-Obfuscation Validation', 'Validates data integrity before obfuscation', 3, 1),
('Data Obfuscation', 'PII Data Obfuscation', 'Performs actual data obfuscation', 4, 1),
('Verification & Reporting', 'Post-Obfuscation Verification', 'Verifies results and generates reports', 5, 1);
GO

-- Insert default supported data types (aligned with Common/DataTypes/SupportedDataTypes.cs)
DECLARE @ExpectedSupportedTypes TABLE (Name NVARCHAR(100) PRIMARY KEY, Description NVARCHAR(MAX));
INSERT INTO @ExpectedSupportedTypes (Name, Description) VALUES
('FirstName', 'Given names only (NOT full names)'),
('LastName', 'Surnames only (NOT full names)'),
('FullName', 'Full personal name/display name'),
('Email', 'Email addresses'),
('Phone', 'Phone numbers'),
('FullAddress', 'Full address (all components combined)'),
('AddressLine1', 'Primary address line (street number + name)'),
('AddressLine2', 'Secondary address line (apartment/unit/suite)'),
('City', 'City/Suburb/Town'),
('Suburb', 'Alternative name for City (AU)'),
('State', 'State/Province/County'),
('StateAbbr', 'State abbreviation'),
('PostCode', 'Postal/ZIP code'),
('ZipCode', 'Alternative name for PostCode'),
('Country', 'Country name'),
('Address', 'Legacy address type - use specific components instead'),
('Date', 'General date value'),
('DateOfBirth', 'Date of Birth specifically'),
('CreditCard', 'Credit card numbers'),
('NINO', 'UK National Insurance Number'),
('NationalInsuranceNumber', 'UK National Insurance Number (alias)'),
('SortCode', 'UK bank sort code'),
('BankSortCode', 'UK bank sort code (alias)'),
('LicenseNumber', 'License numbers, permit IDs'),
('CompanyName', 'Company/operator names'),
('BusinessABN', 'Australian Business Number'),
('BusinessACN', 'Australian Company Number'),
('VehicleRegistration', 'Vehicle registration plates'),
('VINNumber', 'Vehicle identification numbers'),
('VehicleMakeModel', 'Vehicle make and model information'),
('EngineNumber', 'Engine identification numbers'),
('GPSCoordinate', 'GPS coordinates, location data'),
('RouteCode', 'Route identifiers'),
('DepotLocation', 'Depot/facility locations'),
('UKPostcode', 'UK postal codes (e.g., SW1A 1AA)');

MERGE SupportedDataTypes AS t
USING @ExpectedSupportedTypes AS s
    ON t.Name = s.Name
WHEN MATCHED THEN
    UPDATE SET t.Description = s.Description, t.IsActive = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, Description, IsActive) VALUES (s.Name, s.Description, 1);
GO

-- Insert default PII detection rules
INSERT INTO PIIDetectionRules (DataType, ObfuscationDataType, ColumnNamePatterns, SqlDataTypes, TableNamePatterns, BaseConfidence, PreserveLength) VALUES
('PersonName', 'DriverName', '["*name*", "*first*", "*last*", "*middle*", "*full*", "*display*"]', '["varchar", "nvarchar", "char", "nchar"]', '["*person*", "*employee*", "*customer*", "*contact*"]', 0.7, 1),
('EmailAddress', 'ContactEmail', '["*email*", "*mail*", "*e_mail*", "*emailaddress*"]', '["varchar", "nvarchar", "text"]', '["*person*", "*contact*", "*user*"]', 0.8, 1),
('PhoneNumber', 'DriverPhone', '["*phone*", "*mobile*", "*cell*", "*tel*", "*telephone*"]', '["varchar", "nvarchar", "char"]', '["*person*", "*contact*", "*phone*"]', 0.8, 1),
('Address', 'AddressLine1', '["*address*", "*street*", "*line1*"]', '["varchar", "nvarchar", "text"]', '["*address*", "*contact*", "*location*"]', 0.7, 0),
('City', 'City', '["*city*", "*town*", "*municipality*"]', '["varchar", "nvarchar", "char"]', '["*address*", "*location*", "*city*"]', 0.6, 0),
('PostalCode', 'PostCode', '["*postal*", "*zip*", "*postcode*", "*zipcode*"]', '["varchar", "nvarchar", "char"]', '["*address*", "*location*"]', 0.7, 0),
('CreditCard', 'CreditCard', '["*card*", "*credit*", "*cc*", "*cardnumber*"]', '["varchar", "nvarchar", "char"]', '["*payment*", "*credit*", "*card*"]', 0.9, 1),
('DateOfBirth', 'DateOfBirth', '["*birth*", "*dob*", "*born*", "*birthdate*"]', '["date", "datetime", "datetime2"]', '["*person*", "*employee*", "*customer*"]', 0.8, 0);
GO

-- =============================================
-- Views for Common Queries
-- =============================================

-- Product Summary View
CREATE VIEW vw_ProductSummary AS
SELECT 
    p.Id,
    p.Name,
    p.Description,
    p.DatabaseTechnology,
    p.IsActive,
    COUNT(DISTINCT ds.Id) as TableCount,
    COUNT(DISTINCT tc.Id) as ColumnCount,
    COUNT(DISTINCT CASE WHEN com.IsEnabled = 1 THEN com.Id END) as ObfuscatedColumnsCount,
    p.CreatedAt,
    p.UpdatedAt
FROM Products p
LEFT JOIN DatabaseSchemas ds ON p.Id = ds.ProductId
LEFT JOIN TableColumns tc ON ds.Id = tc.DatabaseSchemaId
LEFT JOIN ColumnObfuscationMappings com ON tc.Id = com.TableColumnId
GROUP BY p.Id, p.Name, p.Description, p.DatabaseTechnology, p.IsActive, p.CreatedAt, p.UpdatedAt;
GO

-- Schema Analysis Summary View
CREATE VIEW vw_SchemaAnalysisSummary AS
SELECT 
    p.Name as ProductName,
    p.Id as ProductId,
    COUNT(DISTINCT ds.Id) as TotalTables,
    COUNT(DISTINCT tc.Id) as TotalColumns,
    COUNT(DISTINCT CASE WHEN ds.IsAnalyzed = 1 THEN ds.Id END) as AnalyzedTables,
    COUNT(DISTINCT CASE WHEN com.IsEnabled = 1 THEN com.Id END) as ObfuscatedColumns,
    MAX(ds.AnalyzedAt) as LastAnalyzedAt
FROM Products p
LEFT JOIN DatabaseSchemas ds ON p.Id = ds.ProductId
LEFT JOIN TableColumns tc ON ds.Id = tc.DatabaseSchemaId
LEFT JOIN ColumnObfuscationMappings com ON tc.Id = com.TableColumnId
GROUP BY p.Name, p.Id;
GO

-- Pipeline Configuration Summary View
CREATE VIEW vw_PipelineSummary AS
SELECT 
    p.Id as ProductId,
    p.Name as ProductName,
    COUNT(DISTINCT ppc.Id) as ConfiguredStages,
    COUNT(DISTINCT pss.Id) as TotalScripts,
    COUNT(DISTINCT CASE WHEN ppc.IsEnabled = 1 THEN ppc.Id END) as EnabledStages,
    COUNT(DISTINCT CASE WHEN pss.IsEnabled = 1 THEN pss.Id END) as EnabledScripts,
    MAX(ppc.UpdatedAt) as LastConfigurationUpdate
FROM Products p
LEFT JOIN ProductPipelineConfigurations ppc ON p.Id = ppc.ProductId
LEFT JOIN PipelineStageScripts pss ON ppc.Id = pss.PipelineConfigurationId
GROUP BY p.Id, p.Name;
GO

-- Pipeline Execution Status View
CREATE VIEW vw_PipelineExecutionStatus AS
SELECT 
    pe.Id as ExecutionId,
    pe.ProductId,
    p.Name as ProductName,
    pe.ExecutionName,
    pe.Status as ExecutionStatus,
    pe.StartedAt,
    pe.CompletedAt,
    pe.ProgressPercentage,
    pe.TotalStages,
    pe.CompletedStages,
    pe.FailedStages,
    CASE 
        WHEN pe.Status = 'Running' THEN DATEDIFF(MINUTE, pe.StartedAt, GETDATE())
        WHEN pe.CompletedAt IS NOT NULL THEN DATEDIFF(MINUTE, pe.StartedAt, pe.CompletedAt)
        ELSE NULL 
    END as DurationMinutes,
    COUNT(DISTINCT se.Id) as TotalStageExecutions,
    COUNT(DISTINCT CASE WHEN se.Status = 'Completed' THEN se.Id END) as CompletedStageExecutions
FROM PipelineExecutions pe
INNER JOIN Products p ON pe.ProductId = p.Id
LEFT JOIN StageExecutions se ON pe.Id = se.PipelineExecutionId
GROUP BY pe.Id, pe.ProductId, p.Name, pe.ExecutionName, pe.Status, 
         pe.StartedAt, pe.CompletedAt, pe.ProgressPercentage, 
         pe.TotalStages, pe.CompletedStages, pe.FailedStages;
GO

-- =============================================
-- Stored Procedures
-- =============================================

-- Get Product Configuration as JSON
CREATE PROCEDURE sp_GetProductConfiguration
    @ProductId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        p.Id as ProductId,
        p.Name as ProductName,
        p.ConnectionString,
        p.DatabaseTechnology,
        p.GlobalSeed,
        p.BatchSize,
        p.SqlBatchSize,
        p.ParallelThreads,
        p.MaxCacheSize,
        p.CommandTimeoutSeconds,
        p.MappingCacheDirectory,
        (
            SELECT 
                ds.SchemaName,
                ds.TableName,
                ds.FullTableName,
                ds.PrimaryKeyColumns,
                ds.[RowCount],
                (
                    SELECT 
                        tc.ColumnName,
                        tc.SqlDataType,
                        tc.MaxLength,
                        tc.IsNullable,
                        tc.IsPrimaryKey,
                        com.ObfuscationDataType,
                        com.IsEnabled,
                        com.PreserveLength,
                        com.ConfidenceScore
                    FROM TableColumns tc
                    LEFT JOIN ColumnObfuscationMappings com ON tc.Id = com.TableColumnId
                    WHERE tc.DatabaseSchemaId = ds.Id
                    FOR JSON PATH
                ) as Columns
            FROM DatabaseSchemas ds
            WHERE ds.ProductId = p.Id
            FOR JSON PATH
        ) as Tables
    FROM Products p
    WHERE p.Id = @ProductId
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
END;
GO

-- Get Pipeline Configuration as JSON
CREATE PROCEDURE sp_GetPipelineConfiguration
    @ProductId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        p.Id as ProductId,
        p.Name as ProductName,
        (
            SELECT 
                ps.Id as StageId,
                ps.StageName,
                ps.DisplayName,
                ps.Description,
                ps.StageOrder,
                ppc.IsEnabled,
                ppc.EstimatedDurationMinutes,
                ppc.MaxRetryCount,
                ppc.ContinueOnFailure,
                ppc.StageConfiguration,
                (
                    SELECT 
                        pss.Id as ScriptId,
                        pss.ScriptName,
                        pss.ScriptContent,
                        pss.ScriptType,
                        pss.ExecutionOrder,
                        pss.IsEnabled as ScriptEnabled,
                        pss.Parameters,
                        pss.ValidationQuery,
                        pss.TimeoutSeconds
                    FROM PipelineStageScripts pss
                    WHERE pss.PipelineConfigurationId = ppc.Id
                    ORDER BY pss.ExecutionOrder
                    FOR JSON PATH
                ) as Scripts
            FROM PipelineStages ps
            LEFT JOIN ProductPipelineConfigurations ppc ON ps.Id = ppc.StageId AND ppc.ProductId = p.Id
            ORDER BY ps.StageOrder
            FOR JSON PATH
        ) as Stages
    FROM Products p
    WHERE p.Id = @ProductId
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
END;
GO

-- =============================================
-- Triggers for Audit Logging
-- =============================================

-- Audit trigger for Products table
CREATE TRIGGER tr_Products_Audit
ON Products
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Action NVARCHAR(10);
    DECLARE @UserId NVARCHAR(255) = SYSTEM_USER;
    
    IF EXISTS(SELECT * FROM INSERTED) AND EXISTS(SELECT * FROM DELETED)
        SET @Action = 'UPDATE';
    ELSE IF EXISTS(SELECT * FROM INSERTED)
        SET @Action = 'INSERT';
    ELSE
        SET @Action = 'DELETE';
    
    INSERT INTO AuditLog (Action, EntityType, EntityId, OldValues, NewValues)
    SELECT 
        @Action,
        'Product',
        ISNULL(i.Id, d.Id),
        CASE WHEN @Action IN ('UPDATE', 'DELETE') THEN (SELECT * FROM DELETED FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) ELSE NULL END,
        CASE WHEN @Action IN ('INSERT', 'UPDATE') THEN (SELECT * FROM INSERTED FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) ELSE NULL END
    FROM INSERTED i
    FULL OUTER JOIN DELETED d ON i.Id = d.Id;
END;
GO
