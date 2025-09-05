using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using API.Services;
using Contracts.DTOs;
using API.Authorization;
using DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Pipeline Execution Management")]
[RequireRole(Roles.Analyst)]
public class PipelineExecutionController : ControllerBase
{
    private readonly IPipelineOrchestrationService _orchestrationService;
    private readonly IPipelineExecutionService _executionService;
    private readonly ILogger<PipelineExecutionController> _logger;

    public PipelineExecutionController(
        IPipelineOrchestrationService orchestrationService,
        IPipelineExecutionService executionService,
        ILogger<PipelineExecutionController> logger)
    {
        _orchestrationService = orchestrationService;
        _executionService = executionService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new pipeline execution for a product
    /// </summary>
    /// <param name="request">Pipeline execution start request</param>
    /// <returns>Created pipeline execution details</returns>
    [HttpPost("start")]
    [SwaggerOperation(Summary = "Start Pipeline Execution", Description = "Starts a new pipeline execution for the specified product")]
    [SwaggerResponse(200, "Pipeline execution started successfully", typeof(PipelineExecutionStartResponse))]
    [SwaggerResponse(400, "Invalid request parameters")]
    [SwaggerResponse(404, "Product not found or has no enabled stages")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<ActionResult<PipelineExecutionStartResponse>> StartPipelineExecution([FromBody] PipelineExecutionStartRequest request)
    {
        try
        {
            _logger.LogInformation("Starting pipeline execution for product {ProductId} initiated by {InitiatedBy}", 
                request.ProductId, request.InitiatedBy);

            var execution = await _executionService.StartExecutionAsync(
                request.ProductId, 
                request.InitiatedBy, 
                request.IsDryRun);

            // Check for existing pending jobs for this product
            try
            {
                var jobRepo = HttpContext.RequestServices.GetRequiredService<IObfuscationJobRepository>();
                var productRepo = HttpContext.RequestServices.GetRequiredService<IProductRepository>();
                
                // Check if there's already a pending or running job for this product
                var existingJobs = await jobRepo.GetByProductIdAsync(request.ProductId);
                var pendingOrRunningJob = existingJobs.FirstOrDefault(j => 
                    j.Status == Contracts.Models.JobStatus.Pending || 
                    j.Status == Contracts.Models.JobStatus.Running);
                
                if (pendingOrRunningJob != null)
                {
                    _logger.LogWarning("Cannot create new job - existing {Status} job {JobId} found for product {ProductId}", 
                        pendingOrRunningJob.Status, pendingOrRunningJob.Id, request.ProductId);
                    
                    // Cancel the pipeline execution that was just created
                    await _orchestrationService.CancelPipelineAsync(execution.Id, 
                        $"Existing {pendingOrRunningJob.Status.ToString().ToLower()} job already in queue");
                    
                    return BadRequest(new 
                    { 
                        error = $"Cannot create new job. There is already a {pendingOrRunningJob.Status.ToString().ToLower()} job in the queue for this product.",
                        existingJobId = pendingOrRunningJob.Id,
                        existingJobName = pendingOrRunningJob.JobName,
                        existingJobStatus = pendingOrRunningJob.Status.ToString()
                    });
                }
                
                var product = await productRepo.GetByIdAsync(request.ProductId);
                if (product != null)
                {
                    var job = new Contracts.Models.ObfuscationJob
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        JobName = $"Pipeline-{product.Name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                        Description = $"Obfuscation job for pipeline {execution.Id}",
                        Status = Contracts.Models.JobStatus.Pending,
                        Priority = Contracts.Models.JobPriority.Normal,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = request.InitiatedBy ?? "System",
                        Configuration = new Contracts.Models.JobConfiguration
                        {
                            DryRun = request.IsDryRun,
                            BatchSize = product.BatchSize,
                            SqlBatchSize = product.SqlBatchSize,
                            ParallelThreads = product.ParallelThreads,
                            CommandTimeoutSeconds = product.CommandTimeoutSeconds,
                            GenerateReport = true,
                            ValidateResults = true,
                            SpecificTables = new List<string>(),
                            ExcludedTables = new List<string>()
                        },
                        MappingVersion = "1.0",
                        MappingSasUrl = "", // Will be populated when mapping is uploaded to blob storage
                        MaxRetries = 3,
                        RetryCount = 0
                    };
                    
                    await jobRepo.CreateAsync(job);
                    _logger.LogInformation("Created job {JobId} for pipeline {PipelineId}", job.Id, execution.Id);
                }
            }
            catch (Exception jobEx)
            {
                _logger.LogError(jobEx, "Failed to create job for pipeline {PipelineId}", execution.Id);
                // Continue anyway - pipeline is created
            }

            var response = new PipelineExecutionStartResponse
            {
                PipelineExecutionId = execution.Id,
                Status = execution.Status,
                QueuedAt = execution.QueuedAt,
                TotalStages = execution.TotalStages,
                Message = "Pipeline execution started successfully"
            };

            _logger.LogInformation("Pipeline execution {ExecutionId} started successfully", 
                execution.Id);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument starting pipeline for product {ProductId}", request.ProductId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start pipeline execution for product {ProductId}", request.ProductId);
            return StatusCode(500, new { error = "Internal server error starting pipeline execution" });
        }
    }

