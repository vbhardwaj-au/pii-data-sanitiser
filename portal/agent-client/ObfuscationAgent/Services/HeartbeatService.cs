using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfuscationAgent.Configuration;
using ObfuscationAgent.Models;
using Contracts.Models;
using System.Diagnostics;

namespace ObfuscationAgent.Services;

public class HeartbeatService : BackgroundService
{
    private readonly IPortalCommunicationService _portalService;
    private readonly IAgentStateService _agentState;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly PortalConfiguration _portalConfig;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;

    public HeartbeatService(
        IPortalCommunicationService portalService,
        IAgentStateService agentState,
        IOptions<PortalConfiguration> portalConfig,
        ILogger<HeartbeatService> logger)
    {
        _portalService = portalService;
        _agentState = agentState;
        _logger = logger;
        _portalConfig = portalConfig.Value;

        // Initialize performance counters (Windows only)
        if (OperatingSystem.IsWindows())
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize performance counters");
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Initial delay

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var agentId = await _agentState.GetAgentIdAsync();
                
                if (!agentId.HasValue)
                {
                    _logger.LogDebug("Agent not registered yet, skipping heartbeat");
                    await Task.Delay(TimeSpan.FromSeconds(_portalConfig.HeartbeatIntervalSeconds), stoppingToken);
                    continue;
                }

                var stats = await _agentState.GetStatisticsAsync();
                var systemMetrics = GetSystemMetrics();

                var heartbeatRequest = new Models.AgentHeartbeatRequest
                {
                    Status = stats.ActiveJobs > 0 ? AgentStatus.Busy : AgentStatus.Online,
                    ActiveJobs = stats.ActiveJobs,
                    CompletedJobs = stats.CompletedJobs,
                    FailedJobs = stats.FailedJobs,
                    CpuUsage = systemMetrics.CpuUsage,
                    MemoryUsage = systemMetrics.MemoryUsage,
                    DiskSpaceAvailable = systemMetrics.DiskSpaceAvailable,
                    MaxConcurrentJobs = await _agentState.GetAvailableCapacityAsync()
                };

                var response = await _portalService.SendHeartbeatAsync(agentId.Value, heartbeatRequest);

                if (response != null)
                {
                    if (response.RequiresReregistration)
                    {
                        _logger.LogWarning("Portal requires re-registration");
                        await _agentState.ClearCredentialsAsync();
                    }

                    if (response.NewJobs?.Any() == true)
                    {
                        _logger.LogInformation("Received {Count} new job assignments from heartbeat", 
                            response.NewJobs.Count);
                    }
                }
                else
                {
                    _logger.LogWarning("Heartbeat failed - no response from portal");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during heartbeat");
            }

            await Task.Delay(TimeSpan.FromSeconds(_portalConfig.HeartbeatIntervalSeconds), stoppingToken);
        }
    }

    private (decimal CpuUsage, decimal MemoryUsage, long DiskSpaceAvailable) GetSystemMetrics()
    {
        decimal cpuUsage = 0;
        decimal memoryUsage = 0;
        long diskSpace = 0;

        try
        {
            // CPU usage (Windows only with performance counters)
            if (_cpuCounter != null)
            {
                cpuUsage = (decimal)_cpuCounter.NextValue();
            }

            // Memory usage
            if (_memoryCounter != null)
            {
                var availableMB = _memoryCounter.NextValue();
                var totalMB = GC.GetTotalMemory(false) / (1024 * 1024);
                memoryUsage = totalMB > 0 ? (decimal)(100 - (availableMB / totalMB * 100)) : 0;
            }
            else
            {
                // Fallback for non-Windows
                var process = Process.GetCurrentProcess();
                memoryUsage = (decimal)(process.WorkingSet64 / (1024 * 1024));
            }

            // Disk space
            var driveInfo = new DriveInfo(Directory.GetCurrentDirectory());
            diskSpace = driveInfo.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get system metrics");
        }

        return (cpuUsage, memoryUsage, diskSpace);
    }
}