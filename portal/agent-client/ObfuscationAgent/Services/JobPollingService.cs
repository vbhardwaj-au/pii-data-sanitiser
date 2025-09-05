using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using ObfuscationAgent.Configuration;

namespace ObfuscationAgent.Services;

public class JobPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentStateService _agentState;
    private readonly ILogger<JobPollingService> _logger;
    private readonly PortalConfiguration _portalConfig;

    public JobPollingService(
        IServiceProvider serviceProvider,
        IAgentStateService agentState,
        IOptions<PortalConfiguration> portalConfig,
        ILogger<JobPollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _agentState = agentState;
        _logger = logger;
        _portalConfig = portalConfig.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Initial delay

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var agentId = await _agentState.GetAgentIdAsync();
                
                if (!agentId.HasValue)
                {
                    _logger.LogDebug("Agent not registered yet, skipping job polling");
                    await Task.Delay(TimeSpan.FromSeconds(_portalConfig.JobPollIntervalSeconds), stoppingToken);
                    continue;
                }

                var availableCapacity = await _agentState.GetAvailableCapacityAsync();
                
                if (availableCapacity <= 0)
                {
                    _logger.LogDebug("No available capacity for new jobs");
                    await Task.Delay(TimeSpan.FromSeconds(_portalConfig.JobPollIntervalSeconds), stoppingToken);
                    continue;
                }

                using (var scope = _serviceProvider.CreateScope())
                {
                    var portalService = scope.ServiceProvider.GetRequiredService<IPortalCommunicationService>();
                    var jobAssignment = await portalService.RequestJobAsync();

                    if (jobAssignment != null)
                    {
                        _logger.LogInformation("Received job assignment: {JobId} - {JobName}", 
                            jobAssignment.JobId, jobAssignment.JobName);

                        // Queue the job for execution
                        var jobExecutor = scope.ServiceProvider.GetRequiredService<IJobExecutionService>();
                        _ = Task.Run(async () => await jobExecutor.ExecuteJobAsync(jobAssignment, stoppingToken), stoppingToken);
                    }
                    else
                    {
                        _logger.LogDebug("No jobs available from portal");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job polling");
            }

            await Task.Delay(TimeSpan.FromSeconds(_portalConfig.JobPollIntervalSeconds), stoppingToken);
        }
    }
}