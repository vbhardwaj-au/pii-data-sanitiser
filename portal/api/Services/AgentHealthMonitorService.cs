using DAL.Repositories;

namespace API.Services;

public class AgentHealthMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentHealthMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public AgentHealthMonitorService(
        IServiceProvider serviceProvider,
        ILogger<AgentHealthMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Health Monitor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var agentManagementService = scope.ServiceProvider.GetRequiredService<IAgentManagementService>();
                var realtimeMonitoringService = scope.ServiceProvider.GetRequiredService<IRealtimeMonitoringService>();
                
                await agentManagementService.MarkOfflineAgentsAsync();
                
                // Broadcast system metrics update
                await realtimeMonitoringService.BroadcastSystemMetrics();
                
                _logger.LogDebug("Agent health check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during agent health monitoring");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Agent Health Monitor Service stopped");
    }
}