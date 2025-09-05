using Microsoft.EntityFrameworkCore;
using DAL;
using Contracts.Models;
using Contracts.DTOs;

namespace API.Services;

public class PipelineStateManagerService : IPipelineStateManagerService
{
    private readonly PortalDbContext _context;
    private readonly IPipelineOrchestrationService _orchestrationService;
    private readonly ILogger<PipelineStateManagerService> _logger;

    public PipelineStateManagerService(
        PortalDbContext context,
        IPipelineOrchestrationService orchestrationService,
        ILogger<PipelineStateManagerService> logger)
    {
        _context = context;
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    public async Task<bool> CanTransitionPipelineAsync(Guid pipelineExecutionId, string newStatus)
    {
        var execution = await _context.PipelineExecutions
            .FirstOrDefaultAsync(pe => pe.Id == pipelineExecutionId);

        if (execution == null)
        {
            return false;
        }

        return PipelineExecutionStatus.CanTransitionTo(execution.Status, newStatus);
    }

    public async Task<bool> CanTransitionStageAsync(Guid stageExecutionId, string newStatus)
    {
        var stageExecution = await _context.StageExecutions
            .FirstOrDefaultAsync(se => se.Id == stageExecutionId);

        if (stageExecution == null)
        {
            return false;
        }

        return StageExecutionStatus.CanTransitionTo(stageExecution.Status, newStatus);
    }

    public async Task<PipelineExecutionDto> TransitionPipelineStateAsync(Guid pipelineExecutionId, string newStatus, string? reason = null)
    {
        _logger.LogInformation("Attempting to transition pipeline {PipelineExecutionId} to status {NewStatus}",
            pipelineExecutionId, newStatus);

        if (!await CanTransitionPipelineAsync(pipelineExecutionId, newStatus))
        {
            throw new InvalidOperationException($"Invalid state transition for pipeline {pipelineExecutionId} to {newStatus}");
        }

        var execution = await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
                .ThenInclude(se => se.PipelineStage)
            .FirstOrDefaultAsync(pe => pe.Id == pipelineExecutionId);

        if (execution == null)
        {
            throw new InvalidOperationException($"Pipeline execution {pipelineExecutionId} not found");
        }

        var previousStatus = execution.Status;
        execution.Status = newStatus;

        // Handle state-specific actions
        switch (newStatus)
        {
            case PipelineExecutionStatus.Running:
                if (!execution.StartedAt.HasValue)
                {
                    execution.StartedAt = DateTime.UtcNow;
                }
                break;

            case PipelineExecutionStatus.Paused:
                // Log pause reason if provided
                if (!string.IsNullOrEmpty(reason))
                {
                    _logger.LogInformation("Pipeline {PipelineExecutionId} paused: {Reason}", pipelineExecutionId, reason);
                }
                break;

            case PipelineExecutionStatus.Cancelled:
                execution.CompletedAt = DateTime.UtcNow;
                execution.CancellationReason = reason;
                
                if (execution.StartedAt.HasValue)
                {
                    execution.DurationMs = (long)(DateTime.UtcNow - execution.StartedAt.Value).TotalMilliseconds;
                }

                // Cancel any running or queued stages
                await CancelRemainingStagesAsync(execution);
                break;

            case PipelineExecutionStatus.Completed:
            case PipelineExecutionStatus.Failed:
                execution.CompletedAt = DateTime.UtcNow;
                
                if (execution.StartedAt.HasValue)
                {
                    execution.DurationMs = (long)(DateTime.UtcNow - execution.StartedAt.Value).TotalMilliseconds;
                }

                // Update final statistics
                await UpdateFinalStatisticsAsync(execution);
                break;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Pipeline {PipelineExecutionId} transitioned from {PreviousStatus} to {NewStatus}",
            pipelineExecutionId, previousStatus, newStatus);

        return await _orchestrationService.GetPipelineExecutionAsync(pipelineExecutionId) 
            ?? throw new InvalidOperationException("Failed to retrieve updated pipeline execution");
    }

    public async Task<StageExecutionDto> TransitionStageStateAsync(Guid stageExecutionId, string newStatus, string? reason = null)
    {
        _logger.LogInformation("Attempting to transition stage {StageExecutionId} to status {NewStatus}",
            stageExecutionId, newStatus);

        if (!await CanTransitionStageAsync(stageExecutionId, newStatus))
        {
            throw new InvalidOperationException($"Invalid state transition for stage {stageExecutionId} to {newStatus}");
        }

        var stageExecution = await _context.StageExecutions
            .Include(se => se.PipelineStage)
            .FirstOrDefaultAsync(se => se.Id == stageExecutionId);

        if (stageExecution == null)
        {
            throw new InvalidOperationException($"Stage execution {stageExecutionId} not found");
        }

        var previousStatus = stageExecution.Status;
        stageExecution.Status = newStatus;

        // Handle state-specific actions
        switch (newStatus)
        {
            case StageExecutionStatus.Running:
                if (!stageExecution.StartedAt.HasValue)
                {
                    stageExecution.StartedAt = DateTime.UtcNow;
                }
                break;

            case StageExecutionStatus.Completed:
            case StageExecutionStatus.Failed:
            case StageExecutionStatus.Cancelled:
            case StageExecutionStatus.Skipped:
                stageExecution.CompletedAt = DateTime.UtcNow;
                
                if (stageExecution.StartedAt.HasValue)
                {
                    stageExecution.DurationMs = (long)(DateTime.UtcNow - stageExecution.StartedAt.Value).TotalMilliseconds;
                }

                if (newStatus == StageExecutionStatus.Failed && !string.IsNullOrEmpty(reason))
                {
                    stageExecution.ErrorMessage = reason;
                }
                break;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Stage {StageExecutionId} transitioned from {PreviousStatus} to {NewStatus}",
            stageExecutionId, previousStatus, newStatus);

        // Update parent pipeline progress
        await UpdatePipelineProgressAsync(stageExecution.PipelineExecutionId);

        return await _orchestrationService.UpdateStageStatusAsync(stageExecutionId, newStatus, reason);
    }

    public async Task<IEnumerable<StageExecutionDto>> GetNextExecutableStagesAsync(Guid pipelineExecutionId)
    {
        var execution = await _context.PipelineExecutions
            .Include(pe => pe.StageExecutions)
                .ThenInclude(se => se.PipelineStage)
            .FirstOrDefaultAsync(pe => pe.Id == pipelineExecutionId);

        if (execution == null)
        {
            return Enumerable.Empty<StageExecutionDto>();
        }

        var allStages = execution.StageExecutions.OrderBy(se => se.ExecutionOrder).ToList();
        var executableStages = new List<StageExecution>();

        foreach (var stage in allStages)
        {
            if (stage.Status != StageExecutionStatus.Queued)
            {
                continue;
            }

            // Check if all prerequisite stages are completed
            var prerequisiteStages = allStages.Where(s => s.ExecutionOrder < stage.ExecutionOrder);
            var canExecute = prerequisiteStages.All(ps => 
                ps.Status == StageExecutionStatus.Completed || 
                ps.Status == StageExecutionStatus.Skipped);

            if (canExecute)
            {
                executableStages.Add(stage);
            }
        }

        return executableStages.Select(MapToStageExecutionDto);
    }

    public async Task UpdatePipelineProgressAsync(Guid pipelineExecutionId)
    {
        var execution = await _context.PipelineExecutions
            .Include(pe => pe.StageExecutions)
            .FirstOrDefaultAsync(pe => pe.Id == pipelineExecutionId);

        if (execution == null)
        {
            return;
        }

        var totalStages = execution.StageExecutions.Count;
        if (totalStages == 0)
        {
            return;
        }

        var completedStages = execution.StageExecutions.Count(se => se.Status == StageExecutionStatus.Completed);
        var failedStages = execution.StageExecutions.Count(se => se.Status == StageExecutionStatus.Failed);
        var skippedStages = execution.StageExecutions.Count(se => se.Status == StageExecutionStatus.Skipped);
        var runningStages = execution.StageExecutions.Count(se => se.Status == StageExecutionStatus.Running);

        // Update counts
        execution.StagesCompleted = completedStages;
        execution.StagesFailed = failedStages;
        execution.StagesSkipped = skippedStages;

        // Calculate overall progress
        var finishedStages = completedStages + failedStages + skippedStages;
        execution.ProgressPercentage = totalStages > 0 ? (decimal)finishedStages / totalStages * 100 : 0;

        // Update current stage
        var currentRunningStage = execution.StageExecutions
            .Where(se => se.Status == StageExecutionStatus.Running)
            .OrderBy(se => se.ExecutionOrder)
            .FirstOrDefault();

        execution.CurrentStage = currentRunningStage?.ExecutionOrder;

        // Aggregate row statistics
        execution.TotalRowsProcessed = execution.StageExecutions.Sum(se => se.RowsProcessed ?? 0);
        execution.TotalRowsAffected = execution.StageExecutions.Sum(se => se.RowsAffected ?? 0);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated pipeline {PipelineExecutionId} progress: {Progress}%, Current Stage: {CurrentStage}",
            pipelineExecutionId, execution.ProgressPercentage, execution.CurrentStage);
    }

