using AutoMapper;
using Contracts.Models;
using Contracts.DTOs;
using DAL.Repositories;
using System.Text.Json;

namespace API.Services
{
    /// <summary>
    /// Service for logging script execution details
    /// </summary>
    public class ScriptExecutionLoggingService : IScriptExecutionLoggingService
    {
        private readonly IScriptExecutionLogRepository _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<ScriptExecutionLoggingService> _logger;

        public ScriptExecutionLoggingService(
            IScriptExecutionLogRepository repository,
            IMapper mapper,
            ILogger<ScriptExecutionLoggingService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts logging for a script execution
        /// </summary>
        public async Task<Guid> StartExecutionLogAsync(Guid scriptId, Guid? pipelineExecutionId, string? executedBy, string? connectionType = null)
        {
            var log = new ScriptExecutionLog
            {
                Id = Guid.NewGuid(),
                ScriptId = scriptId,
                PipelineExecutionId = pipelineExecutionId,
                StartedAt = DateTime.UtcNow,
                Status = ScriptExecutionStatus.Running,
                ExecutedBy = executedBy,
                ConnectionType = connectionType
            };

            var created = await _repository.CreateAsync(log);
            
            _logger.LogInformation("Started execution log {LogId} for script {ScriptId}", created.Id, scriptId);
            
            return created.Id;
        }

        /// <summary>
        /// Completes an execution log with results
        /// </summary>
        public async Task CompleteExecutionLogAsync(Guid logId, string status, long? rowsAffected = null, string? errorMessage = null, string? errorStackTrace = null)
        {
            var log = await _repository.GetByIdAsync(logId);
            if (log == null)
            {
                _logger.LogWarning("Execution log {LogId} not found for completion", logId);
                return;
            }

            log.CompletedAt = DateTime.UtcNow;
            log.Status = status;
            log.RowsAffected = rowsAffected;
            log.ErrorMessage = errorMessage;
            log.ErrorStackTrace = errorStackTrace;
            
            if (log.StartedAt != default && log.CompletedAt.HasValue)
            {
                log.DurationMs = (long)(log.CompletedAt.Value - log.StartedAt).TotalMilliseconds;
            }

            await _repository.UpdateAsync(log);
            
            _logger.LogInformation("Completed execution log {LogId} with status {Status} in {Duration}ms", 
                logId, status, log.DurationMs);
        }

        /// <summary>
        /// Updates execution log with progress information
        /// </summary>
        public async Task UpdateExecutionProgressAsync(Guid logId, string? executedSql = null, decimal? memoryUsageMB = null, Dictionary<string, object>? metadata = null)
        {
            var log = await _repository.GetByIdAsync(logId);
            if (log == null)
            {
                _logger.LogWarning("Execution log {LogId} not found for progress update", logId);
                return;
            }

            if (!string.IsNullOrEmpty(executedSql))
                log.ExecutedSql = executedSql;
                
            if (memoryUsageMB.HasValue)
                log.MemoryUsageMB = memoryUsageMB;

            if (metadata != null)
            {
                log.ExecutionMetadata = JsonSerializer.Serialize(metadata);
            }

            await _repository.UpdateAsync(log);
        }

        /// <summary>
        /// Gets execution logs for a script
        /// </summary>
        public async Task<IEnumerable<ScriptExecutionLogDto>> GetScriptLogsAsync(Guid scriptId, int limit = 50)
        {
            var logs = await _repository.GetByScriptIdAsync(scriptId, limit);
            
            var dtos = new List<ScriptExecutionLogDto>();
            foreach (var log in logs)
            {
                var dto = _mapper.Map<ScriptExecutionLogDto>(log);
                
                // Parse metadata if available
                if (!string.IsNullOrEmpty(log.ExecutionMetadata))
                {
                    try
                    {
                        dto.ExecutionMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(log.ExecutionMetadata) ?? new();
                    }
                    catch (JsonException)
                    {
                        dto.ExecutionMetadata = new Dictionary<string, object> { ["rawMetadata"] = log.ExecutionMetadata };
                    }
                }

                dtos.Add(dto);
            }

            return dtos;
        }

        /// <summary>
        /// Gets execution statistics for a script
        /// </summary>
        public async Task<Dictionary<string, object>> GetScriptStatisticsAsync(Guid scriptId)
        {
            return await _repository.GetScriptStatisticsAsync(scriptId);
        }

        /// <summary>
        /// Gets recent execution logs across all scripts
        /// </summary>
        public async Task<IEnumerable<ScriptExecutionLogDto>> GetRecentLogsAsync(int limit = 100)
        {
            var logs = await _repository.GetRecentLogsAsync(limit);
            
            var dtos = new List<ScriptExecutionLogDto>();
            foreach (var log in logs)
            {
                var dto = _mapper.Map<ScriptExecutionLogDto>(log);
                dto.Script = _mapper.Map<PipelineStageScriptDto>(log.Script);
                
                if (!string.IsNullOrEmpty(log.ExecutionMetadata))
                {
                    try
                    {
                        dto.ExecutionMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(log.ExecutionMetadata) ?? new();
                    }
                    catch (JsonException)
                    {
                        dto.ExecutionMetadata = new Dictionary<string, object> { ["rawMetadata"] = log.ExecutionMetadata };
                    }
                }

                dtos.Add(dto);
            }

            return dtos;
        }

        /// <summary>
        /// Cleans up old execution logs
        /// </summary>
        public async Task<int> CleanupOldLogsAsync(int olderThanDays = 90)
        {
            var deletedCount = await _repository.CleanupOldLogsAsync(olderThanDays);
            
            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old execution logs older than {Days} days", deletedCount, olderThanDays);
            }

            return deletedCount;
        }
    }
}