using Contracts.Models;
using Contracts.DTOs;
using DAL.Repositories;

namespace API.Services;

public class PipelineExecutionService : IPipelineExecutionService
{
    private readonly IPipelineExecutionRepository _executionRepository;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<PipelineExecutionService> _logger;

    public PipelineExecutionService(
        IPipelineExecutionRepository executionRepository,
        IProductRepository productRepository,
        ILogger<PipelineExecutionService> logger)
    {
        _executionRepository = executionRepository;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<PipelineExecution> StartExecutionAsync(Guid productId, string? initiatedBy = null, bool isDryRun = false)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
        {
            throw new ArgumentException($"Product {productId} not found");
        }

        var execution = new PipelineExecution
        {
            ProductId = productId,
            Status = PipelineExecutionStatus.Queued,
            InitiatedBy = initiatedBy,
            IsDryRun = isDryRun,
            Priority = 5, // Default priority
            TotalStages = 5 // Standard 5 stages
        };

        execution = await _executionRepository.CreateAsync(execution);

        // Pipeline execution is now handled by distributed agents
        // Agents will pick up the execution from the database
        await _executionRepository.UpdateAsync(execution);

        _logger.LogInformation("Started pipeline execution {ExecutionId} for product {ProductId}", 
            execution.Id, productId);

        return execution;
    }

    public async Task<PipelineExecution?> GetExecutionAsync(Guid executionId)
    {
        return await _executionRepository.GetByIdAsync(executionId);
    }

    public async Task<IEnumerable<PipelineExecution>> GetExecutionsAsync(
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? productName = null,
        int limit = 100)
    {
        return await _executionRepository.GetExecutionHistoryAsync(status, startDate, endDate, productName, limit);
    }

    public async Task<IEnumerable<PipelineExecution>> GetActiveExecutionsAsync()
    {
        return await _executionRepository.GetActiveExecutionsAsync();
    }

    public async Task<IEnumerable<PipelineExecution>> GetProductExecutionsAsync(Guid productId)
    {
        return await _executionRepository.GetByProductIdAsync(productId);
    }

    public async Task<bool> PauseExecutionAsync(Guid executionId)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null || !PipelineExecutionStatus.CanTransitionTo(execution.Status, PipelineExecutionStatus.Paused))
        {
            return false;
        }

        // Pipeline cancellation handled by distributed agents

        execution.Status = PipelineExecutionStatus.Paused;
        await _executionRepository.UpdateAsync(execution);

        _logger.LogInformation("Paused pipeline execution {ExecutionId}", executionId);
        return true;
    }

    public async Task<bool> ResumeExecutionAsync(Guid executionId)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null || !PipelineExecutionStatus.CanTransitionTo(execution.Status, PipelineExecutionStatus.Running))
        {
            return false;
        }

        // Resume execution - agents will pick it up
        execution.Status = PipelineExecutionStatus.Queued;
        await _executionRepository.UpdateAsync(execution);

        _logger.LogInformation("Resumed pipeline execution {ExecutionId}", executionId);
        return true;
    }

    public async Task<bool> CancelExecutionAsync(Guid executionId, string? reason = null)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null || !PipelineExecutionStatus.CanTransitionTo(execution.Status, PipelineExecutionStatus.Cancelled))
        {
            return false;
        }

        // Pipeline cancellation handled by distributed agents

        execution.Status = PipelineExecutionStatus.Cancelled;
        execution.CancellationReason = reason;
        execution.CompletedAt = DateTime.UtcNow;
        
        if (execution.StartedAt.HasValue)
        {
            execution.DurationMs = (long)(DateTime.UtcNow - execution.StartedAt.Value).TotalMilliseconds;
        }

        await _executionRepository.UpdateAsync(execution);

        _logger.LogInformation("Cancelled pipeline execution {ExecutionId}: {Reason}", executionId, reason);
        return true;
    }

    public async Task<bool> UpdateExecutionStatusAsync(Guid executionId, string status, string? errorMessage = null)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null)
        {
            return false;
        }

        var oldStatus = execution.Status;
        if (!PipelineExecutionStatus.CanTransitionTo(oldStatus, status))
        {
            _logger.LogWarning("Invalid status transition from {OldStatus} to {NewStatus} for execution {ExecutionId}",
                oldStatus, status, executionId);
            return false;
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

        await _executionRepository.UpdateAsync(execution);
        return true;
    }

    public async Task<bool> UpdateExecutionProgressAsync(Guid executionId, int currentStage, decimal progressPercentage)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null)
        {
            return false;
        }

        execution.CurrentStage = currentStage;
        execution.ProgressPercentage = Math.Min(100, Math.Max(0, progressPercentage));
        
        // Update stages completed based on current stage
        if (currentStage > 1)
        {
            execution.StagesCompleted = currentStage - 1;
        }

        await _executionRepository.UpdateAsync(execution);
        return true;
    }

    public async Task<bool> CompleteExecutionAsync(Guid executionId, long? rowsProcessed = null, long? rowsAffected = null)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null)
        {
            return false;
        }

        execution.Status = PipelineExecutionStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;
        execution.ProgressPercentage = 100;
        execution.StagesCompleted = execution.TotalStages;
        execution.CurrentStage = null;

        if (execution.StartedAt.HasValue)
        {
            execution.DurationMs = (long)(DateTime.UtcNow - execution.StartedAt.Value).TotalMilliseconds;
        }

        if (rowsProcessed.HasValue)
        {
            execution.TotalRowsProcessed = rowsProcessed.Value;
        }

        if (rowsAffected.HasValue)
        {
            execution.TotalRowsAffected = rowsAffected.Value;
        }

        await _executionRepository.UpdateAsync(execution);
        
        _logger.LogInformation("Completed pipeline execution {ExecutionId}", executionId);
        return true;
    }

    public async Task<bool> FailExecutionAsync(Guid executionId, string errorMessage, string? stackTrace = null)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null)
        {
            return false;
        }

        execution.Status = PipelineExecutionStatus.Failed;
        execution.CompletedAt = DateTime.UtcNow;
        execution.ErrorMessage = errorMessage;
        execution.ErrorStackTrace = stackTrace;
        execution.CurrentStage = null;

        if (execution.StartedAt.HasValue)
        {
            execution.DurationMs = (long)(DateTime.UtcNow - execution.StartedAt.Value).TotalMilliseconds;
        }

        // Mark current stage as failed
        execution.StagesFailed = 1;

        await _executionRepository.UpdateAsync(execution);
        
        _logger.LogError("Failed pipeline execution {ExecutionId}: {ErrorMessage}", executionId, errorMessage);
        return true;
    }

    public async Task<PipelineMetricsDto> GetDashboardMetricsAsync()
    {
        var todayCount = await _executionRepository.GetTodayCountAsync();
        var todaySuccessful = await _executionRepository.GetTodaySuccessfulCountAsync();
        var todayFailed = await _executionRepository.GetTodayFailedCountAsync();
        var currentlyRunning = await _executionRepository.GetCountByStatusAsync(PipelineExecutionStatus.Running);

        return new PipelineMetricsDto
        {
            ExecutionsToday = todayCount,
            SuccessfulToday = todaySuccessful,
            FailedToday = todayFailed,
            CurrentlyRunning = currentlyRunning,
            AverageExecutionTimeMinutes = 25.5 // TODO: Calculate from actual data
        };
    }
}