    public async Task<bool> IsPipelineCompleteAsync(Guid pipelineExecutionId)
    {
        var execution = await _context.PipelineExecutions
            .Include(pe => pe.StageExecutions)
            .FirstOrDefaultAsync(pe => pe.Id == pipelineExecutionId);

        if (execution == null)
        {
            return false;
        }

        return execution.StageExecutions.All(se => 
            se.Status == StageExecutionStatus.Completed ||
            se.Status == StageExecutionStatus.Failed ||
            se.Status == StageExecutionStatus.Cancelled ||
            se.Status == StageExecutionStatus.Skipped);
    }

    public async Task<string> GetPipelineOverallStatusAsync(Guid pipelineExecutionId)
    {
        var execution = await _context.PipelineExecutions
            .Include(pe => pe.StageExecutions)
            .FirstOrDefaultAsync(pe => pe.Id == pipelineExecutionId);

        if (execution == null)
        {
            return PipelineExecutionStatus.Failed;
        }

        if (!await IsPipelineCompleteAsync(pipelineExecutionId))
        {
            return execution.Status;
        }

        // Determine final status based on stage outcomes
        var hasFailedStages = execution.StageExecutions.Any(se => se.Status == StageExecutionStatus.Failed);
        var hasCompletedStages = execution.StageExecutions.Any(se => se.Status == StageExecutionStatus.Completed);

        if (hasFailedStages)
        {
            return PipelineExecutionStatus.Failed;
        }

        if (hasCompletedStages)
        {
            return PipelineExecutionStatus.Completed;
        }

        // All stages were skipped or cancelled
        return execution.Status == PipelineExecutionStatus.Cancelled 
            ? PipelineExecutionStatus.Cancelled 
            : PipelineExecutionStatus.Completed;
    }

