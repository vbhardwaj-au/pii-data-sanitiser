using Contracts.Models;
using Contracts.DTOs;

namespace API.Services;

public interface IPipelineOrchestrationService
{
    Task<PipelineExecutionDto> StartPipelineAsync(Guid productId, string initiatedBy, bool isDryRun = false, Dictionary<string, object>? parameters = null);
    
    Task<PipelineExecutionDto?> GetPipelineExecutionAsync(Guid executionId);
    
    Task<IEnumerable<PipelineExecutionDto>> GetPipelineExecutionsAsync(Guid productId, int pageSize = 50, int page = 1);
    
    Task<bool> CancelPipelineAsync(Guid executionId, string cancellationReason);
    
    Task<bool> PausePipelineAsync(Guid executionId);
    
    Task<bool> ResumePipelineAsync(Guid executionId);
    
    Task<PipelineExecutionDto> UpdatePipelineStatusAsync(Guid executionId, string status, string? errorMessage = null);
    
    Task<StageExecutionDto> StartStageAsync(Guid pipelineExecutionId, Guid stageId, int executionOrder);
    
    Task<StageExecutionDto> UpdateStageStatusAsync(Guid stageExecutionId, string status, string? errorMessage = null);
    
    Task<StageExecutionDto> UpdateStageProgressAsync(Guid stageExecutionId, decimal progressPercentage, long? rowsProcessed = null);
    
    Task<StageExecutionDto?> GetStageExecutionAsync(Guid stageExecutionId);
    
    Task UpdatePipelineMetricsAsync(Guid pipelineExecutionId, int completedStages, int failedStages, int skippedStages, long totalRowsProcessed, long totalRowsAffected, decimal overallProgress);
    
    Task CleanupCompletedExecutionsAsync(TimeSpan retentionPeriod);
}