    /// <summary>
    /// Get pipeline execution details by ID
    /// </summary>
    /// <param name="executionId">Pipeline execution ID</param>
    /// <returns>Pipeline execution details</returns>
    [HttpGet("{executionId}")]
    [SwaggerOperation(Summary = "Get Pipeline Execution", Description = "Retrieves detailed information about a specific pipeline execution")]
    [SwaggerResponse(200, "Pipeline execution details", typeof(PipelineExecutionDto))]
    [SwaggerResponse(404, "Pipeline execution not found")]
    public async Task<ActionResult<PipelineExecutionDto>> GetPipelineExecution(Guid executionId)
    {
        var execution = await _orchestrationService.GetPipelineExecutionAsync(executionId);
        
        if (execution == null)
        {
            return NotFound(new { error = $"Pipeline execution {executionId} not found" });
        }

        return Ok(execution);
    }

    /// <summary>
    /// Get all pipeline executions for a product
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="pageSize">Number of executions per page (default: 50, max: 100)</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <returns>List of pipeline executions</returns>
    [HttpGet("product/{productId}")]
    [SwaggerOperation(Summary = "Get Pipeline Executions for Product", Description = "Retrieves paginated list of pipeline executions for a specific product")]
    [SwaggerResponse(200, "Pipeline executions list", typeof(PipelineExecutionListResponse))]
    [SwaggerResponse(400, "Invalid pagination parameters")]
    public async Task<ActionResult<PipelineExecutionListResponse>> GetPipelineExecutions(
        Guid productId, 
        [FromQuery] int pageSize = 50, 
        [FromQuery] int page = 1)
    {
        if (pageSize <= 0 || pageSize > 100)
        {
            return BadRequest(new { error = "Page size must be between 1 and 100" });
        }

        if (page <= 0)
        {
            return BadRequest(new { error = "Page number must be greater than 0" });
        }

        var executions = await _orchestrationService.GetPipelineExecutionsAsync(productId, pageSize, page);
        
        var response = new PipelineExecutionListResponse
        {
            ProductId = productId,
            Executions = executions.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = executions.Count()
        };

        return Ok(response);
    }

    /// <summary>
    /// Cancel a running or queued pipeline execution
    /// </summary>
    /// <param name="executionId">Pipeline execution ID</param>
    /// <param name="request">Cancellation request with reason</param>
    /// <returns>Cancellation result</returns>
    [HttpPost("{executionId}/cancel")]
    [SwaggerOperation(Summary = "Cancel Pipeline Execution", Description = "Cancels a running or queued pipeline execution")]
    [SwaggerResponse(200, "Pipeline execution cancelled successfully")]
    [SwaggerResponse(400, "Pipeline execution cannot be cancelled")]
    [SwaggerResponse(404, "Pipeline execution not found")]
    public async Task<ActionResult> CancelPipelineExecution(Guid executionId, [FromBody] PipelineCancellationRequest request)
    {
        var success = await _orchestrationService.CancelPipelineAsync(executionId, request.Reason);
        
        if (!success)
        {
            return BadRequest(new { error = "Pipeline execution cannot be cancelled or was not found" });
        }

        _logger.LogInformation("Pipeline execution {ExecutionId} cancelled: {Reason}", executionId, request.Reason);
        return Ok(new { message = "Pipeline execution cancelled successfully" });
    }