    public async Task CleanupStaleExecutionsAsync(TimeSpan staleThreshold)
    {
        var staleTime = DateTime.UtcNow - staleThreshold;

        // Find executions that have been running too long without updates
        var staleExecutions = await _context.PipelineExecutions
            .Include(pe => pe.StageExecutions)
            .Where(pe => (pe.Status == PipelineExecutionStatus.Running || pe.Status == PipelineExecutionStatus.Queued) &&
                        pe.QueuedAt < staleTime)
            .ToListAsync();

        foreach (var execution in staleExecutions)
        {
            _logger.LogWarning("Found stale pipeline execution {PipelineExecutionId}, status: {Status}, queued at: {QueuedAt}",
                execution.Id, execution.Status, execution.QueuedAt);

            try
            {
                await TransitionPipelineStateAsync(execution.Id, PipelineExecutionStatus.Failed, 
                    "Pipeline marked as failed due to staleness");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup stale pipeline execution {PipelineExecutionId}", execution.Id);
            }
        }

        if (staleExecutions.Any())
        {
            _logger.LogInformation("Cleaned up {Count} stale pipeline executions", staleExecutions.Count);
        }
    }

    private async Task CancelRemainingStagesAsync(PipelineExecution execution)
    {
        var stagesToCancel = execution.StageExecutions
            .Where(se => se.Status == StageExecutionStatus.Queued || se.Status == StageExecutionStatus.Running)
            .ToList();

        foreach (var stage in stagesToCancel)
        {
            if (StageExecutionStatus.CanTransitionTo(stage.Status, StageExecutionStatus.Cancelled))
            {
                stage.Status = StageExecutionStatus.Cancelled;
                stage.CompletedAt = DateTime.UtcNow;
                
                if (stage.StartedAt.HasValue)
                {
                    stage.DurationMs = (long)(DateTime.UtcNow - stage.StartedAt.Value).TotalMilliseconds;
                }
            }
        }
    }

