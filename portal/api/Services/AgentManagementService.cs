using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;

namespace API.Services;

public class AgentManagementService : IAgentManagementService
{
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly IJobDistributionService _jobDistributionService;
    private readonly IApiKeyService _apiKeyService;
    private readonly IConfiguration _configuration;
    private readonly IRealtimeMonitoringService _realtimeMonitoringService;
    private readonly ILogger<AgentManagementService> _logger;

    public AgentManagementService(
        IObfuscationAgentRepository agentRepository,
        IObfuscationJobRepository jobRepository,
        IJobDistributionService jobDistributionService,
        IApiKeyService apiKeyService,
        IConfiguration configuration,
        IRealtimeMonitoringService realtimeMonitoringService,
        ILogger<AgentManagementService> logger)
    {
        _agentRepository = agentRepository;
        _jobRepository = jobRepository;
        _jobDistributionService = jobDistributionService;
        _apiKeyService = apiKeyService;
        _configuration = configuration;
        _realtimeMonitoringService = realtimeMonitoringService;
        _logger = logger;
    }

    public async Task<AgentRegistrationResponse> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        try
        {
            var agent = new ObfuscationAgent
            {
                Id = request.AgentId ?? Guid.NewGuid(), // Use provided ID or generate new one
                Name = request.Name,
                HostName = request.HostName,
                IpAddress = request.IpAddress,
                Port = request.Port,
                Version = request.Version,
                Environment = request.Environment,
                Location = request.Location,
                CpuCores = request.CpuCores,
                MemoryMB = request.MemoryMB,
                MaxConcurrentJobs = request.MaxConcurrentJobs,
                Capabilities = request.Capabilities,
                Notes = request.Notes,
                Status = AgentStatus.Online,
                IsEnabled = true,
                Priority = 1
            };

            // Generate API key first
            var apiKey = await GenerateApiKeyForAgentAsync(agent.Id);
            agent.ApiKey = apiKey; // Store API key in agent record
            
            var createdAgent = await _agentRepository.CreateAsync(agent);

            // Also store the API key for authentication
            await _apiKeyService.StoreApiKeyAsync(apiKey, createdAgent.Id, createdAgent.Name);
            
            var response = new AgentRegistrationResponse
            {
                AgentId = createdAgent.Id,
                Name = createdAgent.Name,
                Status = createdAgent.Status,
                RegisteredAt = createdAgent.RegisteredAt,
                ApiKey = apiKey,
                HeartbeatIntervalSeconds = 30,
                PortalBaseUrl = GetPortalBaseUrl(),
                Success = true
            };

            _logger.LogInformation("Registered new agent {AgentName} with ID {AgentId}", 
                agent.Name, createdAgent.Id);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent {AgentName}", request.Name);
            return new AgentRegistrationResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<AgentRegistrationResponse> UpdateAgentRegistrationAsync(AgentRegistrationRequest request)
    {
        try
        {
            if (!request.AgentId.HasValue || request.AgentId.Value == Guid.Empty)
            {
                throw new ArgumentException("AgentId is required for update");
            }

            var existingAgent = await _agentRepository.GetByIdAsync(request.AgentId.Value);
            if (existingAgent == null)
            {
                // Agent doesn't exist, create new one with the provided ID
                return await RegisterAgentAsync(request);
            }

            // Check if agent is already online (prevent multiple instances)
            if (existingAgent.Status == AgentStatus.Online && 
                existingAgent.LastHeartbeat > DateTime.UtcNow.AddMinutes(-2))
            {
                // Agent is already online and active
                _logger.LogWarning("Agent {AgentId} is already online. Preventing duplicate instance.", 
                    existingAgent.Id);
                
                return new AgentRegistrationResponse
                {
                    Success = false,
                    ErrorMessage = $"Agent {existingAgent.Name} is already running. Only one instance allowed."
                };
            }

            // Update existing agent properties
            existingAgent.Name = request.Name;
            existingAgent.HostName = request.HostName;
            existingAgent.IpAddress = request.IpAddress;
            existingAgent.Port = request.Port;
            existingAgent.Version = request.Version;
            existingAgent.Environment = request.Environment;
            existingAgent.Location = request.Location;
            existingAgent.CpuCores = request.CpuCores;
            existingAgent.MemoryMB = request.MemoryMB;
            existingAgent.MaxConcurrentJobs = request.MaxConcurrentJobs;
            existingAgent.Capabilities = request.Capabilities;
            existingAgent.Notes = request.Notes;
            existingAgent.Status = AgentStatus.Online;
            existingAgent.LastHeartbeat = DateTime.UtcNow;

            await _agentRepository.UpdateAsync(existingAgent);

            // Get or regenerate API key
            var apiKey = await _apiKeyService.GetApiKeyForAgentAsync(existingAgent.Id);
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = await GenerateApiKeyForAgentAsync(existingAgent.Id);
                await _apiKeyService.StoreApiKeyAsync(apiKey, existingAgent.Id, existingAgent.Name);
            }

            var response = new AgentRegistrationResponse
            {
                AgentId = existingAgent.Id,
                Name = existingAgent.Name,
                Status = existingAgent.Status,
                RegisteredAt = existingAgent.RegisteredAt,
                ApiKey = apiKey,
                HeartbeatIntervalSeconds = 30,
                PortalBaseUrl = GetPortalBaseUrl(),
                Success = true
            };

            _logger.LogInformation("Updated existing agent {AgentName} with ID {AgentId}", 
                existingAgent.Name, existingAgent.Id);

            // TODO: Notify realtime monitoring about agent status change
            // await _realtimeMonitoringService.NotifyAgentStatusChangeAsync(existingAgent.Id, AgentStatus.Online);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update agent registration for {AgentName}", request.Name);
            return new AgentRegistrationResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<AgentHeartbeatResponse> ProcessHeartbeatAsync(Guid agentId, AgentHeartbeatRequest request)
    {
        try
        {
            // Update agent status and metrics
            await _agentRepository.UpdatePerformanceMetricsAsync(
                agentId, 
                request.CurrentCpuUsage, 
                request.CurrentMemoryUsage, 
                request.ActiveJobs);
            
            await _agentRepository.UpdateStatusAsync(agentId, request.Status);
            
            // Broadcast agent status update in real-time
            await _realtimeMonitoringService.BroadcastAgentStatusUpdate(
                agentId, request.Status, request.CurrentCpuUsage, request.CurrentMemoryUsage);

            // Process job status updates
            foreach (var jobUpdate in request.JobUpdates)
            {
                await ProcessJobStatusUpdate(jobUpdate);
            }

            // Get new job assignments for this agent
            var newJobs = await GetNewJobsForAgent(agentId);

            // Check for cancelled jobs
            var cancelledJobs = await GetCancelledJobsForAgent(agentId);

            var response = new AgentHeartbeatResponse
            {
                Success = true,
                ServerTimestamp = DateTime.UtcNow,
                NewJobs = newJobs,
                CancelledJobs = cancelledJobs,
                Commands = new AgentCommands(), // Add any commands if needed
                Message = $"Heartbeat processed. {newJobs.Count} new jobs assigned."
            };

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process heartbeat for agent {AgentId}", agentId);
            return new AgentHeartbeatResponse
            {
                Success = false,
                Message = "Failed to process heartbeat"
            };
        }
    }

    public async Task<string> GenerateApiKeyForAgentAsync(Guid agentId)
    {
        // Generate a unique API key for the agent
        // In a production system, this would be more sophisticated with proper encryption
        var keyData = $"agent-{agentId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(keyData);
        var base64Key = Convert.ToBase64String(keyBytes);
        return $"pii-agent-{base64Key}";
    }

    public async Task<(bool IsValid, Guid AgentId)> ValidateApiKeyAsync(string apiKey)
    {
        try
        {
            // First try the ApiKeyService (which stores agent keys in memory)
            var userInfo = _apiKeyService.GetUserInfo(apiKey);
            if (userInfo != null && userInfo.IsActive && userInfo.UserType == "Agent")
            {
                if (Guid.TryParse(userInfo.Id, out var agentId))
                {
                    var agent = await _agentRepository.GetByIdAsync(agentId);
                    return (agent != null && agent.IsEnabled, agentId);
                }
            }

            // Fallback to the original parsing method for backward compatibility
            if (!apiKey.StartsWith("pii-agent-"))
                return (false, Guid.Empty);

            var base64Key = apiKey.Substring("pii-agent-".Length);
            var keyBytes = Convert.FromBase64String(base64Key);
            var keyData = System.Text.Encoding.UTF8.GetString(keyBytes);
            
            // Extract agent ID from key data
            // Format: "agent-{guid}-{timestamp}"
            var agentPrefix = "agent-";
            if (keyData.StartsWith(agentPrefix))
            {
                var remaining = keyData.Substring(agentPrefix.Length);
                var lastDashIndex = remaining.LastIndexOf('-');
                if (lastDashIndex > 0)
                {
                    var guidString = remaining.Substring(0, lastDashIndex);
                    if (Guid.TryParse(guidString, out var agentId))
                    {
                        // Verify agent exists and is active
                        var agent = await _agentRepository.GetByIdAsync(agentId);
                        return (agent != null && agent.IsEnabled, agentId);
                    }
                }
            }
            
            return (false, Guid.Empty);
        }
        catch
        {
            return (false, Guid.Empty);
        }
    }

    public async Task MarkOfflineAgentsAsync()
    {
        var offlineThreshold = TimeSpan.FromMinutes(5); // 5 minutes without heartbeat
        var agentsBeforeUpdate = await _agentRepository.GetAllAsync();
        var onlineAgentsBeforeUpdate = agentsBeforeUpdate.Where(a => a.Status != AgentStatus.Offline).ToList();
        
        await _agentRepository.MarkAgentsOfflineAsync(offlineThreshold);
        
        var agentsAfterUpdate = await _agentRepository.GetAllAsync();
        var newlyOfflineAgents = agentsAfterUpdate
            .Where(a => a.Status == AgentStatus.Offline && 
                       onlineAgentsBeforeUpdate.Any(b => b.Id == a.Id && b.Status != AgentStatus.Offline))
            .ToList();
        
        // Send alerts for newly offline agents
        foreach (var agent in newlyOfflineAgents)
        {
            await _realtimeMonitoringService.SendAgentAlert(
                $"Agent '{agent.Name}' has gone offline (last heartbeat: {agent.LastHeartbeat.ToString("HH:mm:ss")})",
                AlertSeverity.Warning);
        }
        
        _logger.LogInformation("Marked offline agents that haven't sent heartbeat for over {Minutes} minutes", 
            offlineThreshold.TotalMinutes);
    }

    private async Task ProcessJobStatusUpdate(JobStatusUpdate update)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(update.JobId);
            if (job == null)
                return;

            // Update job progress
            await _jobRepository.UpdateProgressAsync(
                update.JobId, 
                update.ProgressPercentage, 
                update.ProcessedRows, 
                update.TablesCompleted);
            
            // Broadcast job progress update in real-time
            await _realtimeMonitoringService.BroadcastJobUpdate(update.JobId, update.Status, update.ProgressPercentage);

            // Handle status changes
            switch (update.Status)
            {
                case JobStatus.Running:
                    if (job.Status == JobStatus.Assigned)
                        await _jobRepository.MarkJobAsStartedAsync(update.JobId, DateTime.UtcNow);
                    break;
                
                case JobStatus.Completed:
                    await _jobRepository.MarkJobAsCompletedAsync(update.JobId, DateTime.UtcNow);
                    break;
                
                case JobStatus.Failed:
                    await _jobRepository.MarkJobAsFailedAsync(
                        update.JobId, 
                        update.ErrorMessage ?? "Unknown error", 
                        true);
                    
                    // Send alert for failed job
                    await _realtimeMonitoringService.SendJobAlert(
                        update.JobId,
                        $"Job failed: {update.ErrorMessage ?? "Unknown error"}",
                        AlertSeverity.Error);
                    break;
            }

            _logger.LogDebug("Updated job {JobId} status to {Status} with {Progress}% progress", 
                update.JobId, update.Status, update.ProgressPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process job status update for job {JobId}", update.JobId);
        }
    }

    private async Task<List<JobAssignment>> GetNewJobsForAgent(Guid agentId)
    {
        var assignments = new List<JobAssignment>();
        
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null || !agent.IsEnabled || agent.ActiveJobs >= agent.MaxConcurrentJobs)
                return assignments;

            // Get next assigned job for this agent
            var nextJob = await _jobRepository.GetNextJobForAgentAsync(agentId);
            if (nextJob != null)
            {
                var assignment = new JobAssignment
                {
                    JobId = nextJob.Id,
                    ProductId = nextJob.ProductId,
                    JobName = nextJob.JobName,
                    Description = nextJob.Description,
                    Priority = nextJob.Priority,
                    MappingVersion = nextJob.MappingVersion,
                    MappingSasUrl = nextJob.MappingSasUrl,
                    Configuration = nextJob.Configuration,
                    AssignedAt = DateTime.UtcNow,
                    ProductConnectionString = nextJob.Product?.ConnectionString ?? ""
                };

                assignments.Add(assignment);
            }

            return assignments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get new jobs for agent {AgentId}", agentId);
            return assignments;
        }
    }

    private async Task<List<Guid>> GetCancelledJobsForAgent(Guid agentId)
    {
        var cancelledJobs = new List<Guid>();
        
        try
        {
            var jobs = await _jobRepository.GetByAgentIdAsync(agentId);
            cancelledJobs.AddRange(
                jobs.Where(j => j.Status == JobStatus.Cancelled)
                    .Select(j => j.Id));
            
            return cancelledJobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cancelled jobs for agent {AgentId}", agentId);
            return cancelledJobs;
        }
    }

    private string GetPortalBaseUrl()
    {
        return _configuration["PortalBaseUrl"] ?? "http://localhost:7001";
    }
}