using Microsoft.AspNetCore.Mvc;
using API.Services;
using Contracts.DTOs;
using Contracts.Models;
using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("Pipeline orchestration and execution management")]
public class PipelineController : ControllerBase
{
    private readonly IPipelineOrchestrationService _orchestrationService;
    private readonly IPipelineStateManagerService _stateManagerService;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        IPipelineOrchestrationService orchestrationService,
        IPipelineStateManagerService stateManagerService,
        ILogger<PipelineController> logger)
    {
        _orchestrationService = orchestrationService;
        _stateManagerService = stateManagerService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new pipeline execution for a product
    /// </summary>
    /// <param name="request">Pipeline start request</param>
    /// <returns>Created pipeline execution details</returns>
    [HttpPost("start")]
    [SwaggerOperation(
        Summary = "Start Pipeline Execution",
        Description = "Initiates a new pipeline execution for the specified product. The pipeline will be queued and processed asynchronously through multiple stages.",
        Tags = new[] { "Pipeline Execution" }
    )]
    [SwaggerResponse(201, "Pipeline execution started successfully", typeof(PipelineExecutionDto))]
    [SwaggerResponse(400, "Invalid request parameters", typeof(ProblemDetails))]
    [SwaggerResponse(500, "Internal server error", typeof(ProblemDetails))]
    public async Task<ActionResult<PipelineExecutionDto>> StartPipeline([FromBody] StartPipelineRequest request)
    {
        try
        {
            _logger.LogInformation("Starting pipeline for product {ProductId}, initiated by {InitiatedBy}", 
                request.ProductId, request.InitiatedBy);

            var result = await _orchestrationService.StartPipelineAsync(
                request.ProductId, 
                request.InitiatedBy, 
                request.IsDryRun, 
                request.Parameters);

            _logger.LogInformation("Pipeline {PipelineId} started successfully for product {ProductId}", 
                result.Id, request.ProductId);

            return CreatedAtAction(nameof(GetPipelineExecution), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request to start pipeline for product {ProductId}", request.ProductId);
            return BadRequest(new ProblemDetails 
            { 
                Title = "Invalid Request", 
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot start pipeline for product {ProductId}", request.ProductId);
            return BadRequest(new ProblemDetails 
            { 
                Title = "Pipeline Start Failed", 
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start pipeline for product {ProductId}", request.ProductId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new ProblemDetails 
                { 
                    Title = "Internal Server Error", 
                    Detail = "An unexpected error occurred while starting the pipeline"
                });
        }
    }

    /// <summary>
    /// Get pipeline execution details by ID
    /// </summary>
    /// <param name="id">Pipeline execution ID</param>
    /// <returns>Pipeline execution details</returns>
    [HttpGet("execution/{id:guid}")]
    [ProducesResponseType(typeof(PipelineExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PipelineExecutionDto>> GetPipelineExecution(Guid id)
    {
        try
        {
            var result = await _orchestrationService.GetPipelineExecutionAsync(id);
            
            if (result == null)
            {
                return NotFound(new ProblemDetails 
                { 
                    Title = "Pipeline Not Found", 
                    Detail = $"Pipeline execution with ID {id} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipeline execution {PipelineId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get pipeline executions for a product with pagination
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="pageSize">Number of items per page (default: 50)</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <returns>List of pipeline executions</returns>
    [HttpGet("executions/{productId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PipelineExecutionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PipelineExecutionDto>>> GetPipelineExecutions(
        Guid productId, 
        [FromQuery] int pageSize = 50, 
        [FromQuery] int page = 1)
    {
        try
        {
            if (pageSize <= 0 || pageSize > 100)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Title = "Invalid Page Size", 
                    Detail = "Page size must be between 1 and 100",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (page <= 0)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Title = "Invalid Page Number", 
                    Detail = "Page number must be greater than 0",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var result = await _orchestrationService.GetPipelineExecutionsAsync(productId, pageSize, page);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipeline executions for product {ProductId}", productId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Cancel a running pipeline execution
    /// </summary>
    /// <param name="id">Pipeline execution ID</param>
    /// <param name="request">Cancellation request</param>
    /// <returns>Success result</returns>
    [HttpPost("cancel/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CancelPipeline(Guid id, [FromBody] CancelPipelineRequest request)
    {
        try
        {
            _logger.LogInformation("Cancelling pipeline {PipelineId}: {Reason}", id, request.CancellationReason);

            var success = await _orchestrationService.CancelPipelineAsync(id, request.CancellationReason);
            
            if (!success)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Title = "Cannot Cancel Pipeline", 
                    Detail = "Pipeline cannot be cancelled in its current state",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Pipeline {PipelineId} cancelled successfully", id);
            return Ok(new { message = "Pipeline cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel pipeline {PipelineId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Pause a running pipeline execution
    /// </summary>
    /// <param name="id">Pipeline execution ID</param>
    /// <returns>Success result</returns>
    [HttpPost("pause/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> PausePipeline(Guid id)
    {
        try
        {
            _logger.LogInformation("Pausing pipeline {PipelineId}", id);

            var success = await _orchestrationService.PausePipelineAsync(id);
            
            if (!success)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Title = "Cannot Pause Pipeline", 
                    Detail = "Pipeline cannot be paused in its current state",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Pipeline {PipelineId} paused successfully", id);
            return Ok(new { message = "Pipeline paused successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause pipeline {PipelineId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Resume a paused pipeline execution
    /// </summary>
    /// <param name="id">Pipeline execution ID</param>
    /// <returns>Success result</returns>
    [HttpPost("resume/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResumePipeline(Guid id)
    {
        try
        {
            _logger.LogInformation("Resuming pipeline {PipelineId}", id);

            var success = await _orchestrationService.ResumePipelineAsync(id);
            
            if (!success)
            {
                return BadRequest(new ProblemDetails 
                { 
                    Title = "Cannot Resume Pipeline", 
                    Detail = "Pipeline cannot be resumed in its current state",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Pipeline {PipelineId} resumed successfully", id);
            return Ok(new { message = "Pipeline resumed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume pipeline {PipelineId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Update pipeline execution status
    /// </summary>
    /// <param name="id">Pipeline execution ID</param>
    /// <param name="request">Status update request</param>
    /// <returns>Updated pipeline execution</returns>
    [HttpPatch("status/{id:guid}")]
    [ProducesResponseType(typeof(PipelineExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PipelineExecutionDto>> UpdatePipelineStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            _logger.LogInformation("Updating pipeline {PipelineId} status to {Status}", id, request.Status);

            var result = await _orchestrationService.UpdatePipelineStatusAsync(id, request.Status, request.ErrorMessage);
            
            _logger.LogInformation("Pipeline {PipelineId} status updated to {Status}", id, request.Status);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid status transition for pipeline {PipelineId}", id);
            return BadRequest(new ProblemDetails 
            { 
                Title = "Invalid Status Transition", 
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update pipeline status for {PipelineId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get next executable stages for a pipeline
    /// </summary>
    /// <param name="id">Pipeline execution ID</param>
    /// <returns>List of executable stages</returns>
    [HttpGet("execution/{id:guid}/next-stages")]
    [ProducesResponseType(typeof(IEnumerable<StageExecutionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StageExecutionDto>>> GetNextExecutableStages(Guid id)
    {
        try
        {
            var result = await _stateManagerService.GetNextExecutableStagesAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next executable stages for pipeline {PipelineId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get pipeline execution progress summary
    /// </summary>
    /// <param name="id">Pipeline execution ID</param>
    /// <returns>Progress information</returns>
    [HttpGet("execution/{id:guid}/progress")]
    [ProducesResponseType(typeof(PipelineProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PipelineProgressDto>> GetPipelineProgress(Guid id)
    {
        try
        {
            var pipeline = await _orchestrationService.GetPipelineExecutionAsync(id);
            
            if (pipeline == null)
            {
                return NotFound(new ProblemDetails 
                { 
                    Title = "Pipeline Not Found", 
                    Detail = $"Pipeline execution with ID {id} was not found"
                });
            }

            var progress = new PipelineProgressDto
            {
                Id = pipeline.Id,
                Status = pipeline.Status,
                ProgressPercentage = pipeline.ProgressPercentage,
                CurrentStage = pipeline.CurrentStage,
                TotalStages = pipeline.TotalStages,
                StagesCompleted = pipeline.StagesCompleted,
                StagesFailed = pipeline.StagesFailed,
                StagesSkipped = pipeline.StagesSkipped,
                QueuedAt = pipeline.QueuedAt,
                StartedAt = pipeline.StartedAt,
                CompletedAt = pipeline.CompletedAt,
                DurationMs = pipeline.DurationMs,
                TotalRowsProcessed = pipeline.TotalRowsProcessed,
                TotalRowsAffected = pipeline.TotalRowsAffected,
                ErrorMessage = pipeline.ErrorMessage,
                StageProgress = pipeline.StageExecutions.Select(se => new StageProgressDto
                {
                    Id = se.Id,
                    StageName = se.StageName,
                    ExecutionOrder = se.ExecutionOrder,
                    Status = se.Status,
                    ProgressPercentage = se.ProgressPercentage,
                    StartedAt = se.StartedAt,
                    CompletedAt = se.CompletedAt,
                    DurationMs = se.DurationMs,
                    TotalScripts = se.TotalScripts,
                    ScriptsCompleted = se.ScriptsCompleted,
                    ScriptsFailed = se.ScriptsFailed,
                    ErrorMessage = se.ErrorMessage
                }).ToList()
            };

            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipeline progress for {PipelineId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

// Request DTOs
public class StartPipelineRequest
{
    [Required]
    [API.Filters.RequiredGuid]
    public Guid ProductId { get; set; }

    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string InitiatedBy { get; set; } = string.Empty;

    public bool IsDryRun { get; set; } = false;

    public Dictionary<string, object>? Parameters { get; set; }
}

public class CancelPipelineRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string CancellationReason { get; set; } = string.Empty;
}

public class UpdateStatusRequest
{
    [Required]
    [API.Filters.ValidPipelineStatus]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ErrorMessage { get; set; }
}