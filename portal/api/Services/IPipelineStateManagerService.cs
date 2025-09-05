using Contracts.DTOs;

namespace API.Services;

public interface IPipelineStateManagerService
{
    Task<bool> CanTransitionPipelineAsync(Guid pipelineExecutionId, string newStatus);
    
    Task<bool> CanTransitionStageAsync(Guid stageExecutionId, string newStatus);
    
    Task<PipelineExecutionDto> TransitionPipelineStateAsync(Guid pipelineExecutionId, string newStatus, string? reason = null);
    
    Task<StageExecutionDto> TransitionStageStateAsync(Guid stageExecutionId, string newStatus, string? reason = null);
    
    Task<IEnumerable<StageExecutionDto>> GetNextExecutableStagesAsync(Guid pipelineExecutionId);
    
    Task UpdatePipelineProgressAsync(Guid pipelineExecutionId);
    
    Task<bool> IsPipelineCompleteAsync(Guid pipelineExecutionId);
    
    Task<string> GetPipelineOverallStatusAsync(Guid pipelineExecutionId);
    
    Task CleanupStaleExecutionsAsync(TimeSpan staleThreshold);
}