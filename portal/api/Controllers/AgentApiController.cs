using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using API.Authorization;
using API.Services;
using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;

namespace API.Controllers;

[ApiController]
[Route("api/agent")]
[SwaggerTag("Agent Communication")]
[AgentAuthentication] // Use agent-specific authentication
public class AgentApiController : ControllerBase
{
    private readonly IAgentManagementService _agentManagementService;
    private readonly IJobDistributionService _jobDistributionService;
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<AgentApiController> _logger;

    public AgentApiController(
        IAgentManagementService agentManagementService,
        IJobDistributionService jobDistributionService,
        IObfuscationJobRepository jobRepository,
        IBlobStorageService blobStorageService,
        ILogger<AgentApiController> logger)
    {
        _agentManagementService = agentManagementService;
        _jobDistributionService = jobDistributionService;
        _jobRepository = jobRepository;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Submit job progress update
    /// </summary>
    [HttpPost("jobs/{jobId}/progress")]
    [SwaggerOperation(Summary = "Update Job Progress", Description = "Agent updates job execution progress")]
    [SwaggerResponse(200, "Progress updated successfully")]
    [SwaggerResponse(404, "Job not found")]
    public async Task<ActionResult> UpdateJobProgress(Guid jobId, [FromBody] JobProgressUpdate progressUpdate)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            // Validate agent owns this job
            var agentId = GetCurrentAgentId();
            if (job.AgentId != agentId)
            {
                return Forbid("Agent does not own this job");
            }

            await _jobRepository.UpdateProgressAsync(
                jobId, 
                progressUpdate.ProgressPercentage,
                progressUpdate.ProcessedRows,
                progressUpdate.TablesCompleted);

            _logger.LogInformation("Agent {AgentId} updated progress for job {JobId}: {Progress}%",
                agentId, jobId, progressUpdate.ProgressPercentage);

            return Ok(new { message = "Progress updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job progress for job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to update job progress" });
        }
    }

    /// <summary>
    /// Submit job status update (started, completed, failed)
    /// </summary>
    [HttpPost("jobs/{jobId}/status")]
    [SwaggerOperation(Summary = "Update Job Status", Description = "Agent updates job execution status")]
    [SwaggerResponse(200, "Status updated successfully")]
    [SwaggerResponse(404, "Job not found")]
    public async Task<ActionResult> UpdateJobStatus(Guid jobId, [FromBody] JobStatusUpdateRequest statusUpdate)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            // Validate agent owns this job
            var agentId = GetCurrentAgentId();
            if (job.AgentId != agentId)
            {
                return Forbid("Agent does not own this job");
            }

            switch (statusUpdate.Status)
            {
                case JobStatus.Running:
                    if (job.Status == JobStatus.Assigned)
                    {
                        await _jobRepository.MarkJobAsStartedAsync(jobId, DateTime.UtcNow);
                        _logger.LogInformation("Agent {AgentId} started job {JobId}", agentId, jobId);
                    }
                    break;

                case JobStatus.Completed:
                    await _jobRepository.MarkJobAsCompletedAsync(
                        jobId, 
                        DateTime.UtcNow,
                        statusUpdate.ResultSummary,
                        statusUpdate.ResultsBlobUrl);
                    _logger.LogInformation("Agent {AgentId} completed job {JobId}", agentId, jobId);
                    break;

                case JobStatus.Failed:
                    await _jobRepository.MarkJobAsFailedAsync(
                        jobId,
                        statusUpdate.ErrorMessage ?? "Job failed without specific error message",
                        shouldRetry: true);
                    _logger.LogWarning("Agent {AgentId} failed job {JobId}: {Error}", 
                        agentId, jobId, statusUpdate.ErrorMessage);
                    break;

                default:
                    return BadRequest(new { error = $"Invalid status transition to {statusUpdate.Status}" });
            }

            return Ok(new { message = $"Job status updated to {statusUpdate.Status}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job status for job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to update job status" });
        }
    }

    /// <summary>
    /// Add execution log entry for a job
    /// </summary>
    [HttpPost("jobs/{jobId}/logs")]
    [SwaggerOperation(Summary = "Add Job Log", Description = "Agent adds execution log entry")]
    [SwaggerResponse(200, "Log added successfully")]
    [SwaggerResponse(404, "Job not found")]
    public async Task<ActionResult> AddJobLog(Guid jobId, [FromBody] JobLogEntry logEntry)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            // Validate agent owns this job
            var agentId = GetCurrentAgentId();
            if (job.AgentId != agentId)
            {
                return Forbid("Agent does not own this job");
            }

            var executionLog = new JobExecutionLog
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Timestamp = DateTime.UtcNow,
                LogLevel = Enum.TryParse<Contracts.Models.LogLevel>(logEntry.LogLevel, true, out var level) ? level : Contracts.Models.LogLevel.Information,
                Message = logEntry.Message,
                Details = logEntry.Details
            };

            // Add log to job (assuming we extend the repository to support this)
            job.ExecutionLogs.Add(executionLog);
            await _jobRepository.UpdateAsync(job);

            _logger.LogDebug("Agent {AgentId} added log entry for job {JobId}: {Message}",
                agentId, jobId, logEntry.Message);

            return Ok(new { message = "Log entry added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add job log for job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to add job log" });
        }
    }

    /// <summary>
    /// Request new job assignment
    /// </summary>
    [HttpPost("jobs/request")]
    [SwaggerOperation(Summary = "Request Job Assignment", Description = "Agent requests a new job to execute")]
    [SwaggerResponse(200, "Job assignment response")]
    public async Task<ActionResult<JobAssignmentResponse>> RequestJobAssignment([FromBody] JobRequest request)
    {
        try
        {
            var agentId = GetCurrentAgentId();
            
            // Check if agent has capacity for new jobs
            var agent = await _agentManagementService.ValidateApiKeyAsync(GetCurrentApiKey());
            if (!agent.IsValid)
            {
                return Unauthorized("Invalid agent credentials");
            }

            // Try to find a suitable job
            var bestJobId = await _jobDistributionService.FindBestAgentForJobAsync(agentId);
            if (!bestJobId.HasValue)
            {
                return Ok(new JobAssignmentResponse
                {
                    HasJob = false,
                    Message = "No suitable jobs available at this time"
                });
            }

            // Assign the job
            await _jobDistributionService.AssignJobToAgentAsync(bestJobId.Value, agentId);
            var assignedJob = await _jobRepository.GetByIdAsync(bestJobId.Value);

            if (assignedJob == null)
            {
                return Ok(new JobAssignmentResponse
                {
                    HasJob = false,
                    Message = "Job assignment failed"
                });
            }

            var response = new JobAssignmentResponse
            {
                HasJob = true,
                Job = new JobAssignment
                {
                    JobId = assignedJob.Id,
                    ProductId = assignedJob.ProductId,
                    JobName = assignedJob.JobName,
                    Description = assignedJob.Description,
                    Priority = assignedJob.Priority,
                    MappingVersion = assignedJob.MappingVersion,
                    MappingSasUrl = assignedJob.MappingSasUrl,
                    Configuration = assignedJob.Configuration,
                    AssignedAt = DateTime.UtcNow,
                    ProductConnectionString = assignedJob.Product?.ConnectionString ?? ""
                },
                Message = "Job assigned successfully"
            };

            _logger.LogInformation("Assigned job {JobId} to agent {AgentId}", assignedJob.Id, agentId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process job request from agent");
            return StatusCode(500, new { error = "Failed to process job request" });
        }
    }

    /// <summary>
    /// Upload job results to blob storage
    /// </summary>
    [HttpPost("jobs/{jobId}/results")]
    [SwaggerOperation(Summary = "Upload Job Results", Description = "Agent uploads job execution results")]
    [SwaggerResponse(200, "Results uploaded successfully")]
    [SwaggerResponse(404, "Job not found")]
    public async Task<ActionResult> UploadJobResults(Guid jobId, [FromBody] JobResultsUpload resultsUpload)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            // Validate agent owns this job
            var agentId = GetCurrentAgentId();
            if (job.AgentId != agentId)
            {
                return Forbid("Agent does not own this job");
            }

            // Upload results to blob storage
            var resultsBlobUrl = await _blobStorageService.UploadJobResultsAsync(
                job.ProductId,
                jobId,
                resultsUpload.ResultsData,
                $"job-results-{jobId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}");

            // Update job with results URL
            job.ResultsBlobUrl = resultsBlobUrl;
            job.ResultSummary = resultsUpload.Summary;
            await _jobRepository.UpdateAsync(job);

            _logger.LogInformation("Agent {AgentId} uploaded results for job {JobId} to {BlobUrl}",
                agentId, jobId, resultsBlobUrl);

            return Ok(new 
            { 
                message = "Results uploaded successfully",
                resultsBlobUrl = resultsBlobUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload job results for job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to upload job results" });
        }
    }

    /// <summary>
    /// Get mapping file download URL
    /// </summary>
    [HttpGet("jobs/{jobId}/mapping")]
    [SwaggerOperation(Summary = "Get Mapping Download URL", Description = "Get SAS URL for mapping file download")]
    [SwaggerResponse(200, "Mapping URL retrieved successfully")]
    [SwaggerResponse(404, "Job not found")]
    public async Task<ActionResult> GetMappingDownloadUrl(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            // Validate agent owns this job
            var agentId = GetCurrentAgentId();
            if (job.AgentId != agentId)
            {
                return Forbid("Agent does not own this job");
            }

            // Ensure SAS URL is still valid (refresh if needed)
            var mappingUrl = await _blobStorageService.GetMappingSasUrlAsync(
                job.ProductId,
                job.MappingVersion,
                24); // 24 hour validity

            return Ok(new
            {
                jobId = jobId,
                mappingVersion = job.MappingVersion,
                mappingUrl = mappingUrl,
                expiresAt = DateTime.UtcNow.AddHours(24)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get mapping download URL for job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to get mapping download URL" });
        }
    }

    private Guid GetCurrentAgentId()
    {
        // This would be set by the AgentAuthentication middleware
        var agentIdClaim = HttpContext.Items["AgentId"]?.ToString();
        if (Guid.TryParse(agentIdClaim, out var agentId))
        {
            return agentId;
        }
        
        throw new UnauthorizedAccessException("Invalid agent context");
    }

    private string GetCurrentApiKey()
    {
        // Extract API key from Authorization header
        var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ") == true)
        {
            return authHeader.Substring("Bearer ".Length);
        }
        
        throw new UnauthorizedAccessException("Missing API key");
    }
}

// DTOs for agent communication
public class JobProgressUpdate
{
    public decimal ProgressPercentage { get; set; }
    public long ProcessedRows { get; set; }
    public int TablesCompleted { get; set; }
    public string? StatusMessage { get; set; }
}

public class JobStatusUpdateRequest
{
    public JobStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultSummary { get; set; }
    public string? ResultsBlobUrl { get; set; }
}

public class JobLogEntry
{
    public string LogLevel { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class JobRequest
{
    public int MaxConcurrentJobs { get; set; } = 1;
    public List<string>? PreferredJobTypes { get; set; }
}

public class JobAssignmentResponse
{
    public bool HasJob { get; set; }
    public JobAssignment? Job { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class JobResultsUpload
{
    public string ResultsData { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Format { get; set; } = "json"; // json, csv, xml
}