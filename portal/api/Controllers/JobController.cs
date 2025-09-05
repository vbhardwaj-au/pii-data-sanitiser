using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using API.Authorization;
using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;
using API.Services;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Job Management")]
[RequireRole(Roles.Admin, Roles.ProductAdmin, Roles.Analyst)]
public class JobController : ControllerBase
{
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IJobDistributionService _jobDistributionService;
    private readonly ILogger<JobController> _logger;

    public JobController(
        IObfuscationJobRepository jobRepository,
        IObfuscationAgentRepository agentRepository,
        IProductRepository productRepository,
        IBlobStorageService blobStorageService,
        IJobDistributionService jobDistributionService,
        ILogger<JobController> logger)
    {
        _jobRepository = jobRepository;
        _agentRepository = agentRepository;
        _productRepository = productRepository;
        _blobStorageService = blobStorageService;
        _jobDistributionService = jobDistributionService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new obfuscation job
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create Job", Description = "Create a new obfuscation job")]
    [SwaggerResponse(200, "Job created successfully", typeof(JobCreationResponse))]
    [SwaggerResponse(400, "Invalid job request")]
    [SwaggerResponse(404, "Product not found")]
    public async Task<ActionResult<JobCreationResponse>> CreateJob([FromBody] JobCreationRequest request)
    {
        try
        {
            // Validate product exists
            var product = await _productRepository.GetByIdAsync(request.ProductId);
            if (product == null)
            {
                return NotFound(new JobCreationResponse
                {
                    Success = false,
                    ErrorMessage = "Product not found"
                });
            }

            // Get or create mapping version
            string mappingVersion = request.MappingVersion ?? 
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");

            // Export mappings to Azure if no version specified
            string mappingSasUrl;
            if (string.IsNullOrEmpty(request.MappingVersion))
            {
                var blobUrl = await _blobStorageService.UploadMappingAsync(
                    request.ProductId, 
                    await GenerateUnifiedMappingAsync(product), 
                    mappingVersion);
                
                mappingSasUrl = await _blobStorageService.GetMappingSasUrlAsync(
                    request.ProductId, 
                    mappingVersion, 
                    168); // 7 days
            }
            else
            {
                mappingSasUrl = await _blobStorageService.GetMappingSasUrlAsync(
                    request.ProductId, 
                    request.MappingVersion, 
                    168);
            }

            // Create job
            var job = new ObfuscationJob
            {
                ProductId = request.ProductId,
                JobName = request.JobName,
                Description = request.Description,
                Priority = request.Priority,
                MappingVersion = mappingVersion,
                MappingSasUrl = mappingSasUrl,
                Configuration = request.Configuration,
                Status = JobStatus.Pending,
                CreatedBy = "API"
            };

            var createdJob = await _jobRepository.CreateAsync(job);

            // Try to assign to preferred agent if specified
            Guid? assignedAgentId = null;
            string? assignedAgentName = null;

            if (request.PreferredAgentId.HasValue)
            {
                var preferredAgent = await _agentRepository.GetByIdAsync(request.PreferredAgentId.Value);
                if (preferredAgent != null && preferredAgent.IsEnabled && 
                    preferredAgent.ActiveJobs < preferredAgent.MaxConcurrentJobs)
                {
                    await _jobRepository.AssignJobToAgentAsync(createdJob.Id, preferredAgent.Id);
                    assignedAgentId = preferredAgent.Id;
                    assignedAgentName = preferredAgent.Name;
                }
            }

            var response = new JobCreationResponse
            {
                JobId = createdJob.Id,
                JobName = createdJob.JobName,
                Status = createdJob.Status,
                AssignedAgentId = assignedAgentId,
                AssignedAgentName = assignedAgentName,
                CreatedAt = createdJob.CreatedAt,
                MappingVersion = mappingVersion,
                Success = true
            };

            _logger.LogInformation("Created job {JobName} for product {ProductName}", 
                request.JobName, product.Name);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create job {JobName}", request.JobName);
            return StatusCode(500, new JobCreationResponse
            {
                Success = false,
                ErrorMessage = "Internal server error during job creation"
            });
        }
    }

    /// <summary>
    /// Get all jobs
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List Jobs", Description = "Get all obfuscation jobs")]
    [SwaggerResponse(200, "Jobs retrieved successfully")]
    public async Task<ActionResult> GetJobs(
        [FromQuery] JobStatus? status = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] Guid? agentId = null,
        [FromQuery] int? days = null)
    {
        try
        {
            IEnumerable<ObfuscationJob> jobs;

            if (status.HasValue)
            {
                jobs = await _jobRepository.GetByStatusAsync(status.Value);
            }
            else if (productId.HasValue)
            {
                jobs = await _jobRepository.GetByProductIdAsync(productId.Value);
            }
            else if (agentId.HasValue)
            {
                jobs = await _jobRepository.GetByAgentIdAsync(agentId.Value);
            }
            else
            {
                jobs = await _jobRepository.GetAllAsync();
            }

            // Filter by days if specified
            if (days.HasValue)
            {
                var cutoff = DateTime.UtcNow.AddDays(-days.Value);
                jobs = jobs.Where(j => j.CreatedAt >= cutoff);
            }

            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve jobs");
            return StatusCode(500, new { error = "Failed to retrieve jobs" });
        }
    }