    private async Task UpdateFinalStatisticsAsync(PipelineExecution execution)
    {
        execution.TotalRowsProcessed = execution.StageExecutions.Sum(se => se.RowsProcessed ?? 0);
        execution.TotalRowsAffected = execution.StageExecutions.Sum(se => se.RowsAffected ?? 0);
        execution.StagesCompleted = execution.StageExecutions.Count(se => se.Status == StageExecutionStatus.Completed);
        execution.StagesFailed = execution.StageExecutions.Count(se => se.Status == StageExecutionStatus.Failed);
        execution.StagesSkipped = execution.StageExecutions.Count(se => se.Status == StageExecutionStatus.Skipped);
        execution.ProgressPercentage = 100;
    }

    private static StageExecutionDto MapToStageExecutionDto(StageExecution stageExecution)
    {
        return new StageExecutionDto
        {
            Id = stageExecution.Id,
            PipelineExecutionId = stageExecution.PipelineExecutionId,
            PipelineStageId = stageExecution.PipelineStageId,
            PipelineStage = stageExecution.PipelineStage != null ? new PipelineStageDto
            {
                Id = stageExecution.PipelineStage.Id,
                Name = stageExecution.PipelineStage.Name,
                DisplayName = stageExecution.PipelineStage.DisplayName,
                Description = stageExecution.PipelineStage.Description,
                ExecutionOrder = stageExecution.PipelineStage.ExecutionOrder,
                IsActive = stageExecution.PipelineStage.IsActive,
                IsRequired = stageExecution.PipelineStage.IsRequired
            } : null,
            ExecutionOrder = stageExecution.ExecutionOrder,
            StageName = stageExecution.StageName,
            Status = stageExecution.Status,
            QueuedAt = stageExecution.QueuedAt,
            StartedAt = stageExecution.StartedAt,
            CompletedAt = stageExecution.CompletedAt,
            DurationMs = stageExecution.DurationMs,
            TotalScripts = stageExecution.TotalScripts,
            ScriptsCompleted = stageExecution.ScriptsCompleted,
            ScriptsFailed = stageExecution.ScriptsFailed,
            ScriptsSkipped = stageExecution.ScriptsSkipped,
            ProgressPercentage = stageExecution.ProgressPercentage,
            RowsProcessed = stageExecution.RowsProcessed,
            RowsAffected = stageExecution.RowsAffected,
            ErrorMessage = stageExecution.ErrorMessage,
            OnFailureAction = stageExecution.OnFailureAction,
            RetryAttempts = stageExecution.RetryAttempts,
            MaxRetries = stageExecution.MaxRetries,
            TimeoutMinutes = stageExecution.TimeoutMinutes,
            SupportsParallelExecution = stageExecution.SupportsParallelExecution,
            ExecutedInParallel = stageExecution.ExecutedInParallel,
            MemoryUsageMB = stageExecution.MemoryUsageMB,
            CpuUsagePercentage = stageExecution.CpuUsagePercentage,
            OutputSummary = stageExecution.OutputSummary,
            Warnings = stageExecution.Warnings
        };
    }
}