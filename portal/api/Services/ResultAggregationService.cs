using Contracts.Models;
using DAL.Repositories;
using System.Text.Json;

namespace API.Services;

public class ResultAggregationService : IResultAggregationService
{
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<ResultAggregationService> _logger;

    public ResultAggregationService(
        IObfuscationJobRepository jobRepository,
        IObfuscationAgentRepository agentRepository,
        IProductRepository productRepository,
        IBlobStorageService blobStorageService,
        ILogger<ResultAggregationService> logger)
    {
        _jobRepository = jobRepository;
        _agentRepository = agentRepository;
        _productRepository = productRepository;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<JobAggregateResults> GetJobAggregateResultsAsync(Guid productId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            startDate ??= DateTime.UtcNow.AddDays(-30); // Default to last 30 days
            endDate ??= DateTime.UtcNow;

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
            {
                throw new ArgumentException("Product not found", nameof(productId));
            }

            var completedJobs = await _jobRepository.GetCompletedJobsAsync((endDate.Value - startDate.Value).Days);
            var failedJobs = await _jobRepository.GetFailedJobsAsync((endDate.Value - startDate.Value).Days);
            
            var productJobs = completedJobs.Concat(failedJobs)
                .Where(j => j.ProductId == productId && j.CreatedAt >= startDate && j.CreatedAt <= endDate)
                .ToList();

            var aggregateResults = new JobAggregateResults
            {
                ProductId = productId,
                ProductName = product.Name,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TotalJobs = productJobs.Count,
                CompletedJobs = productJobs.Count(j => j.Status == JobStatus.Completed),
                FailedJobs = productJobs.Count(j => j.Status == JobStatus.Failed),
                TotalRowsProcessed = productJobs.Sum(j => j.ProcessedRows),
                AverageDuration = productJobs.Where(j => j.Duration.HasValue).Select(j => j.Duration!.Value).DefaultIfEmpty().Average(d => d.TotalMinutes),
                AverageRowsPerSecond = productJobs.Where(j => j.RowsPerSecond.HasValue).Select(j => (double)j.RowsPerSecond!.Value).DefaultIfEmpty().Average(),
                GeneratedAt = DateTime.UtcNow
            };

            // Calculate success rate
            aggregateResults.SuccessRate = aggregateResults.TotalJobs > 0 ?
                (aggregateResults.CompletedJobs * 100.0 / aggregateResults.TotalJobs) : 0;

            // Calculate performance metrics
            aggregateResults.TotalProcessingTime = productJobs.Where(j => j.Duration.HasValue)
                .Sum(j => j.Duration!.Value.TotalMinutes);

            // Agent performance breakdown
            aggregateResults.AgentPerformance = productJobs
                .Where(j => j.AgentId.HasValue)
                .GroupBy(j => j.AgentId!.Value)
                .Select(g => new AgentPerformanceMetrics
                {
                    AgentId = g.Key,
                    AgentName = g.First().Agent?.Name ?? "Unknown",
                    JobsCompleted = g.Count(j => j.Status == JobStatus.Completed),
                    JobsFailed = g.Count(j => j.Status == JobStatus.Failed),
                    TotalRowsProcessed = g.Sum(j => j.ProcessedRows),
                    AverageDuration = g.Where(j => j.Duration.HasValue).Select(j => j.Duration!.Value.TotalMinutes).DefaultIfEmpty().Average(),
                    AverageRowsPerSecond = g.Where(j => j.RowsPerSecond.HasValue).Select(j => (double)j.RowsPerSecond!.Value).DefaultIfEmpty().Average()
                })
                .OrderByDescending(a => a.JobsCompleted)
                .ToList();

            // Time series data for trends
            aggregateResults.DailyTrends = productJobs
                .GroupBy(j => j.CreatedAt.Date)
                .Select(g => new DailyJobTrend
                {
                    Date = g.Key,
                    JobsCompleted = g.Count(j => j.Status == JobStatus.Completed),
                    JobsFailed = g.Count(j => j.Status == JobStatus.Failed),
                    TotalRowsProcessed = g.Sum(j => j.ProcessedRows),
                    AverageDuration = g.Where(j => j.Duration.HasValue).Select(j => j.Duration!.Value.TotalMinutes).DefaultIfEmpty().Average()
                })
                .OrderBy(t => t.Date)
                .ToList();

            return aggregateResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job aggregate results for product {ProductId}", productId);
            throw;
        }
    }

