using Microsoft.AspNetCore.Mvc;
using MediatR;
using DAL.Repositories;
using API.Features.Products.ExportProductMapping;
using System.Text.Json;
using Contracts.Models;
using API.Authorization;
using API.Services;

namespace API.Controllers
{
    [ApiController]
    [Route("api/agent")]
    public class AgentJobController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IObfuscationJobRepository _jobRepository;
        private readonly IObfuscationAgentRepository _agentRepository;
        private readonly IRealtimeMonitoringService _monitoringService;
        private readonly ILogger<AgentJobController> _logger;

        public AgentJobController(
            IMediator mediator,
            IObfuscationJobRepository jobRepository,
            IObfuscationAgentRepository agentRepository,
            IRealtimeMonitoringService monitoringService,
            ILogger<AgentJobController> logger)
        {
            _mediator = mediator;
            _jobRepository = jobRepository;
            _agentRepository = agentRepository;
            _monitoringService = monitoringService;
            _logger = logger;
        }

        /// <summary>
        /// Agent requests the JSON configuration for a job
        /// </summary>
        [HttpGet("job/{jobId}/configuration")]
        [AgentAuthentication]
        public async Task<IActionResult> GetJobConfiguration(Guid jobId)
        {
            try
            {
                // Get the agent ID from the context (set by authentication)
                var agentId = HttpContext.Items["AgentId"]?.ToString();
                if (string.IsNullOrEmpty(agentId))
                {
                    return Unauthorized("Agent not authenticated");
                }

                // Get the job
                var job = await _jobRepository.GetByIdAsync(jobId);
                if (job == null)
                {
                    return NotFound($"Job {jobId} not found");
                }

                // Verify the job is assigned to this agent
                if (job.AgentId?.ToString() != agentId)
                {
                    return Forbid($"Job {jobId} is not assigned to agent {agentId}");
                }

                // TEMPORARY: For testing, return the actual adv2-mapping.json file directly
                var mappingFilePath = "/Users/vikas/Code/published/treat-sql-db/JSON/adv2-mapping.json";
                if (System.IO.File.Exists(mappingFilePath))
                {
                    var jsonContent = await System.IO.File.ReadAllTextAsync(mappingFilePath);
                    
                    // Return the raw configuration wrapped with job metadata
                    var enhancedConfig = new
                    {
                        JobId = jobId,
                        JobName = job.JobName,
                        ProductId = job.ProductId,
                        CreatedAt = job.CreatedAt,
                        Configuration = JsonSerializer.Deserialize<JsonDocument>(jsonContent)
                    };
                    
                    return Ok(enhancedConfig);
                }
                
                // Fallback to original logic if file doesn't exist
                var (fileName, json) = await _mediator.Send(new ExportProductMappingQuery(job.ProductId));
                var config = JsonSerializer.Deserialize<JsonDocument>(json);
                var originalConfig = new
                {
                    JobId = jobId,
                    JobName = job.JobName,
                    ProductId = job.ProductId,
                    CreatedAt = job.CreatedAt,
                    Configuration = config
                };

                return Ok(originalConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting job configuration for job {JobId}", jobId);
                return StatusCode(500, "Failed to get job configuration");
            }
        }

        /// <summary>
        /// Agent polls for available jobs
        /// </summary>
        [HttpPost("jobs/poll")]
        [AgentAuthentication]
        public async Task<IActionResult> PollForJobs()
        {
            try
            {
                var agentId = HttpContext.Items["AgentId"]?.ToString();
                if (string.IsNullOrEmpty(agentId))
                {
                    return Unauthorized("Agent not authenticated");
                }

                // Check if agent already has active jobs (Assigned or Running)
                var agentGuid = Guid.Parse(agentId);
                var activeJobs = await _jobRepository.GetByAgentIdAsync(agentGuid);
                var hasActiveJob = activeJobs.Any(j => j.Status == Contracts.Models.JobStatus.Assigned || 
                                                       j.Status == Contracts.Models.JobStatus.Running);
                
                if (hasActiveJob)
                {
                    return NoContent(); // Agent already has an active job
                }

                // Get the next pending job - simplified query
                ObfuscationJob? nextJob = null;
                try 
                {
                    var pendingJobs = await _jobRepository.GetPendingJobsAsync();
                    nextJob = pendingJobs.FirstOrDefault();
                }
                catch (Exception jobEx)
                {
                    _logger.LogError(jobEx, "Error getting pending jobs");
                    // Try a direct query as fallback
                    var jobs = await _jobRepository.GetByStatusAsync(JobStatus.Pending);
                    nextJob = jobs.FirstOrDefault();
                }

                if (nextJob == null)
                {
                    return NoContent(); // No jobs available
                }

                // Assign the job to this agent
                nextJob.AgentId = Guid.Parse(agentId);
                nextJob.Status = Contracts.Models.JobStatus.Assigned;
                nextJob.StartedAt = DateTime.UtcNow;
                await _jobRepository.UpdateAsync(nextJob);

                // Broadcast job assignment to monitoring dashboard
                await _monitoringService.BroadcastJobUpdate(nextJob.Id, nextJob.Status);

                _logger.LogInformation($"Assigned job {nextJob.Id} to agent {agentId}");

                // Return job details
                return Ok(new
                {
                    JobId = nextJob.Id,
                    JobName = nextJob.JobName,
                    ProductId = nextJob.ProductId,
                    Priority = nextJob.Priority,
                    ConfigurationUrl = $"/api/agent/job/{nextJob.Id}/configuration"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling for jobs");
                return StatusCode(500, "Failed to poll for jobs");
            }
        }

        /// <summary>
        /// Agent reports job progress
        /// </summary>
        [HttpPost("job/{jobId}/progress")]
        [AgentAuthentication]
        public async Task<IActionResult> ReportProgress(Guid jobId, [FromBody] JobProgressReport report)
        {
            try
            {
                var agentId = HttpContext.Items["AgentId"]?.ToString();
                
                var job = await _jobRepository.GetByIdAsync(jobId);
                if (job == null) return NotFound();
                
                if (job.AgentId?.ToString() != agentId)
                {
                    return Forbid("Job not assigned to this agent");
                }

                // Update job progress
                job.ProgressPercentage = report.ProgressPercentage;
                job.ProcessedRows = report.ProcessedRows.HasValue ? (long)report.ProcessedRows.Value : job.ProcessedRows;
                job.TotalRows = report.TotalRows.HasValue ? (long)report.TotalRows.Value : job.TotalRows;
                job.TablesCompleted = report.TablesCompleted ?? job.TablesCompleted;
                job.TotalTables = report.TotalTables ?? job.TotalTables;
                job.Status = report.Status == "Completed" ? Contracts.Models.JobStatus.Completed : 
                            report.Status == "Failed" ? Contracts.Models.JobStatus.Failed :
                            Contracts.Models.JobStatus.Running;
                job.LastUpdateAt = DateTime.UtcNow;

                if (report.Status == "Completed")
                {
                    job.CompletedAt = DateTime.UtcNow;
                    job.Duration = TimeSpan.FromSeconds((int)(job.CompletedAt.Value - job.StartedAt.Value).TotalSeconds);
                }

                await _jobRepository.UpdateAsync(job);

                // Broadcast job progress update to monitoring dashboard
                await _monitoringService.BroadcastJobUpdate(jobId, job.Status, job.ProgressPercentage);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting progress for job {JobId}", jobId);
                return StatusCode(500, "Failed to report progress");
            }
        }

        /// <summary>
        /// Agent streams console logs
        /// </summary>
        [HttpPost("job/{jobId}/logs")]
        [AgentAuthentication]
        public async Task<IActionResult> StreamLogs(Guid jobId, [FromBody] AgentJobLogEntry logEntry)
        {
            try
            {
                var agentId = HttpContext.Items["AgentId"]?.ToString();
                
                // Verify job ownership
                var job = await _jobRepository.GetByIdAsync(jobId);
                if (job?.AgentId?.ToString() != agentId)
                {
                    return Forbid();
                }

                // Store log entry (you can implement a log repository)
                _logger.LogInformation($"[Job {jobId}] {logEntry.Message}");

                // TODO: Stream to SignalR hub for real-time monitoring

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming logs for job {JobId}", jobId);
                return StatusCode(500);
            }
        }
    }

    public class JobProgressReport
    {
        public decimal ProgressPercentage { get; set; }
        public int? ProcessedRows { get; set; }
        public int? TotalRows { get; set; }
        public int? TablesCompleted { get; set; }
        public int? TotalTables { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class AgentJobLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "Info";
        public string Message { get; set; } = "";
        public Dictionary<string, object>? Metadata { get; set; }
    }
}