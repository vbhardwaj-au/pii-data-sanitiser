using DAL.Repositories;
using Contracts.Models;

namespace API.Services;

public class JobProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobProcessingService> _logger;
    private readonly TimeSpan _processInterval = TimeSpan.FromSeconds(30);

    public JobProcessingService(
        IServiceProvider serviceProvider,
        ILogger<JobProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Processing Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var jobDistributionService = scope.ServiceProvider.GetRequiredService<IJobDistributionService>();
                
                // Distribute pending jobs to available agents
                var result = await jobDistributionService.DistributePendingJobsAsync();
                
                if (result.AssignedJobs > 0)
                {
                    _logger.LogInformation("Distributed {JobCount} jobs to {AgentCount} agents", 
                        result.AssignedJobs, result.AvailableAgents);
                }

                // Handle job timeouts
                await HandleJobTimeouts(scope);

                // Handle retrying jobs
                await HandleRetryingJobs(scope);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job processing");
            }

            await Task.Delay(_processInterval, stoppingToken);
        }

        _logger.LogInformation("Job Processing Service stopped");
    }

    private async Task HandleJobTimeouts(IServiceScope scope)
    {
        try
        {
            var jobRepository = scope.ServiceProvider.GetRequiredService<IObfuscationJobRepository>();
            var timeoutMinutes = 30; // Jobs timeout after 30 minutes without progress
            
            var runningJobs = await jobRepository.GetByStatusAsync(JobStatus.Running);
            var timedOutJobs = runningJobs.Where(j => 
                j.LastUpdateAt.HasValue && 
                DateTime.UtcNow - j.LastUpdateAt.Value > TimeSpan.FromMinutes(timeoutMinutes));

            foreach (var job in timedOutJobs)
            {
                _logger.LogWarning("Job {JobId} ({JobName}) timed out after {Minutes} minutes", 
                    job.Id, job.JobName, timeoutMinutes);
                
                await jobRepository.MarkJobAsFailedAsync(
                    job.Id, 
                    $"Job timed out after {timeoutMinutes} minutes without progress updates",
                    shouldRetry: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling job timeouts");
        }
    }

    private async Task HandleRetryingJobs(IServiceScope scope)
    {
        try
        {
            var jobRepository = scope.ServiceProvider.GetRequiredService<IObfuscationJobRepository>();
            var retryDelayMinutes = 5; // Wait 5 minutes before retrying
            
            var retryingJobs = await jobRepository.GetByStatusAsync(JobStatus.Retrying);
            var jobsReadyToRetry = retryingJobs.Where(j => 
                j.LastUpdateAt.HasValue && 
                DateTime.UtcNow - j.LastUpdateAt.Value > TimeSpan.FromMinutes(retryDelayMinutes));

            foreach (var job in jobsReadyToRetry)
            {
                _logger.LogInformation("Retrying job {JobId} ({JobName}) - attempt {RetryCount}/{MaxRetries}", 
                    job.Id, job.JobName, job.RetryCount, job.MaxRetries);
                
                // Reset job to pending for redistribution
                job.Status = JobStatus.Pending;
                job.AgentId = null;
                job.StartedAt = null;
                job.ErrorMessage = null;
                job.ProgressPercentage = 0;
                job.ProcessedRows = 0;
                job.TablesCompleted = 0;
                
                await jobRepository.UpdateAsync(job);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling retrying jobs");
        }
    }
}