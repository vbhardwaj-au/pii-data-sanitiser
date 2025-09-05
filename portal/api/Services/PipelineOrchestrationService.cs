using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DAL;
using Contracts.Models;
using Contracts.DTOs;

namespace API.Services;

public class PipelineOrchestrationService : IPipelineOrchestrationService
{
    private readonly PortalDbContext _context;
    private readonly ILogger<PipelineOrchestrationService> _logger;

    public PipelineOrchestrationService(PortalDbContext context, ILogger<PipelineOrchestrationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PipelineExecutionDto> StartPipelineAsync(Guid productId, string initiatedBy, bool isDryRun = false, Dictionary<string, object>? parameters = null)
    {
        _logger.LogInformation("Starting pipeline for product {ProductId}, initiated by {InitiatedBy}, isDryRun: {IsDryRun}", 
            productId, initiatedBy, isDryRun);

        var product = await _context.Products
            .Include(p => p.ProductPipelineConfigurations)
                .ThenInclude(ppc => ppc.PipelineStage)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

        if (product == null)
        {
            throw new InvalidOperationException($"Active product with ID {productId} not found");
        }

        var enabledStageConfigs = product.ProductPipelineConfigurations
            .Where(ppc => ppc.IsEnabled)
            .OrderBy(ppc => ppc.PipelineStage?.ExecutionOrder ?? 999)
            .ToList();

        if (!enabledStageConfigs.Any())
        {
            throw new InvalidOperationException($"No enabled pipeline stages configured for product {product.Name}");
        }

        var pipelineExecution = new PipelineExecution
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Status = PipelineExecutionStatus.Queued,
            QueuedAt = DateTime.UtcNow,
            InitiatedBy = initiatedBy,
            IsDryRun = isDryRun,
            TotalStages = enabledStageConfigs.Count,
            ExecutionParameters = parameters != null ? JsonSerializer.Serialize(parameters) : null,
            Priority = 5,
            StageExecutions = new List<StageExecution>()
        };

        foreach (var stageConfig in enabledStageConfigs)
        {
            var stageExecution = new StageExecution
            {
                Id = Guid.NewGuid(),
                PipelineExecutionId = pipelineExecution.Id,
                PipelineStageId = stageConfig.PipelineStageId,
                ExecutionOrder = stageConfig.PipelineStage?.ExecutionOrder ?? 999,
                StageName = stageConfig.PipelineStage?.Name ?? "Unknown",
                Status = StageExecutionStatus.Queued,
                QueuedAt = DateTime.UtcNow,
                OnFailureAction = stageConfig.OnFailureAction,
                MaxRetries = stageConfig.RetryAttempts,
                TimeoutMinutes = stageConfig.TimeoutMinutes ?? 60
            };

            pipelineExecution.StageExecutions.Add(stageExecution);
        }

        _context.PipelineExecutions.Add(pipelineExecution);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pipeline execution {ExecutionId} created for product {ProductId} with {StageCount} stages",
            pipelineExecution.Id, productId, enabledStageConfigs.Count);