    public async Task<SystemAggregateResults> GetSystemAggregateResultsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            var completedJobs = await _jobRepository.GetCompletedJobsAsync((endDate.Value - startDate.Value).Days);
            var failedJobs = await _jobRepository.GetFailedJobsAsync((endDate.Value - startDate.Value).Days);
            var allJobs = completedJobs.Concat(failedJobs)
                .Where(j => j.CreatedAt >= startDate && j.CreatedAt <= endDate)
                .ToList();

            var onlineAgents = await _agentRepository.GetOnlineAgentsAsync();
            var allAgents = await _agentRepository.GetAllAsync();

            var systemResults = new SystemAggregateResults
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TotalJobs = allJobs.Count,
                CompletedJobs = allJobs.Count(j => j.Status == JobStatus.Completed),
                FailedJobs = allJobs.Count(j => j.Status == JobStatus.Failed),
                TotalRowsProcessed = allJobs.Sum(j => j.ProcessedRows),
                TotalProcessingTime = allJobs.Where(j => j.Duration.HasValue).Sum(j => j.Duration!.Value.TotalMinutes),
                AverageDuration = allJobs.Where(j => j.Duration.HasValue).Select(j => j.Duration!.Value.TotalMinutes).DefaultIfEmpty().Average(),
                AverageRowsPerSecond = allJobs.Where(j => j.RowsPerSecond.HasValue).Select(j => (double)j.RowsPerSecond!.Value).DefaultIfEmpty().Average(),
                TotalAgents = allAgents.Count(),
                OnlineAgents = onlineAgents.Count(),
                TotalCapacity = onlineAgents.Sum(a => a.MaxConcurrentJobs),
                UsedCapacity = onlineAgents.Sum(a => a.ActiveJobs),
                GeneratedAt = DateTime.UtcNow
            };

            systemResults.SuccessRate = systemResults.TotalJobs > 0 ?
                (systemResults.CompletedJobs * 100.0 / systemResults.TotalJobs) : 0;

            systemResults.CapacityUtilization = systemResults.TotalCapacity > 0 ?
                (systemResults.UsedCapacity * 100.0 / systemResults.TotalCapacity) : 0;

            // Product performance breakdown
            systemResults.ProductPerformance = allJobs
                .GroupBy(j => j.ProductId)
                .Select(g => new ProductPerformanceMetrics
                {
                    ProductId = g.Key,
                    ProductName = g.First().Product?.Name ?? "Unknown",
                    JobsCompleted = g.Count(j => j.Status == JobStatus.Completed),
                    JobsFailed = g.Count(j => j.Status == JobStatus.Failed),
                    TotalRowsProcessed = g.Sum(j => j.ProcessedRows),
                    AverageDuration = g.Where(j => j.Duration.HasValue).Select(j => j.Duration!.Value.TotalMinutes).DefaultIfEmpty().Average()
                })
                .OrderByDescending(p => p.JobsCompleted)
                .ToList();

            return systemResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system aggregate results");
            throw;
        }
    }

    public async Task<DetailedJobReport> GenerateDetailedJobReportAsync(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                throw new ArgumentException("Job not found", nameof(jobId));
            }

            var report = new DetailedJobReport
            {
                JobId = jobId,
                JobName = job.JobName,
                ProductId = job.ProductId,
                ProductName = job.Product?.Name ?? "Unknown",
                AgentId = job.AgentId,
                AgentName = job.Agent?.Name ?? "Unknown",
                Status = job.Status,
                Priority = job.Priority,
                CreatedAt = job.CreatedAt,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt,
                Duration = job.Duration,
                ProcessedRows = job.ProcessedRows,
                TotalRows = job.TotalRows,
                TablesCompleted = job.TablesCompleted,
                TotalTables = job.TotalTables,
                ProgressPercentage = job.ProgressPercentage,
                RowsPerSecond = job.RowsPerSecond,
                ErrorMessage = job.ErrorMessage,
                ResultSummary = job.ResultSummary,
                RetryCount = job.RetryCount,
                GeneratedAt = DateTime.UtcNow
            };

            // Calculate derived metrics
            if (job.StartedAt.HasValue && job.Duration.HasValue)
            {
                report.EffectiveStartTime = job.StartedAt.Value;
                report.EffectiveEndTime = job.StartedAt.Value.Add(job.Duration.Value);
            }

            report.CompletionRate = job.TotalTables > 0 ? 
                (job.TablesCompleted * 100.0 / job.TotalTables) : (double)job.ProgressPercentage;

            // Get execution logs
            report.ExecutionLogs = job.ExecutionLogs?
                .OrderBy(l => l.Timestamp)
                .Take(100) // Limit to last 100 logs
                .ToList() ?? new List<JobExecutionLog>();

            // Try to get additional details from blob storage if available
            if (!string.IsNullOrEmpty(job.ResultsBlobUrl))
            {
                try
                {
                    // This would fetch additional details from blob storage
                    report.HasDetailedResults = true;
                    report.ResultsBlobUrl = job.ResultsBlobUrl;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch additional details from blob storage for job {JobId}", jobId);
                    report.HasDetailedResults = false;
                }
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate detailed job report for job {JobId}", jobId);
            throw;
        }
    }

    public async Task<ComprehensiveSystemReport> GenerateComprehensiveSystemReportAsync()
    {
        try
        {
            var report = new ComprehensiveSystemReport
            {
                GeneratedAt = DateTime.UtcNow,
                ReportPeriod = "Last 30 days"
            };

            // Get system-wide aggregates
            report.SystemAggregates = await GetSystemAggregateResultsAsync();

            // Get all products
            var products = await _productRepository.GetAllAsync();
            
            // Get aggregates for each product
            report.ProductReports = new List<JobAggregateResults>();
            foreach (var product in products)
            {
                var productAggregates = await GetJobAggregateResultsAsync(product.Id);
                report.ProductReports.Add(productAggregates);
            }

            // Agent health and performance summary
            var allAgents = await _agentRepository.GetAllAsync();
            report.AgentHealthSummary = allAgents.Select(a => new AgentHealthSummary
            {
                AgentId = a.Id,
                AgentName = a.Name,
                Status = a.Status,
                Location = a.Location,
                IsOnline = a.Status == AgentStatus.Online,
                LastHeartbeat = a.LastHeartbeat,
                CompletedJobs = a.CompletedJobs,
                FailedJobs = a.FailedJobs,
                CurrentCpuUsage = a.CurrentCpuUsage,
                CurrentMemoryUsage = a.CurrentMemoryUsage,
                ActiveJobs = a.ActiveJobs,
                MaxConcurrentJobs = a.MaxConcurrentJobs
            }).ToList();

            // System recommendations
            report.Recommendations = await GenerateSystemRecommendationsAsync(report.SystemAggregates, report.AgentHealthSummary);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate comprehensive system report");
            throw;
        }
    }

    private async Task<List<SystemRecommendation>> GenerateSystemRecommendationsAsync(
        SystemAggregateResults systemAggregates, 
        List<AgentHealthSummary> agentHealth)
    {
        var recommendations = new List<SystemRecommendation>();

        // Performance recommendations
        if (systemAggregates.SuccessRate < 80)
        {
            recommendations.Add(new SystemRecommendation
            {
                Type = "Performance",
                Priority = "High",
                Title = "Low Success Rate Detected",
                Description = $"System success rate is {systemAggregates.SuccessRate:F1}%. Investigate job failures and agent stability.",
                Action = "Review failed job logs and agent health status"
            });
        }

        // Capacity recommendations
        if (systemAggregates.CapacityUtilization > 90)
        {
            recommendations.Add(new SystemRecommendation
            {
                Type = "Capacity",
                Priority = "High",
                Title = "High Capacity Utilization",
                Description = $"System is running at {systemAggregates.CapacityUtilization:F1}% capacity. Consider adding more agents.",
                Action = "Scale out by adding more agents or increase existing agent capacity"
            });
        }
        else if (systemAggregates.CapacityUtilization < 30)
        {
            recommendations.Add(new SystemRecommendation
            {
                Type = "Capacity",
                Priority = "Low",
                Title = "Low Capacity Utilization",
                Description = $"System is running at {systemAggregates.CapacityUtilization:F1}% capacity. Consider optimizing resource allocation.",
                Action = "Review agent allocation or reduce agent capacity during low demand periods"
            });
        }

        // Agent health recommendations
        var offlineAgents = agentHealth.Count(a => !a.IsOnline);
        if (offlineAgents > 0)
        {
            recommendations.Add(new SystemRecommendation
            {
                Type = "Agent Health",
                Priority = "Medium",
                Title = "Offline Agents Detected",
                Description = $"{offlineAgents} agents are currently offline. This may impact job processing capacity.",
                Action = "Check agent connectivity and restart offline agents"
            });
        }

        // Performance trend recommendations
        if (systemAggregates.AverageRowsPerSecond < 1000)
        {
            recommendations.Add(new SystemRecommendation
            {
                Type = "Performance",
                Priority = "Medium",
                Title = "Low Processing Throughput",
                Description = $"Average processing speed is {systemAggregates.AverageRowsPerSecond:F0} rows/second. Consider performance optimization.",
                Action = "Review job configurations, agent resources, and database performance"
            });
        }

        return recommendations;
    }
}

