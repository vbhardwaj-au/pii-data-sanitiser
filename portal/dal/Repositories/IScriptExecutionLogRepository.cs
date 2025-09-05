using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository interface for managing script execution logs
    /// </summary>
    public interface IScriptExecutionLogRepository
    {
        /// <summary>
        /// Gets execution logs for a specific script
        /// </summary>
        Task<IEnumerable<ScriptExecutionLog>> GetByScriptIdAsync(Guid scriptId, int limit = 50);

        /// <summary>
        /// Gets execution logs for a pipeline execution
        /// </summary>
        Task<IEnumerable<ScriptExecutionLog>> GetByPipelineExecutionIdAsync(Guid pipelineExecutionId);

        /// <summary>
        /// Gets a specific execution log by ID
        /// </summary>
        Task<ScriptExecutionLog?> GetByIdAsync(Guid id);

        /// <summary>
        /// Creates a new execution log
        /// </summary>
        Task<ScriptExecutionLog> CreateAsync(ScriptExecutionLog log);

        /// <summary>
        /// Updates an existing execution log
        /// </summary>
        Task<ScriptExecutionLog> UpdateAsync(ScriptExecutionLog log);

        /// <summary>
        /// Gets recent execution logs across all scripts
        /// </summary>
        Task<IEnumerable<ScriptExecutionLog>> GetRecentLogsAsync(int limit = 100);

        /// <summary>
        /// Gets execution statistics for a script
        /// </summary>
        Task<Dictionary<string, object>> GetScriptStatisticsAsync(Guid scriptId);

        /// <summary>
        /// Cleans up old execution logs older than specified days
        /// </summary>
        Task<int> CleanupOldLogsAsync(int olderThanDays = 90);
    }
}