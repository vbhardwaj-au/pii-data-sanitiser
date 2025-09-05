-- Clean up all agent records from the database
-- WARNING: This will delete all agents and related data

BEGIN TRANSACTION;

-- First, clear any jobs assigned to agents
UPDATE ObfuscationJobs 
SET AgentId = NULL 
WHERE AgentId IS NOT NULL;

-- Delete all job execution logs
DELETE FROM JobExecutionLogs;

-- Delete all agents
DELETE FROM ObfuscationAgents;

-- Verify cleanup
SELECT 'Agents remaining:' as Status, COUNT(*) as Count FROM ObfuscationAgents
UNION ALL
SELECT 'Jobs with agents:' as Status, COUNT(*) as Count FROM ObfuscationJobs WHERE AgentId IS NOT NULL
UNION ALL
SELECT 'Job logs remaining:' as Status, COUNT(*) as Count FROM JobExecutionLogs;

-- Commit the transaction
COMMIT;

-- Verify final state
SELECT 'Final agent count:' as Status, COUNT(*) as Count FROM ObfuscationAgents;