public interface IResultAggregationService
{
    Task<JobAggregateResults> GetJobAggregateResultsAsync(Guid productId, DateTime? startDate = null, DateTime? endDate = null);
    Task<SystemAggregateResults> GetSystemAggregateResultsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<DetailedJobReport> GenerateDetailedJobReportAsync(Guid jobId);
    Task<ComprehensiveSystemReport> GenerateComprehensiveSystemReportAsync();
}

// Result aggregation models
public class JobAggregateResults
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public long TotalRowsProcessed { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDuration { get; set; }
    public double AverageRowsPerSecond { get; set; }
    public double TotalProcessingTime { get; set; }
    public DateTime GeneratedAt { get; set; }
    
    public List<AgentPerformanceMetrics> AgentPerformance { get; set; } = new();
    public List<DailyJobTrend> DailyTrends { get; set; } = new();
}

public class SystemAggregateResults
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public long TotalRowsProcessed { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDuration { get; set; }
    public double AverageRowsPerSecond { get; set; }
    public double TotalProcessingTime { get; set; }
    public int TotalAgents { get; set; }
    public int OnlineAgents { get; set; }
    public int TotalCapacity { get; set; }
    public int UsedCapacity { get; set; }
    public double CapacityUtilization { get; set; }
    public DateTime GeneratedAt { get; set; }
    
    public List<ProductPerformanceMetrics> ProductPerformance { get; set; } = new();
}

