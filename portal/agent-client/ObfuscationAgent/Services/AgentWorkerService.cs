using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfuscationAgent.Configuration;
using ObfuscationAgent.Models;
using System.Net;
using System.Net.Sockets;

namespace ObfuscationAgent.Services;

public class AgentWorkerService : BackgroundService
{
    private readonly IPortalCommunicationService _portalService;
    private readonly IAgentStateService _agentState;
    private readonly ILogger<AgentWorkerService> _logger;
    private readonly AgentConfiguration _agentConfig;

    public AgentWorkerService(
        IPortalCommunicationService portalService,
        IAgentStateService agentState,
        IOptions<AgentConfiguration> agentConfig,
        ILogger<AgentWorkerService> logger)
    {
        _portalService = portalService;
        _agentState = agentState;
        _logger = logger;
        _agentConfig = agentConfig.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Worker Service started");

        // Always attempt registration to update agent status
        await RegisterAgentAsync(stoppingToken);

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task RegisterAgentAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Priority order for Agent ID:
            // 1. Environment variable (for cloud deployments)
            // 2. Stored ID from previous registration
            // 3. Generate new ID
            
            Guid? agentId = null;
            
            // Check if agent ID is provided via environment/config
            if (_agentConfig.AgentId.HasValue && _agentConfig.AgentId.Value != Guid.Empty)
            {
                agentId = _agentConfig.AgentId.Value;
                _logger.LogInformation("Using agent ID from configuration: {AgentId}", agentId);
            }
            else
            {
                // Check if agent has a stored ID from previous registration
                var existingAgentId = await _agentState.GetAgentIdAsync();
                if (existingAgentId.HasValue)
                {
                    agentId = existingAgentId;
                    _logger.LogInformation("Using existing stored agent ID: {AgentId}", agentId);
                }
            }

            // Get local IP address
            var ipAddress = GetLocalIPAddress();
            if (!string.IsNullOrEmpty(ipAddress))
            {
                _agentConfig.IpAddress = ipAddress;
            }

            var registrationRequest = new Models.AgentRegistrationRequest
            {
                AgentId = agentId, // Include ID if available
                Name = _agentConfig.Name,
                HostName = _agentConfig.HostName,
                IpAddress = _agentConfig.IpAddress,
                Port = _agentConfig.Port,
                Version = _agentConfig.Version,
                Environment = _agentConfig.Environment,
                Location = _agentConfig.Location,
                CpuCores = _agentConfig.CpuCores,
                MemoryMB = _agentConfig.MemoryMB,
                MaxConcurrentJobs = _agentConfig.MaxConcurrentJobs,
                Priority = _agentConfig.Priority,
                Capabilities = new Contracts.Models.AgentCapabilities
                {
                    CanProcessSqlServer = _agentConfig.Capabilities.CanProcessSqlServer,
                    CanProcessMySQL = _agentConfig.Capabilities.CanProcessMySQL,
                    CanProcessPostgreSQL = _agentConfig.Capabilities.CanProcessPostgreSQL,
                    CanProcessOracle = _agentConfig.Capabilities.CanProcessOracle,
                    MaxDatabaseSizeGB = _agentConfig.Capabilities.MaxDatabaseSizeGB,
                    MaxRowsPerBatch = _agentConfig.Capabilities.MaxRowsPerBatch,
                    SupportedObfuscationTypes = _agentConfig.Capabilities.SupportedObfuscationTypes
                }
            };

            _logger.LogInformation("Registering agent with portal...");
            _logger.LogInformation("Agent Name: {Name}, Host: {Host}, Location: {Location}", 
                registrationRequest.Name, registrationRequest.HostName, registrationRequest.Location);

            var response = await _portalService.RegisterAgentAsync(registrationRequest);

            if (response != null && response.Success)
            {
                _logger.LogInformation("Agent registered successfully with ID: {AgentId}", response.AgentId);
                
                // Store the credentials
                await _agentState.SetAgentIdAsync(response.AgentId);
                await _agentState.SetApiKeyAsync(response.ApiKey);
                
                _logger.LogInformation("Agent credentials stored securely");
            }
            else
            {
                var message = response?.Message ?? "Unknown error";
                _logger.LogError("Failed to register agent: {Message}", message);
                
                // Retry after delay
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    await RegisterAgentAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during agent registration");
            
            // Retry after delay
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await RegisterAgentAsync(cancellationToken);
            }
        }
    }

    private string? GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get local IP address");
        }
        
        return null;
    }
}