        return await MapToPipelineExecutionDto(pipelineExecution);
    }

    public async Task<PipelineExecutionDto?> GetPipelineExecutionAsync(Guid executionId)
    {
        var execution = await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
                .ThenInclude(se => se.PipelineStage)
            .FirstOrDefaultAsync(pe => pe.Id == executionId);

        return execution != null ? await MapToPipelineExecutionDto(execution) : null;
    }

    public async Task<IEnumerable<PipelineExecutionDto>> GetPipelineExecutionsAsync(Guid productId, int pageSize = 50, int page = 1)
    {
        var executions = await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
                .ThenInclude(se => se.PipelineStage)
            .Where(pe => pe.ProductId == productId)
            .OrderByDescending(pe => pe.QueuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<PipelineExecutionDto>();
        foreach (var execution in executions)
        {
            result.Add(await MapToPipelineExecutionDto(execution));
        }
        return result;
    }

    public async Task<bool> CancelPipelineAsync(Guid executionId, string cancellationReason)
    {
        var execution = await _context.PipelineExecutions
            .Include(pe => pe.StageExecutions)
            .FirstOrDefaultAsync(pe => pe.Id == executionId);

        if (execution == null)
        {
            return false;
        }

        if (!PipelineExecutionStatus.CanTransitionTo(execution.Status, PipelineExecutionStatus.Cancelled))
        {
            _logger.LogWarning("Cannot cancel pipeline {ExecutionId} - invalid status transition from {CurrentStatus}",
                executionId, execution.Status);
            return false;
        }

        execution.Status = PipelineExecutionStatus.Cancelled;
        execution.CancellationReason = cancellationReason;
        execution.CompletedAt = DateTime.UtcNow;

        if (execution.StartedAt.HasValue)
        {
            execution.DurationMs = (long)(DateTime.UtcNow - execution.StartedAt.Value).TotalMilliseconds;
        }

        foreach (var stage in execution.StageExecutions.Where(se => se.Status == StageExecutionStatus.Queued || se.Status == StageExecutionStatus.Running))
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

        await _context.SaveChangesAsync();
        _logger.LogInformation("Pipeline execution {ExecutionId} cancelled: {Reason}", executionId, cancellationReason);
        
        return true;
    }

    public async Task<bool> PausePipelineAsync(Guid executionId)
    {
        var execution = await _context.PipelineExecutions.FirstOrDefaultAsync(pe => pe.Id == executionId);
        
        if (execution == null || !PipelineExecutionStatus.CanTransitionTo(execution.Status, PipelineExecutionStatus.Paused))
        {
            return false;
        }

        execution.Status = PipelineExecutionStatus.Paused;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Pipeline execution {ExecutionId} paused", executionId);
        return true;
    }

    public async Task<bool> ResumePipelineAsync(Guid executionId)
    {
        var execution = await _context.PipelineExecutions.FirstOrDefaultAsync(pe => pe.Id == executionId);
        
        if (execution == null || !PipelineExecutionStatus.CanTransitionTo(execution.Status, PipelineExecutionStatus.Running))
        {
            return false;
        }

        execution.Status = PipelineExecutionStatus.Running;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Pipeline execution {ExecutionId} resumed", executionId);
        return true;
    }

    public async Task<PipelineExecutionDto> UpdatePipelineStatusAsync(Guid executionId, string status, string? errorMessage = null)
    {
        var execution = await _context.PipelineExecutions
            .Include(pe => pe.Product)
            .Include(pe => pe.StageExecutions)
                .ThenInclude(se => se.PipelineStage)
            .FirstOrDefaultAsync(pe => pe.Id == executionId);

        if (execution == null)
        {
            throw new InvalidOperationException($"Pipeline execution {executionId} not found");
        }

        if (!PipelineExecutionStatus.CanTransitionTo(execution.Status, status))
        {
            throw new InvalidOperationException($"Cannot transition pipeline execution from {execution.Status} to {status}");
        }

        execution.Status = status;
        
        if (status == PipelineExecutionStatus.Running && !execution.StartedAt.HasValue)
        {
            execution.StartedAt = DateTime.UtcNow;
        }
        
        if (PipelineExecutionStatus.IsTerminal(status))
        {
            execution.CompletedAt = DateTime.UtcNow;
            if (execution.StartedAt.HasValue)
            {
                execution.DurationMs = (long)(DateTime.UtcNow - execution.StartedAt.Value).TotalMilliseconds;
            }
        }
        
        if (!string.IsNullOrEmpty(errorMessage))
        {
            execution.ErrorMessage = errorMessage;
        }

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Pipeline execution {ExecutionId} status updated to {Status}", executionId, status);
        
        return await MapToPipelineExecutionDto(execution);
    }

    public async Task<StageExecutionDto> StartStageAsync(Guid pipelineExecutionId, Guid stageId, int executionOrder)
    {
        var stageExecution = await _context.StageExecutions
            .Include(se => se.PipelineStage)
            .FirstOrDefaultAsync(se => se.PipelineExecutionId == pipelineExecutionId && se.PipelineStageId == stageId);

        if (stageExecution == null)
        {
            throw new InvalidOperationException($"Stage execution not found for pipeline {pipelineExecutionId} and stage {stageId}");
        }

        if (!StageExecutionStatus.CanTransitionTo(stageExecution.Status, StageExecutionStatus.Running))
        {
            throw new InvalidOperationException($"Cannot start stage execution - invalid status transition from {stageExecution.Status}");
        }

        stageExecution.Status = StageExecutionStatus.Running;
        stageExecution.StartedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Stage execution {StageExecutionId} started for stage {StageName}", 
            stageExecution.Id, stageExecution.StageName);
        
        return MapToStageExecutionDto(stageExecution);
    }

    public async Task<StageExecutionDto> UpdateStageStatusAsync(Guid stageExecutionId, string status, string? errorMessage = null)
    {
        var stageExecution = await _context.StageExecutions
            .Include(se => se.PipelineStage)
            .FirstOrDefaultAsync(se => se.Id == stageExecutionId);

        if (stageExecution == null)
        {
            throw new InvalidOperationException($"Stage execution {stageExecutionId} not found");
        }

        if (!StageExecutionStatus.CanTransitionTo(stageExecution.Status, status))
        {
            throw new InvalidOperationException($"Cannot transition stage execution from {stageExecution.Status} to {status}");
        }

        stageExecution.Status = status;
        
        if (StageExecutionStatus.IsTerminal(status))
        {
            stageExecution.CompletedAt = DateTime.UtcNow;
            if (stageExecution.StartedAt.HasValue)
            {
                stageExecution.DurationMs = (long)(DateTime.UtcNow - stageExecution.StartedAt.Value).TotalMilliseconds;
            }
        }
        
        if (!string.IsNullOrEmpty(errorMessage))
        {
            stageExecution.ErrorMessage = errorMessage;
        }

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Stage execution {StageExecutionId} status updated to {Status}", stageExecutionId, status);
        
        return MapToStageExecutionDto(stageExecution);
    }

    public async Task<StageExecutionDto> UpdateStageProgressAsync(Guid stageExecutionId, decimal progressPercentage, long? rowsProcessed = null)
    {
        var stageExecution = await _context.StageExecutions
            .Include(se => se.PipelineStage)
            .FirstOrDefaultAsync(se => se.Id == stageExecutionId);

        if (stageExecution == null)
        {
            throw new InvalidOperationException($"Stage execution {stageExecutionId} not found");
        }

        stageExecution.ProgressPercentage = Math.Max(0, Math.Min(100, progressPercentage));
        
        if (rowsProcessed.HasValue)
        {
            stageExecution.RowsProcessed = rowsProcessed.Value;
        }

        await _context.SaveChangesAsync();
        
        return MapToStageExecutionDto(stageExecution);
    }

    public async Task<StageExecutionDto?> GetStageExecutionAsync(Guid stageExecutionId)
    {
        var stageExecution = await _context.StageExecutions
            .Include(se => se.PipelineStage)
            .FirstOrDefaultAsync(se => se.Id == stageExecutionId);

        return stageExecution != null ? MapToStageExecutionDto(stageExecution) : null;
    }

    public async Task UpdatePipelineMetricsAsync(Guid pipelineExecutionId, int completedStages, int failedStages, int skippedStages, long totalRowsProcessed, long totalRowsAffected, decimal overallProgress)
    {
        var execution = await _context.PipelineExecutions.FirstOrDefaultAsync(pe => pe.Id == pipelineExecutionId);
        if (execution != null)
        {
            execution.StagesCompleted = completedStages;
            execution.StagesFailed = failedStages;
            execution.StagesSkipped = skippedStages;
            execution.TotalRowsProcessed = totalRowsProcessed;
            execution.TotalRowsAffected = totalRowsAffected;
            execution.ProgressPercentage = Math.Max(0, Math.Min(100, overallProgress));

            // Update current stage indicator
            var currentRunningStage = await _context.StageExecutions
                .Where(se => se.PipelineExecutionId == pipelineExecutionId && se.Status == StageExecutionStatus.Running)
                .OrderBy(se => se.ExecutionOrder)
                .FirstOrDefaultAsync();
            
            execution.CurrentStage = currentRunningStage?.ExecutionOrder;

            await _context.SaveChangesAsync();
        }
    }

    public async Task CleanupCompletedExecutionsAsync(TimeSpan retentionPeriod)
    {
        var cutoffDate = DateTime.UtcNow - retentionPeriod;
        
        var expiredExecutions = await _context.PipelineExecutions
            .Where(pe => pe.CompletedAt.HasValue && pe.CompletedAt.Value < cutoffDate)
            .CountAsync();

        if (expiredExecutions > 0)
        {
            await _context.PipelineExecutions
                .Where(pe => pe.CompletedAt.HasValue && pe.CompletedAt.Value < cutoffDate)
                .ExecuteDeleteAsync();
                
            _logger.LogInformation("Cleaned up {Count} expired pipeline executions older than {CutoffDate}", 
                expiredExecutions, cutoffDate);
        }
    }

    private async Task<PipelineExecutionDto> MapToPipelineExecutionDto(PipelineExecution execution)
    {
        return new PipelineExecutionDto
        {
            Id = execution.Id,
            ProductId = execution.ProductId,
            ProductName = execution.Product?.Name,
            Status = execution.Status,
            QueuedAt = execution.QueuedAt,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            DurationMs = execution.DurationMs,
            CurrentStage = execution.CurrentStage,
            TotalStages = execution.TotalStages,
            StagesCompleted = execution.StagesCompleted,
            StagesFailed = execution.StagesFailed,
            StagesSkipped = execution.StagesSkipped,
            ProgressPercentage = execution.ProgressPercentage,
            ErrorMessage = execution.ErrorMessage,
            InitiatedBy = execution.InitiatedBy,
            ConnectionType = execution.ConnectionType,
            TotalRowsProcessed = execution.TotalRowsProcessed,
            TotalRowsAffected = execution.TotalRowsAffected,
            Priority = execution.Priority,
            Tags = execution.Tags,
            IsDryRun = execution.IsDryRun,
            StageExecutions = execution.StageExecutions?.Select(MapToStageExecutionDto).ToList() ?? new List<StageExecutionDto>()
        };
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