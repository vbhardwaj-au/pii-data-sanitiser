using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using API.Authorization;
using API.Services;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Result Reports and Analytics")]
[RequireRole(Roles.Admin, Roles.ProductAdmin, Roles.Analyst)]
public class ReportsController : ControllerBase
{
    private readonly IResultAggregationService _resultAggregationService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IResultAggregationService resultAggregationService,
        ILogger<ReportsController> logger)
    {
        _resultAggregationService = resultAggregationService;
        _logger = logger;
    }

    /// <summary>
    /// Get aggregated job results for a specific product
    /// </summary>
    [HttpGet("products/{productId}/aggregate")]
    [SwaggerOperation(Summary = "Get Product Job Aggregates", Description = "Get aggregated job results and statistics for a specific product")]
    [SwaggerResponse(200, "Product aggregates retrieved successfully", typeof(JobAggregateResults))]
    [SwaggerResponse(404, "Product not found")]
    public async Task<ActionResult<JobAggregateResults>> GetProductAggregates(
        Guid productId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var aggregates = await _resultAggregationService.GetJobAggregateResultsAsync(productId, startDate, endDate);
            return Ok(aggregates);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Product {ProductId} not found", productId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get product aggregates for {ProductId}", productId);
            return StatusCode(500, new { error = "Failed to get product aggregates" });
        }
    }

    /// <summary>
    /// Get system-wide aggregated results
    /// </summary>
    [HttpGet("system/aggregate")]
    [SwaggerOperation(Summary = "Get System Aggregates", Description = "Get system-wide aggregated job results and statistics")]
    [SwaggerResponse(200, "System aggregates retrieved successfully", typeof(SystemAggregateResults))]
    public async Task<ActionResult<SystemAggregateResults>> GetSystemAggregates(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var aggregates = await _resultAggregationService.GetSystemAggregateResultsAsync(startDate, endDate);
            return Ok(aggregates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system aggregates");
            return StatusCode(500, new { error = "Failed to get system aggregates" });
        }
    }

    /// <summary>
    /// Get detailed report for a specific job
    /// </summary>
    [HttpGet("jobs/{jobId}/detailed")]
    [SwaggerOperation(Summary = "Get Detailed Job Report", Description = "Get comprehensive detailed report for a specific job")]
    [SwaggerResponse(200, "Detailed job report retrieved successfully", typeof(DetailedJobReport))]
    [SwaggerResponse(404, "Job not found")]
    public async Task<ActionResult<DetailedJobReport>> GetDetailedJobReport(Guid jobId)
    {
        try
        {
            var report = await _resultAggregationService.GenerateDetailedJobReportAsync(jobId);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Job {JobId} not found", jobId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get detailed job report for {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to get detailed job report" });
        }
    }

    /// <summary>
    /// Generate comprehensive system report
    /// </summary>
    [HttpGet("system/comprehensive")]
    [SwaggerOperation(Summary = "Generate Comprehensive System Report", Description = "Generate a comprehensive system report with all metrics, trends, and recommendations")]
    [SwaggerResponse(200, "Comprehensive report generated successfully", typeof(ComprehensiveSystemReport))]
    public async Task<ActionResult<ComprehensiveSystemReport>> GetComprehensiveSystemReport()
    {
        try
        {
            var report = await _resultAggregationService.GenerateComprehensiveSystemReportAsync();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate comprehensive system report");
            return StatusCode(500, new { error = "Failed to generate comprehensive system report" });
        }
    }

    /// <summary>
    /// Get performance trends over time
    /// </summary>
    [HttpGet("trends/performance")]
    [SwaggerOperation(Summary = "Get Performance Trends", Description = "Get performance trends and analytics over specified time period")]
    [SwaggerResponse(200, "Performance trends retrieved successfully")]
    public async Task<ActionResult> GetPerformanceTrends(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? productId = null)
    {
        try
        {
            if (productId.HasValue)
            {
                // Get product-specific trends
                var productAggregates = await _resultAggregationService.GetJobAggregateResultsAsync(productId.Value, startDate, endDate);
                return Ok(new
                {
                    ProductId = productId.Value,
                    ProductName = productAggregates.ProductName,
                    Trends = productAggregates.DailyTrends,
                    Summary = new
                    {
                        productAggregates.TotalJobs,
                        productAggregates.SuccessRate,
                        productAggregates.AverageDuration,
                        productAggregates.AverageRowsPerSecond,
                        productAggregates.TotalRowsProcessed
                    }
                });
            }
            else
            {
                // Get system-wide trends
                var systemAggregates = await _resultAggregationService.GetSystemAggregateResultsAsync(startDate, endDate);
                return Ok(new
                {
                    SystemWide = true,
                    Summary = new
                    {
                        systemAggregates.TotalJobs,
                        systemAggregates.SuccessRate,
                        systemAggregates.AverageDuration,
                        systemAggregates.AverageRowsPerSecond,
                        systemAggregates.TotalRowsProcessed,
                        systemAggregates.CapacityUtilization
                    },
                    ProductBreakdown = systemAggregates.ProductPerformance
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get performance trends");
            return StatusCode(500, new { error = "Failed to get performance trends" });
        }
    }

    /// <summary>
    /// Get agent performance comparison
    /// </summary>
    [HttpGet("agents/performance")]
    [SwaggerOperation(Summary = "Get Agent Performance Comparison", Description = "Compare performance metrics across all agents")]
    [SwaggerResponse(200, "Agent performance comparison retrieved successfully")]
    public async Task<ActionResult> GetAgentPerformanceComparison(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var systemAggregates = await _resultAggregationService.GetSystemAggregateResultsAsync(startDate, endDate);
            
            // Get agent performance from all products
            var allProductReports = new List<JobAggregateResults>();
            foreach (var product in systemAggregates.ProductPerformance)
            {
                try
                {
                    var productReport = await _resultAggregationService.GetJobAggregateResultsAsync(product.ProductId, startDate, endDate);
                    allProductReports.Add(productReport);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get product report for {ProductId}", product.ProductId);
                }
            }

            // Aggregate agent performance across all products
            var agentPerformance = allProductReports
                .SelectMany(r => r.AgentPerformance)
                .GroupBy(a => a.AgentId)
                .Select(g => new
                {
                    AgentId = g.Key,
                    AgentName = g.First().AgentName,
                    TotalJobsCompleted = g.Sum(a => a.JobsCompleted),
                    TotalJobsFailed = g.Sum(a => a.JobsFailed),
                    TotalRowsProcessed = g.Sum(a => a.TotalRowsProcessed),
                    AverageDuration = g.Average(a => a.AverageDuration),
                    AverageRowsPerSecond = g.Average(a => a.AverageRowsPerSecond),
                    SuccessRate = g.Sum(a => a.JobsCompleted + a.JobsFailed) > 0 ?
                        (g.Sum(a => a.JobsCompleted) * 100.0 / g.Sum(a => a.JobsCompleted + a.JobsFailed)) : 0
                })
                .OrderByDescending(a => a.TotalJobsCompleted)
                .ToList();

            return Ok(new
            {
                TimeRange = new { StartDate = startDate, EndDate = endDate },
                AgentPerformance = agentPerformance,
                Summary = new
                {
                    TotalAgents = agentPerformance.Count,
                    TotalJobsProcessed = agentPerformance.Sum(a => a.TotalJobsCompleted + a.TotalJobsFailed),
                    AverageSuccessRate = agentPerformance.Any() ? agentPerformance.Average(a => a.SuccessRate) : 0,
                    TopPerformer = agentPerformance.FirstOrDefault()?.AgentName ?? "None"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent performance comparison");
            return StatusCode(500, new { error = "Failed to get agent performance comparison" });
        }
    }

    /// <summary>
    /// Export system report to different formats
    /// </summary>
    [HttpGet("export")]
    [SwaggerOperation(Summary = "Export System Report", Description = "Export comprehensive system report in various formats")]
    [SwaggerResponse(200, "Report exported successfully")]
    [SwaggerResponse(400, "Invalid export format")]
    public async Task<ActionResult> ExportSystemReport(
        [FromQuery] string format = "json",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var report = await _resultAggregationService.GenerateComprehensiveSystemReportAsync();
            
            return format.ToLowerInvariant() switch
            {
                "json" => Ok(report),
                "csv" => File(GenerateCsvReport(report), "text/csv", $"system-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv"),
                "xlsx" => StatusCode(501, new { error = "Excel export not yet implemented" }),
                "pdf" => StatusCode(501, new { error = "PDF export not yet implemented" }),
                _ => BadRequest(new { error = "Invalid format. Supported formats: json, csv" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export system report in format {Format}", format);
            return StatusCode(500, new { error = "Failed to export system report" });
        }
    }

    private byte[] GenerateCsvReport(ComprehensiveSystemReport report)
    {
        var csv = new System.Text.StringBuilder();
        
        // System summary
        csv.AppendLine("System Summary");
        csv.AppendLine($"Generated At,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"Report Period,{report.ReportPeriod}");
        csv.AppendLine($"Total Jobs,{report.SystemAggregates.TotalJobs}");
        csv.AppendLine($"Completed Jobs,{report.SystemAggregates.CompletedJobs}");
        csv.AppendLine($"Failed Jobs,{report.SystemAggregates.FailedJobs}");
        csv.AppendLine($"Success Rate,{report.SystemAggregates.SuccessRate:F2}%");
        csv.AppendLine($"Total Rows Processed,{report.SystemAggregates.TotalRowsProcessed}");
        csv.AppendLine($"Average Duration (minutes),{report.SystemAggregates.AverageDuration:F2}");
        csv.AppendLine($"Average Rows/Second,{report.SystemAggregates.AverageRowsPerSecond:F2}");
        csv.AppendLine($"Capacity Utilization,{report.SystemAggregates.CapacityUtilization:F2}%");
        csv.AppendLine();
        
        // Agent health
        csv.AppendLine("Agent Health Summary");
        csv.AppendLine("Agent Name,Status,Location,Online,Completed Jobs,Failed Jobs,CPU Usage,Memory Usage,Active Jobs,Max Jobs");
        foreach (var agent in report.AgentHealthSummary)
        {
            csv.AppendLine($"{agent.AgentName},{agent.Status},{agent.Location},{agent.IsOnline}," +
                          $"{agent.CompletedJobs},{agent.FailedJobs},{agent.CurrentCpuUsage:F1},{agent.CurrentMemoryUsage:F1}," +
                          $"{agent.ActiveJobs},{agent.MaxConcurrentJobs}");
        }
        csv.AppendLine();
        
        // Recommendations
        csv.AppendLine("System Recommendations");
        csv.AppendLine("Type,Priority,Title,Description,Action");
        foreach (var rec in report.Recommendations)
        {
            csv.AppendLine($"{rec.Type},{rec.Priority},\"{rec.Title}\",\"{rec.Description}\",\"{rec.Action}\"");
        }
        
        return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    }
}