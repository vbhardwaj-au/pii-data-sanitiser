using Contracts.DTOs;

namespace API.Services;

public interface IStageExecutionService
{
    Task<StageExecutionDto> ExecuteStageAsync(Guid stageExecutionId, CancellationToken cancellationToken = default);
    
    Task<bool> ValidateStagePrerequisitesAsync(Guid stageExecutionId);
    
    Task<IEnumerable<Guid>> GetStageScriptsAsync(Guid productId, Guid stageId);
    
    Task<bool> ExecuteScriptAsync(Guid scriptId, Guid stageExecutionId, string connectionString);
    
    Task UpdateStageMetricsAsync(Guid stageExecutionId, long rowsProcessed, long rowsAffected, decimal memoryUsageMB, decimal cpuUsagePercentage);
}