using Microsoft.EntityFrameworkCore;
using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository implementation for managing script execution logs
    /// </summary>
    public class ScriptExecutionLogRepository : IScriptExecutionLogRepository
    {
        private readonly PortalDbContext _context;

        public ScriptExecutionLogRepository(PortalDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets execution logs for a specific script
        /// </summary>
        public async Task<IEnumerable<ScriptExecutionLog>> GetByScriptIdAsync(Guid scriptId, int limit = 50)
        {
            return await _context.ScriptExecutionLogs
                .Include(log => log.Script)
                .Where(log => log.ScriptId == scriptId)
                .OrderByDescending(log => log.StartedAt)
                .Take(limit)
                .ToListAsync();
        }

        /// <summary>
        /// Gets execution logs for a pipeline execution
        /// </summary>
        public async Task<IEnumerable<ScriptExecutionLog>> GetByPipelineExecutionIdAsync(Guid pipelineExecutionId)
        {
            return await _context.ScriptExecutionLogs
                .Include(log => log.Script)
                .ThenInclude(s => s!.PipelineStage)
                .Where(log => log.PipelineExecutionId == pipelineExecutionId)
                .OrderBy(log => log.Script!.PipelineStage!.ExecutionOrder)
                .ThenBy(log => log.Script!.ExecutionOrder)
                .ThenBy(log => log.StartedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a specific execution log by ID
        /// </summary>
        public async Task<ScriptExecutionLog?> GetByIdAsync(Guid id)
        {
            return await _context.ScriptExecutionLogs
                .Include(log => log.Script)
                .ThenInclude(s => s!.Product)
                .Include(log => log.Script)
                .ThenInclude(s => s!.PipelineStage)
                .FirstOrDefaultAsync(log => log.Id == id);
        }

        /// <summary>
        /// Creates a new execution log
        /// </summary>
        public async Task<ScriptExecutionLog> CreateAsync(ScriptExecutionLog log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            _context.ScriptExecutionLogs.Add(log);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(log.Id) ?? log;
        }

        /// <summary>
        /// Updates an existing execution log
        /// </summary>
        public async Task<ScriptExecutionLog> UpdateAsync(ScriptExecutionLog log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            var existing = await _context.ScriptExecutionLogs
                .FirstOrDefaultAsync(l => l.Id == log.Id);

            if (existing == null)
                throw new InvalidOperationException($"Execution log with ID {log.Id} not found");

            // Update properties
            existing.CompletedAt = log.CompletedAt;
            existing.Status = log.Status;
            existing.RowsAffected = log.RowsAffected;
            existing.ErrorMessage = log.ErrorMessage;
            existing.ErrorStackTrace = log.ErrorStackTrace;
            existing.ExecutedSql = log.ExecutedSql;
            existing.DurationMs = log.DurationMs;
            existing.MemoryUsageMB = log.MemoryUsageMB;
            existing.ExecutionMetadata = log.ExecutionMetadata;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(existing.Id) ?? existing;
        }

        /// <summary>
        /// Gets recent execution logs across all scripts
        /// </summary>
        public async Task<IEnumerable<ScriptExecutionLog>> GetRecentLogsAsync(int limit = 100)
        {
            return await _context.ScriptExecutionLogs
                .Include(log => log.Script)
                .ThenInclude(s => s!.Product)
                .Include(log => log.Script)
                .ThenInclude(s => s!.PipelineStage)
                .OrderByDescending(log => log.StartedAt)
                .Take(limit)
                .ToListAsync();
        }

        /// <summary>
        /// Gets execution statistics for a script
        /// </summary>
        public async Task<Dictionary<string, object>> GetScriptStatisticsAsync(Guid scriptId)
        {
            var logs = await _context.ScriptExecutionLogs
                .Where(log => log.ScriptId == scriptId)
                .ToListAsync();

            var stats = new Dictionary<string, object>
            {
                ["TotalExecutions"] = logs.Count,
                ["SuccessfulExecutions"] = logs.Count(l => l.Status == ScriptExecutionStatus.Completed),
                ["FailedExecutions"] = logs.Count(l => l.Status == ScriptExecutionStatus.Failed),
                ["AverageDurationMs"] = logs.Where(l => l.DurationMs.HasValue).Select(l => l.DurationMs!.Value).DefaultIfEmpty(0).Average(),
                ["TotalRowsAffected"] = logs.Where(l => l.RowsAffected.HasValue).Sum(l => l.RowsAffected!.Value),
                ["LastExecutedAt"] = logs.MaxBy(l => l.StartedAt)?.StartedAt,
                ["LastStatus"] = logs.MaxBy(l => l.StartedAt)?.Status ?? "Never"
            };

            return stats;
        }

        /// <summary>
        /// Cleans up old execution logs older than specified days
        /// </summary>
        public async Task<int> CleanupOldLogsAsync(int olderThanDays = 90)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            
            var oldLogs = await _context.ScriptExecutionLogs
                .Where(log => log.StartedAt < cutoffDate)
                .ToListAsync();

            if (oldLogs.Any())
            {
                _context.ScriptExecutionLogs.RemoveRange(oldLogs);
                await _context.SaveChangesAsync();
            }

            return oldLogs.Count;
        }
    }
}