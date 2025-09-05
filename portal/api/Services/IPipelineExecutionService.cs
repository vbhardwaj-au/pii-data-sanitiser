using Contracts.Models;
using Contracts.DTOs;

namespace API.Services;

public interface IPipelineExecutionService
{
    /// <summary>
    /// Start a new pipeline execution for a product
    /// </summary>
    Task<PipelineExecution> StartExecutionAsync(Guid productId, string? initiatedBy = null, bool isDryRun = false);
    
    /// <summary>
    /// Get execution by ID
    /// </summary>
    Task<PipelineExecution?> GetExecutionAsync(Guid executionId);
    
    /// <summary>
    /// Get all executions with optional filters
    /// </summary>
    Task<IEnumerable<PipelineExecution>> GetExecutionsAsync(
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? productName = null,
        int limit = 100);
    
    /// <summary>
    /// Get current active executions
    /// </summary>
    Task<IEnumerable<PipelineExecution>> GetActiveExecutionsAsync();
    
    /// <summary>
    /// Get executions for a specific product
    /// </summary>
    Task<IEnumerable<PipelineExecution>> GetProductExecutionsAsync(Guid productId);
    
    /// <summary>
    /// Pause a running execution
    /// </summary>
    Task<bool> PauseExecutionAsync(Guid executionId);
    
    /// <summary>
    /// Resume a paused execution
    /// </summary>
    Task<bool> ResumeExecutionAsync(Guid executionId);
    
    /// <summary>
    /// Cancel an execution
    /// </summary>
    Task<bool> CancelExecutionAsync(Guid executionId, string? reason = null);
    
    /// <summary>
    /// Update execution status
    /// </summary>
    Task<bool> UpdateExecutionStatusAsync(Guid executionId, string status, string? errorMessage = null);
    
    /// <summary>
    /// Update execution progress
    /// </summary>
    Task<bool> UpdateExecutionProgressAsync(Guid executionId, int currentStage, decimal progressPercentage);
    
    /// <summary>
    /// Mark execution as completed
    /// </summary>
    Task<bool> CompleteExecutionAsync(Guid executionId, long? rowsProcessed = null, long? rowsAffected = null);
    
    /// <summary>
    /// Mark execution as failed
    /// </summary>
    Task<bool> FailExecutionAsync(Guid executionId, string errorMessage, string? stackTrace = null);
    
    /// <summary>
    /// Get dashboard metrics
    /// </summary>
    Task<PipelineMetricsDto> GetDashboardMetricsAsync();
}