using Microsoft.AspNetCore.Mvc;
using API.Services;
using Contracts.DTOs;
using Contracts.Models;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers;

[ApiController]
[Route("api/pipeline/stage")]
[Produces("application/json")]
public class StageController : ControllerBase
{
    private readonly IPipelineOrchestrationService _orchestrationService;
    private readonly IStageExecutionService _stageExecutionService;
    private readonly IPipelineStateManagerService _stateManagerService;
    private readonly ILogger<StageController> _logger;

    public StageController(
        IPipelineOrchestrationService orchestrationService,
        IStageExecutionService stageExecutionService,
        IPipelineStateManagerService stateManagerService,
        ILogger<StageController> logger)
    {
        _orchestrationService = orchestrationService;
        _stageExecutionService = stageExecutionService;
        _stateManagerService = stateManagerService;
        _logger = logger;
    }

    /// <summary>
    /// Start execution of a specific stage
    /// </summary>
    /// <param name="id">Stage execution ID</param>
    /// <returns>Started stage execution details</returns>
    [HttpPost("start/{id:guid}")]
    [ProducesResponseType(typeof(StageExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StageExecutionDto>> StartStage(Guid id)
    {
        try
        {
            _logger.LogInformation("Starting stage execution {StageExecutionId}", id);

            // Validate prerequisites first
            var canStart = await _stageExecutionService.ValidateStagePrerequisitesAsync(id);
            if (!canStart)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Title = "Prerequisites Not Met", 
                    Detail = "Stage cannot start because prerequisite stages are not completed",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Get the stage execution to find pipeline and stage info
            var pipeline = await FindPipelineByStageAsync(id);
            if (pipeline == null)
            {
                return NotFound(new ProblemDetails 
                { 
                    Title = "Stage Not Found", 
                    Detail = $"Stage execution with ID {id} was not found"
                });
            }

            var stage = pipeline.StageExecutions.FirstOrDefault(se => se.Id == id);
            if (stage == null)
            {
                return NotFound(new ProblemDetails 
                { 
                    Title = "Stage Not Found", 
                    Detail = $"Stage execution with ID {id} was not found"
                });
            }

            var result = await _orchestrationService.StartStageAsync(pipeline.Id, stage.PipelineStageId, stage.ExecutionOrder);
            
            _logger.LogInformation("Stage execution {StageExecutionId} started successfully", id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot start stage execution {StageExecutionId}", id);
            return BadRequest(new ProblemDetails 
            { 
                Title = "Cannot Start Stage", 
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start stage execution {StageExecutionId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Update stage execution status
    /// </summary>
    /// <param name="id">Stage execution ID</param>
    /// <param name="request">Status update request</param>
    /// <returns>Updated stage execution</returns>
    [HttpPatch("status/{id:guid}")]
    [ProducesResponseType(typeof(StageExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StageExecutionDto>> UpdateStageStatus(Guid id, [FromBody] UpdateStageStatusRequest request)
    {
        try
        {
            _logger.LogInformation("Updating stage {StageExecutionId} status to {Status}", id, request.Status);

            var result = await _orchestrationService.UpdateStageStatusAsync(id, request.Status, request.ErrorMessage);
            
            _logger.LogInformation("Stage {StageExecutionId} status updated to {Status}", id, request.Status);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid status transition for stage {StageExecutionId}", id);
            return BadRequest(new ProblemDetails 
            { 
                Title = "Invalid Status Transition", 
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stage status for {StageExecutionId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Update stage execution progress
    /// </summary>
    /// <param name="id">Stage execution ID</param>
    /// <param name="request">Progress update request</param>
    /// <returns>Updated stage execution</returns>
    [HttpPatch("progress/{id:guid}")]
    [ProducesResponseType(typeof(StageExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StageExecutionDto>> UpdateStageProgress(Guid id, [FromBody] UpdateStageProgressRequest request)
    {
        try
        {
            _logger.LogInformation("Updating stage {StageExecutionId} progress to {Progress}%", id, request.ProgressPercentage);

            var result = await _orchestrationService.UpdateStageProgressAsync(id, request.ProgressPercentage, request.RowsProcessed);
            
            _logger.LogInformation("Stage {StageExecutionId} progress updated to {Progress}%", id, request.ProgressPercentage);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stage progress for {StageExecutionId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Execute a stage completely (for testing or manual execution)
    /// </summary>
    /// <param name="id">Stage execution ID</param>
    /// <returns>Completed stage execution details</returns>
    [HttpPost("execute/{id:guid}")]
    [ProducesResponseType(typeof(StageExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StageExecutionDto>> ExecuteStage(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing stage {StageExecutionId}", id);

            var result = await _stageExecutionService.ExecuteStageAsync(id, cancellationToken);
            
            _logger.LogInformation("Stage {StageExecutionId} execution completed with status {Status}", id, result.Status);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot execute stage {StageExecutionId}", id);
            return BadRequest(new ProblemDetails 
            { 
                Title = "Cannot Execute Stage", 
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute stage {StageExecutionId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get stage execution details
    /// </summary>
    /// <param name="id">Stage execution ID</param>
    /// <returns>Stage execution details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StageExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StageExecutionDto>> GetStageExecution(Guid id)
    {
        try
        {
            var pipeline = await FindPipelineByStageAsync(id);
            if (pipeline == null)
            {
                return NotFound(new ProblemDetails 
                { 
                    Title = "Stage Not Found", 
                    Detail = $"Stage execution with ID {id} was not found"
                });
            }

            var stage = pipeline.StageExecutions.FirstOrDefault(se => se.Id == id);
            if (stage == null)
            {
                return NotFound(new ProblemDetails 
                { 
                    Title = "Stage Not Found", 
                    Detail = $"Stage execution with ID {id} was not found"
                });
            }

            return Ok(stage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stage execution {StageExecutionId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Update stage metrics (memory, CPU usage, etc.)
    /// </summary>
    /// <param name="id">Stage execution ID</param>
    /// <param name="request">Metrics update request</param>
    /// <returns>Success result</returns>
    [HttpPatch("metrics/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateStageMetrics(Guid id, [FromBody] UpdateStageMetricsRequest request)
    {
        try
        {
            _logger.LogInformation("Updating metrics for stage {StageExecutionId}", id);

            await _stageExecutionService.UpdateStageMetricsAsync(
                id, 
                request.RowsProcessed, 
                request.RowsAffected, 
                request.MemoryUsageMB, 
                request.CpuUsagePercentage);
            
            return Ok(new { message = "Stage metrics updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stage metrics for {StageExecutionId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Check if stage can transition to a new status
    /// </summary>
    /// <param name="id">Stage execution ID</param>
    /// <param name="newStatus">New status to validate</param>
    /// <returns>Validation result</returns>
    [HttpGet("{id:guid}/can-transition/{newStatus}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CanTransitionStage(Guid id, string newStatus)
    {
        try
        {
            var canTransition = await _stateManagerService.CanTransitionStageAsync(id, newStatus);
            return Ok(canTransition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check stage transition for {StageExecutionId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<PipelineExecutionDto?> FindPipelineByStageAsync(Guid stageExecutionId)
    {
        try
        {
            // This is a simplified approach - in production, you might want a more efficient query
            // For now, we'll need to search through recent pipeline executions
            // A better approach would be to add a direct query method to the orchestration service
            
            // For this implementation, we'll return null and let the caller handle it
            // In a real implementation, you'd add a method to find pipeline by stage ID
            return null;
        }
        catch
        {
            return null;
        }
    }
}

// Request DTOs for Stage Controller
public class UpdateStageStatusRequest
{
    [Required]
    [API.Filters.ValidStageStatus]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ErrorMessage { get; set; }
}

public class UpdateStageProgressRequest
{
    [Required]
    [Range(0, 100, ErrorMessage = "Progress percentage must be between 0 and 100")]
    public decimal ProgressPercentage { get; set; }

    [Range(0, long.MaxValue, ErrorMessage = "Rows processed must be non-negative")]
    public long? RowsProcessed { get; set; }
}

public class UpdateStageMetricsRequest
{
    [Required]
    [Range(0, long.MaxValue, ErrorMessage = "Rows processed must be non-negative")]
    public long RowsProcessed { get; set; }

    [Required]
    [Range(0, long.MaxValue, ErrorMessage = "Rows affected must be non-negative")]
    public long RowsAffected { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Memory usage must be non-negative")]
    public decimal MemoryUsageMB { get; set; }

    [Required]
    [Range(0, 100, ErrorMessage = "CPU usage percentage must be between 0 and 100")]
    public decimal CpuUsagePercentage { get; set; }
}