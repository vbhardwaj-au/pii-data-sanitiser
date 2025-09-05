SET QUOTED_IDENTIFIER ON;
GO

-- Delete all existing agent records
DELETE FROM ObfuscationAgents;

SELECT @@ROWCOUNT as 'Deleted Agents';
GO