    /// <summary>
    /// Get job by ID
    /// </summary>
    [HttpGet("{jobId}")]
    [SwaggerOperation(Summary = "Get Job", Description = "Get job details by ID")]
    [SwaggerResponse(200, "Job retrieved successfully")]
    [SwaggerResponse(404, "Job not found")]
    public async Task<ActionResult> GetJob(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to retrieve job" });
        }
    }

    /// <summary>
    /// Cancel a job
    /// </summary>
    [HttpPost("{jobId}/cancel")]
    [SwaggerOperation(Summary = "Cancel Job", Description = "Cancel a running or pending job")]
    [SwaggerResponse(200, "Job cancelled successfully")]
    [SwaggerResponse(404, "Job not found")]
    [SwaggerResponse(400, "Job cannot be cancelled")]
    public async Task<ActionResult> CancelJob(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            if (job.Status == JobStatus.Completed || job.Status == JobStatus.Cancelled)
            {
                return BadRequest(new { error = "Job is already completed or cancelled" });
            }

            await _jobRepository.UpdateStatusAsync(jobId, JobStatus.Cancelled);

            _logger.LogInformation("Cancelled job {JobName} (ID: {JobId})", job.JobName, jobId);
            return Ok(new { message = "Job cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to cancel job" });
        }
    }

    /// <summary>
    /// Retry a failed job
    /// </summary>
    [HttpPost("{jobId}/retry")]
    [SwaggerOperation(Summary = "Retry Job", Description = "Retry a failed job")]
    [SwaggerResponse(200, "Job queued for retry")]
    [SwaggerResponse(404, "Job not found")]
    [SwaggerResponse(400, "Job cannot be retried")]
    public async Task<ActionResult> RetryJob(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            if (job.Status != JobStatus.Failed && job.Status != JobStatus.Cancelled)
            {
                return BadRequest(new { error = "Only failed or cancelled jobs can be retried" });
            }

            if (job.RetryCount >= job.MaxRetries)
            {
                return BadRequest(new { error = "Job has exceeded maximum retry attempts" });
            }

            // Reset job for retry
            job.Status = JobStatus.Pending;
            job.ErrorMessage = null;
            job.AgentId = null;
            job.StartedAt = null;
            job.CompletedAt = null;
            job.ProgressPercentage = 0;
            job.ProcessedRows = 0;
            job.TablesCompleted = 0;
            job.RetryCount++;

            await _jobRepository.UpdateAsync(job);

            _logger.LogInformation("Queued job {JobName} for retry (attempt {RetryCount})", 
                job.JobName, job.RetryCount);
            
            return Ok(new { message = "Job queued for retry" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to retry job" });
        }
    }

    /// <summary>
    /// Get job execution logs
    /// </summary>
    [HttpGet("{jobId}/logs")]
    [SwaggerOperation(Summary = "Get Job Logs", Description = "Get execution logs for a job")]
    [SwaggerResponse(200, "Logs retrieved successfully")]
    [SwaggerResponse(404, "Job not found")]
    public async Task<ActionResult> GetJobLogs(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            return Ok(job.ExecutionLogs?.OrderByDescending(l => l.Timestamp).ToList() ?? new List<JobExecutionLog>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve logs for job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to retrieve job logs" });
        }
    }

    /// <summary>
    /// Get job queue statistics
    /// </summary>
    [HttpGet("queue/stats")]
    [SwaggerOperation(Summary = "Queue Statistics", Description = "Get job queue statistics")]
    [SwaggerResponse(200, "Statistics retrieved successfully")]
    public async Task<ActionResult> GetQueueStats()
    {
        try
        {
            var pendingJobs = await _jobRepository.GetByStatusAsync(JobStatus.Pending);
            var runningJobs = await _jobRepository.GetByStatusAsync(JobStatus.Running);
            var assignedJobs = await _jobRepository.GetByStatusAsync(JobStatus.Assigned);
            var completedJobs = await _jobRepository.GetCompletedJobsAsync(1);
            var failedJobs = await _jobRepository.GetFailedJobsAsync(1);

            var onlineAgents = await _agentRepository.GetOnlineAgentsAsync();
            var availableAgents = await _agentRepository.GetAvailableAgentsAsync();

            var stats = new
            {
                Jobs = new
                {
                    Pending = pendingJobs.Count(),
                    Running = runningJobs.Count(),
                    Assigned = assignedJobs.Count(),
                    CompletedToday = completedJobs.Count(),
                    FailedToday = failedJobs.Count(),
                    QueueLength = await _jobRepository.GetQueueLengthAsync()
                },
                Agents = new
                {
                    Online = onlineAgents.Count(),
                    Available = availableAgents.Count(),
                    TotalCapacity = availableAgents.Sum(a => a.MaxConcurrentJobs),
                    UsedCapacity = onlineAgents.Sum(a => a.ActiveJobs)
                },
                Performance = new
                {
                    AverageJobDuration = completedJobs.Any() ? 
                        completedJobs.Where(j => j.Duration.HasValue)
                                   .Average(j => j.Duration!.Value.TotalMinutes) : 0,
                    AverageRowsPerSecond = completedJobs.Any() ?
                        completedJobs.Where(j => j.RowsPerSecond.HasValue)
                                   .Average(j => j.RowsPerSecond!.Value) : 0
                }
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve queue statistics");
            return StatusCode(500, new { error = "Failed to retrieve queue statistics" });
        }
    }

    /// <summary>
    /// Force job distribution to available agents
    /// </summary>
    [HttpPost("distribute")]
    [RequireRole(Roles.Admin, Roles.ProductAdmin)]
    [SwaggerOperation(Summary = "Distribute Jobs", Description = "Force distribution of pending jobs to available agents")]
    [SwaggerResponse(200, "Job distribution completed")]
    public async Task<ActionResult> DistributeJobs()
    {
        try
        {
            var result = await _jobDistributionService.DistributePendingJobsAsync();
            
            _logger.LogInformation("Distributed {JobCount} jobs to {AgentCount} agents", 
                result.AssignedJobs, result.AvailableAgents);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to distribute jobs");
            return StatusCode(500, new { error = "Failed to distribute jobs" });
        }
    }

    /// <summary>
    /// Create test jobs for demonstration
    /// </summary>
    [HttpPost("test")]
    [RequireRole(Roles.Admin, Roles.ProductAdmin, Roles.Analyst, "Agent")]
    [SwaggerOperation(Summary = "Create Test Jobs", Description = "Create test jobs for simulating obfuscation workload")]
    [SwaggerResponse(200, "Test jobs created successfully")]
    public async Task<ActionResult> CreateTestJobs([FromQuery] int count = 5, [FromQuery] int durationSeconds = 60)
    {
        try
        {
            // Test job service removed - functionality disabled
            _logger.LogWarning("Test job creation requested but service is disabled");
            return StatusCode(501, new { error = "Test job service not available" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create test jobs");
            return StatusCode(500, new { error = "Failed to create test jobs" });
        }
    }

    private async Task<string> GenerateUnifiedMappingAsync(Product product)
    {
        // This would generate the unified mapping JSON similar to MappingStorageController
        // For now, return a placeholder - implement full mapping generation logic
        var config = new
        {
            ProductInfo = new
            {
                Id = product.Id,
                Name = product.Name,
                DatabaseTechnology = product.DatabaseTechnology,
                ConnectionString = product.ConnectionString,
                GlobalSeed = product.GlobalSeed
            },
            ProcessingSettings = new
            {
                BatchSize = product.BatchSize,
                SqlBatchSize = product.SqlBatchSize,
                ParallelThreads = product.ParallelThreads,
                CommandTimeoutSeconds = product.CommandTimeoutSeconds,
                MaxCacheSize = product.MaxCacheSize
            },
            GeneratedAt = DateTimeOffset.UtcNow,
            Version = "1.0"
        };

        return System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}