public class AgentPerformanceMetrics
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public int JobsCompleted { get; set; }
    public int JobsFailed { get; set; }
    public long TotalRowsProcessed { get; set; }
    public double AverageDuration { get; set; }
    public double AverageRowsPerSecond { get; set; }
}

public class ProductPerformanceMetrics
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int JobsCompleted { get; set; }
    public int JobsFailed { get; set; }
    public long TotalRowsProcessed { get; set; }
    public double AverageDuration { get; set; }
}

public class DailyJobTrend
{
    public DateTime Date { get; set; }
    public int JobsCompleted { get; set; }
    public int JobsFailed { get; set; }
    public long TotalRowsProcessed { get; set; }
    public double AverageDuration { get; set; }
}

public class DetailedJobReport
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public JobPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime EffectiveStartTime { get; set; }
    public DateTime EffectiveEndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public long ProcessedRows { get; set; }
    public long TotalRows { get; set; }
    public int TablesCompleted { get; set; }
    public int TotalTables { get; set; }
    public decimal ProgressPercentage { get; set; }
    public double CompletionRate { get; set; }
    public decimal? RowsPerSecond { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultSummary { get; set; }
    public int RetryCount { get; set; }
    public bool HasDetailedResults { get; set; }
    public string? ResultsBlobUrl { get; set; }
    public DateTime GeneratedAt { get; set; }
    
    public List<JobExecutionLog> ExecutionLogs { get; set; } = new();
}

public class ComprehensiveSystemReport
{
    public DateTime GeneratedAt { get; set; }
    public string ReportPeriod { get; set; } = string.Empty;
    public SystemAggregateResults SystemAggregates { get; set; } = new();
    public List<JobAggregateResults> ProductReports { get; set; } = new();
    public List<AgentHealthSummary> AgentHealthSummary { get; set; } = new();
    public List<SystemRecommendation> Recommendations { get; set; } = new();
}

public class AgentHealthSummary
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public string Location { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public decimal CurrentCpuUsage { get; set; }
    public decimal CurrentMemoryUsage { get; set; }
    public int ActiveJobs { get; set; }
    public int MaxConcurrentJobs { get; set; }
}

public class SystemRecommendation
{
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}