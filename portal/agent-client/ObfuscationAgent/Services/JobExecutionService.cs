using Microsoft.Extensions.Logging;
using ObfuscationAgent.Models;
using Contracts.Models;
using System.Text.Json;

namespace ObfuscationAgent.Services;

public interface IJobExecutionService
{
    Task ExecuteJobAsync(Models.JobAssignment job, CancellationToken cancellationToken);
}

public class JobExecutionService : IJobExecutionService
{
    private readonly IPortalCommunicationService _portalService;
    private readonly IObfuscationEngineService _obfuscationEngine;
    private readonly IProgressReportingService _progressReporting;
    private readonly IAgentStateService _agentState;
    private readonly ILogger<JobExecutionService> _logger;

    public JobExecutionService(
        IPortalCommunicationService portalService,
        IObfuscationEngineService obfuscationEngine,
        IProgressReportingService progressReporting,
        IAgentStateService agentState,
        ILogger<JobExecutionService> logger)
    {
        _portalService = portalService;
        _obfuscationEngine = obfuscationEngine;
        _progressReporting = progressReporting;
        _agentState = agentState;
        _logger = logger;
    }

    public async Task ExecuteJobAsync(Models.JobAssignment job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting execution of job {JobId}: {JobName}", job.JobId, job.JobName);

        try
        {
            // Update job status to Running
            await _portalService.UpdateJobStatusAsync(job.JobId, new Models.JobStatusUpdate
            {
                Status = JobStatus.Running,
                Message = "Job execution started"
            });

            // Update local state
            var stats = await _agentState.GetStatisticsAsync();
            await _agentState.UpdateJobCountAsync(stats.ActiveJobs + 1, stats.CompletedJobs, stats.FailedJobs);

            // Get mapping configuration from API
            _logger.LogInformation("Getting mapping configuration for job {JobId}", job.JobId);
            var mappingConfig = await GetJobConfigurationAsync(job.JobId);

            if (mappingConfig == null)
            {
                throw new InvalidOperationException("Failed to get mapping configuration from API");
            }

            // Set up progress reporting
            _progressReporting.OnProgressUpdate += async (progress) =>
            {
                await _portalService.UpdateJobProgressAsync(job.JobId, new Models.JobProgressUpdate
                {
                    ProgressPercentage = progress.ProgressPercentage,
                    RowsProcessed = progress.RowsProcessed,
                    RowsAffected = progress.RowsAffected,
                    CurrentTable = progress.CurrentTable,
                    CurrentOperation = progress.CurrentOperation,
                    RowsPerSecond = progress.RowsPerSecond,
                    EstimatedRemainingMs = progress.EstimatedRemainingMs
                });
            };

            // Execute pipeline (with stage 3 being obfuscation)
            var result = await ExecutePipelineStagesAsync(job, mappingConfig, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Job {JobId} completed successfully", job.JobId);

                // Upload results
                var resultsJson = JsonSerializer.Serialize(result, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                await _portalService.UploadJobResultsAsync(job.JobId, resultsJson);

                // Update status to Completed
                await _portalService.UpdateJobStatusAsync(job.JobId, new Models.JobStatusUpdate
                {
                    Status = JobStatus.Completed,
                    Message = $"Processed {result.TotalRowsProcessed:N0} rows, affected {result.TotalRowsAffected:N0} rows"
                });

                // Update local state
                stats = await _agentState.GetStatisticsAsync();
                await _agentState.UpdateJobCountAsync(
                    Math.Max(0, stats.ActiveJobs - 1), 
                    stats.CompletedJobs + 1, 
                    stats.FailedJobs);
            }
            else
            {
                throw new Exception(result.ErrorMessage ?? "Obfuscation failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", job.JobId);

            // Update status to Failed
            await _portalService.UpdateJobStatusAsync(job.JobId, new Models.JobStatusUpdate
            {
                Status = JobStatus.Failed,
                Message = ex.Message
            });

            // Update local state
            var stats = await _agentState.GetStatisticsAsync();
            await _agentState.UpdateJobCountAsync(
                Math.Max(0, stats.ActiveJobs - 1), 
                stats.CompletedJobs, 
                stats.FailedJobs + 1);
        }
    }

    private async Task<string?> GetJobConfigurationAsync(Guid jobId)
    {
        try
        {
            // Get the configuration from the portal API
            var config = await _portalService.GetJobConfigurationAsync(jobId);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job configuration from API");
            return null;
        }
    }

    private async Task<PipelineExecutionResult> ExecutePipelineStagesAsync(
        Models.JobAssignment job, 
        string mappingConfig, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing 5-stage pipeline for job {JobId}", job.JobId);
        
        var result = new PipelineExecutionResult 
        { 
            JobId = job.JobId, 
            Success = true 
        };

        try
        {
            // Stage 1: Data Reduction (SQL scripts - placeholder)
            _logger.LogInformation("Stage 1: Data Reduction");
            await Task.Delay(100, cancellationToken); // Placeholder for SQL scripts
            
            // Stage 2: Pre-Obfuscation (SQL scripts - placeholder) 
            _logger.LogInformation("Stage 2: Pre-Obfuscation");
            await Task.Delay(100, cancellationToken); // Placeholder for SQL scripts
            
            // Stage 3: Obfuscation (call data-obfuscation program with JSON config)
            _logger.LogInformation("Stage 3: Obfuscation - calling data-obfuscation program with JSON config");
            var obfuscationResult = await _obfuscationEngine.ExecuteObfuscationAsync(
                job.ConnectionString,
                mappingConfig,
                job.Parameters,
                cancellationToken);

            if (!obfuscationResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = obfuscationResult.ErrorMessage ?? "Obfuscation failed";
                result.FailedStage = "Obfuscation";
                return result;
            }

            result.TotalRowsProcessed = obfuscationResult.TotalRowsProcessed;
            result.TotalRowsAffected = obfuscationResult.TotalRowsAffected;
            
            // Stage 4: Post-Obfuscation (SQL scripts - placeholder)
            _logger.LogInformation("Stage 4: Post-Obfuscation");
            await Task.Delay(100, cancellationToken); // Placeholder for SQL scripts
            
            // Stage 5: Testing & Validation (SQL scripts - placeholder)
            _logger.LogInformation("Stage 5: Testing & Validation");
            await Task.Delay(100, cancellationToken); // Placeholder for SQL scripts

            _logger.LogInformation("All 5 pipeline stages completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline execution failed at stage");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}

// Simple result class for pipeline execution
public class PipelineExecutionResult
{
    public Guid JobId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FailedStage { get; set; }
    public long TotalRowsProcessed { get; set; }
    public long TotalRowsAffected { get; set; }
}