    /// <summary>
    /// Pause a running pipeline execution
    /// </summary>
    /// <param name="executionId">Pipeline execution ID</param>
    /// <returns>Pause result</returns>
    [HttpPost("{executionId}/pause")]
    [SwaggerOperation(Summary = "Pause Pipeline Execution", Description = "Pauses a running pipeline execution")]
    [SwaggerResponse(200, "Pipeline execution paused successfully")]
    [SwaggerResponse(400, "Pipeline execution cannot be paused")]
    [SwaggerResponse(404, "Pipeline execution not found")]
    public async Task<ActionResult> PausePipelineExecution(Guid executionId)
    {
        var success = await _orchestrationService.PausePipelineAsync(executionId);
        
        if (!success)
        {
            return BadRequest(new { error = "Pipeline execution cannot be paused or was not found" });
        }

        _logger.LogInformation("Pipeline execution {ExecutionId} paused", executionId);
        return Ok(new { message = "Pipeline execution paused successfully" });
    }

    /// <summary>
    /// Resume a paused pipeline execution
    /// </summary>
    /// <param name="executionId">Pipeline execution ID</param>
    /// <returns>Resume result</returns>
    [HttpPost("{executionId}/resume")]
    [SwaggerOperation(Summary = "Resume Pipeline Execution", Description = "Resumes a paused pipeline execution")]
    [SwaggerResponse(200, "Pipeline execution resumed successfully")]
    [SwaggerResponse(400, "Pipeline execution cannot be resumed")]
    [SwaggerResponse(404, "Pipeline execution not found")]
    public async Task<ActionResult> ResumePipelineExecution(Guid executionId)
    {
        var success = await _orchestrationService.ResumePipelineAsync(executionId);
        
        if (!success)
        {
            return BadRequest(new { error = "Pipeline execution cannot be resumed or was not found" });
        }

        _logger.LogInformation("Pipeline execution {ExecutionId} resumed", executionId);
        return Ok(new { message = "Pipeline execution resumed successfully" });
    }

    /// <summary>
    /// Retry a failed stage execution
    /// </summary>
    /// <param name="stageExecutionId">Stage execution ID</param>
    /// <returns>Retry result</returns>
    [HttpPost("stage/{stageExecutionId}/retry")]
    [SwaggerOperation(Summary = "Retry Stage Execution", Description = "Manually retry a failed stage execution")]
    [SwaggerResponse(200, "Stage execution retry scheduled successfully")]
    [SwaggerResponse(400, "Stage execution cannot be retried")]
    [SwaggerResponse(404, "Stage execution not found")]
    public async Task<ActionResult> RetryStageExecution(Guid stageExecutionId)
    {
        var stageExecution = await _orchestrationService.GetStageExecutionAsync(stageExecutionId);
        
        if (stageExecution == null)
        {
            return NotFound(new { error = $"Stage execution {stageExecutionId} not found" });
        }

        if (stageExecution.Status != "Failed" || stageExecution.RetryAttempts >= stageExecution.MaxRetries)
        {
            return BadRequest(new { error = "Stage execution cannot be retried" });
        }

        // Retry will be handled by distributed agents
        _logger.LogInformation("Manual retry requested for stage execution {StageExecutionId}", 
            stageExecutionId);
        
        return Ok(new { message = "Stage execution retry scheduled for agent processing" });
    }
}

// DTOs for API requests and responses
public class PipelineExecutionStartRequest
{
    public Guid ProductId { get; set; }
    public string InitiatedBy { get; set; } = "System";
    public bool IsDryRun { get; set; } = false;
    public Dictionary<string, object>? Parameters { get; set; }
}

public class PipelineExecutionStartResponse
{
    public Guid PipelineExecutionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; }
    public int TotalStages { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PipelineExecutionListResponse
{
    public Guid ProductId { get; set; }
    public List<PipelineExecutionDto> Executions { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class PipelineCancellationRequest
{
    public string Reason { get; set; } = "Cancelled by user";
}