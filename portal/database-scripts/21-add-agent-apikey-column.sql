-- Add ApiKey column to ObfuscationAgents table
-- This stores the unique API key for each agent for authentication

-- Check if the column doesn't exist before adding it
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[ObfuscationAgents]') 
    AND name = 'ApiKey'
)
BEGIN
    ALTER TABLE [dbo].[ObfuscationAgents]
    ADD [ApiKey] NVARCHAR(255) NULL;
    
    PRINT 'Added ApiKey column to ObfuscationAgents table';
END
ELSE
BEGIN
    PRINT 'ApiKey column already exists in ObfuscationAgents table';
END
GO

-- Create index on ApiKey for faster authentication lookups
IF NOT EXISTS (
    SELECT * 
    FROM sys.indexes 
    WHERE object_id = OBJECT_ID(N'[dbo].[ObfuscationAgents]') 
    AND name = 'IX_ObfuscationAgents_ApiKey'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ObfuscationAgents_ApiKey]
    ON [dbo].[ObfuscationAgents]([ApiKey])
    WHERE [ApiKey] IS NOT NULL;
    
    PRINT 'Created index on ApiKey column';
END
ELSE
BEGIN
    PRINT 'Index on ApiKey column already exists';
END
GO