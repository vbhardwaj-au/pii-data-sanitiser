-- Create index on ApiKey column with proper SET options
SET QUOTED_IDENTIFIER ON;
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