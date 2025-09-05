using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using DAL.Repositories;
using API.Services;
using Contracts.Models;
using API.Authorization;
using Contracts.DTOs;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Pipeline Monitoring")]
[RequireRole(Roles.Viewer)]
public class PipelineMonitoringController : ControllerBase
{
    private readonly IPipelineOrchestrationService _orchestrationService;
    private readonly IPipelineExecutionService _executionService;
    private readonly IProductRepository _productRepository;
    private readonly IScriptExecutionLogRepository _logRepository;
    private readonly ILogger<PipelineMonitoringController> _logger;

    public PipelineMonitoringController(
        IPipelineOrchestrationService orchestrationService,
        IPipelineExecutionService executionService,
        IProductRepository productRepository,
        IScriptExecutionLogRepository logRepository,
        ILogger<PipelineMonitoringController> logger)
    {
        _orchestrationService = orchestrationService;
        _executionService = executionService;
        _productRepository = productRepository;
        _logRepository = logRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard overview with key metrics and activities
    /// </summary>
    /// <returns>Dashboard overview with metrics, active executions, and recent activities</returns>
    [HttpGet("dashboard")]
    [SwaggerOperation(Summary = "Get Dashboard Overview", Description = "Gets dashboard overview with key metrics, active executions, and recent activities")]
    [SwaggerResponse(200, "Dashboard overview retrieved successfully", typeof(DashboardOverviewDto))]
    public async Task<ActionResult<DashboardOverviewDto>> GetDashboardOverview()
    {
        try
        {
            var metrics = await _executionService.GetDashboardMetricsAsync();
            var activeExecutions = await _executionService.GetActiveExecutionsAsync();
            
            var dashboard = new DashboardOverviewDto
            {
                Metrics = metrics,
                ActiveExecutions = activeExecutions.Select(MapToPipelineExecutionDto).ToList(),
                RecentActivities = await GetRecentActivitiesAsync(),
                SystemHealth = new SystemHealthDto
                {
                    DatabaseConnected = await CheckDatabaseConnectionAsync(),
                    JobServerHealthy = CheckJobServerHealth(),
                    PendingJobs = 0, // TODO: Get from Hangfire
                    FailedJobs = 0 // TODO: Get from Hangfire
                }
            };

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard overview");
            return StatusCode(500, $"Error retrieving dashboard: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current pipeline execution status for all products
    /// </summary>
    /// <returns>Current execution status for all products</returns>
    [HttpGet("executions/current")]
    [SwaggerOperation(Summary = "Get Current Executions", Description = "Gets current pipeline execution status for all products")]
    [SwaggerResponse(200, "Current executions retrieved successfully", typeof(List<PipelineExecutionDto>))]
    public async Task<ActionResult<List<PipelineExecutionDto>>> GetCurrentExecutions()
    {
        try
        {
            var activeExecutions = await _executionService.GetActiveExecutionsAsync();
            var executionDtos = activeExecutions.Select(MapToPipelineExecutionDto).ToList();
            return Ok(executionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current executions");
            return StatusCode(500, $"Error retrieving executions: {ex.Message}");
        }
    }

    /// <summary>
    /// Get pipeline execution history across all products
    /// </summary>
    /// <param name="status">Filter by status (optional)</param>
    /// <param name="startDate">Filter by start date (optional)</param>
    /// <param name="endDate">Filter by end date (optional)</param>
    /// <param name="product">Filter by product name (optional)</param>
    /// <returns>Pipeline execution history</returns>
    [HttpGet("executions/history")]
    [SwaggerOperation(Summary = "Get All Execution History", Description = "Gets pipeline execution history across all products with optional filters")]
    [SwaggerResponse(200, "Execution history retrieved successfully", typeof(List<PipelineExecutionDto>))]
    public async Task<ActionResult<List<PipelineExecutionDto>>> GetAllExecutionHistory(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? product = null)
    {
        try
        {
            var executions = await _executionService.GetExecutionsAsync(status, startDate, endDate, product);
            var executionDtos = executions.Select(MapToPipelineExecutionDto).ToList();
            return Ok(executionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution history");
            return StatusCode(500, $"Error retrieving execution history: {ex.Message}");
        }
    }

    /// <summary>
    /// Get pipeline execution history for a product
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="limit">Maximum number of records to return (default: 50)</param>
    /// <returns>Pipeline execution history</returns>
    [HttpGet("executions/product/{productId}/history")]
    [SwaggerOperation(Summary = "Get Execution History", Description = "Gets pipeline execution history for a specific product")]
    [SwaggerResponse(200, "Execution history retrieved successfully", typeof(List<PipelineExecutionDto>))]
    [SwaggerResponse(404, "Product not found")]
    public async Task<ActionResult<List<PipelineExecutionDto>>> GetExecutionHistory(Guid productId, int limit = 50)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
        {
            return NotFound($"Product {productId} not found");
        }

        try
        {
            // Mock data - replace with actual service call
            var executions = new List<PipelineExecutionDto>();
            return Ok(executions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution history for product {ProductId}", productId);
            return StatusCode(500, $"Error retrieving execution history: {ex.Message}");
        }
    }

    /// <summary>
    /// Get detailed execution information including stage progress
    /// </summary>
    /// <param name="executionId">Pipeline execution ID</param>
    /// <returns>Detailed execution information with stage progress</returns>
    [HttpGet("executions/{executionId}/details")]
    [SwaggerOperation(Summary = "Get Execution Details", Description = "Gets detailed execution information including stage progress and logs")]
    [SwaggerResponse(200, "Execution details retrieved successfully", typeof(PipelineProgressDto))]
    [SwaggerResponse(404, "Execution not found")]
    public async Task<ActionResult<PipelineProgressDto>> GetExecutionDetails(Guid executionId)
    {
        try
        {
            // Mock data - replace with actual service call
            var progress = new PipelineProgressDto(); // Return empty progress for now
            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution details for {ExecutionId}", executionId);
            return StatusCode(500, $"Error retrieving execution details: {ex.Message}");
        }
    }

    /// <summary>
    /// Get system health status and metrics
    /// </summary>
    /// <returns>System health indicators and performance metrics</returns>
    [HttpGet("health")]
    [SwaggerOperation(Summary = "Get System Health", Description = "Gets system health indicators and performance metrics")]
    [SwaggerResponse(200, "System health retrieved successfully", typeof(SystemHealthDto))]
    public async Task<ActionResult<SystemHealthDto>> GetSystemHealth()
    {
        try
        {
            var health = new SystemHealthDto
            {
                DatabaseConnected = await CheckDatabaseConnectionAsync(),
                JobServerHealthy = CheckJobServerHealth(),
                PendingJobs = 0, // TODO: Get from Hangfire monitoring API
                FailedJobs = 0 // TODO: Get from Hangfire monitoring API
            };

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking system health");
            return StatusCode(500, $"Error checking system health: {ex.Message}");
        }
    }

    private async Task<List<PipelineActivityDto>> GetRecentActivitiesAsync()
    {
        // TODO: Implement actual activity log retrieval
        // For now, return mock data
        return new List<PipelineActivityDto>
        {
            new PipelineActivityDto
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                ProductName = "AdventureWorks",
                StageName = "Data Obfuscation",
                ActivityType = "Completed",
                Description = "Successfully completed data obfuscation stage",
                Severity = "Info"
            },
            new PipelineActivityDto
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-15),
                ProductName = "Northwind",
                StageName = "Validation",
                ActivityType = "Started",
                Description = "Started data validation stage",
                Severity = "Info"
            }
        };
    }

    private async Task<bool> CheckDatabaseConnectionAsync()
    {
        try
        {
            // Try to get a product to test database connection
            var products = await _productRepository.GetAllAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool CheckJobServerHealth()
    {
        // TODO: Implement actual Hangfire server health check
        return true;
    }

    private static PipelineExecutionDto MapToPipelineExecutionDto(PipelineExecution execution)
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
            IsDryRun = execution.IsDryRun
        };
    }
}