using Contracts.Models;
using Contracts.DTOs;

namespace API.Services
{
    /// <summary>
    /// Service interface for logging script execution details
    /// </summary>
    public interface IScriptExecutionLoggingService
    {
        /// <summary>
        /// Starts logging for a script execution
        /// </summary>
        Task<Guid> StartExecutionLogAsync(Guid scriptId, Guid? pipelineExecutionId, string? executedBy, string? connectionType = null);

        /// <summary>
        /// Completes an execution log with results
        /// </summary>
        Task CompleteExecutionLogAsync(Guid logId, string status, long? rowsAffected = null, string? errorMessage = null, string? errorStackTrace = null);

        /// <summary>
        /// Updates execution log with progress information
        /// </summary>
        Task UpdateExecutionProgressAsync(Guid logId, string? executedSql = null, decimal? memoryUsageMB = null, Dictionary<string, object>? metadata = null);

        /// <summary>
        /// Gets execution logs for a script
        /// </summary>
        Task<IEnumerable<ScriptExecutionLogDto>> GetScriptLogsAsync(Guid scriptId, int limit = 50);

        /// <summary>
        /// Gets execution statistics for a script
        /// </summary>
        Task<Dictionary<string, object>> GetScriptStatisticsAsync(Guid scriptId);

        /// <summary>
        /// Gets recent execution logs across all scripts
        /// </summary>
        Task<IEnumerable<ScriptExecutionLogDto>> GetRecentLogsAsync(int limit = 100);

        /// <summary>
        /// Cleans up old execution logs
        /// </summary>
        Task<int> CleanupOldLogsAsync(int olderThanDays = 90);
    }
}