using DAL.Repositories;
using Contracts.Models;

namespace API.Services;

public class AgentDiscoveryService : IAgentDiscoveryService
{
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly ILogger<AgentDiscoveryService> _logger;

    public AgentDiscoveryService(
        IObfuscationAgentRepository agentRepository,
        ILogger<AgentDiscoveryService> logger)
    {
        _agentRepository = agentRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ObfuscationAgent>> DiscoverAvailableAgentsAsync()
    {
        try
        {
            var agents = await _agentRepository.GetAvailableAgentsAsync();
            
            _logger.LogDebug("Discovered {AgentCount} available agents", agents.Count());
            
            return agents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover available agents");
            return Enumerable.Empty<ObfuscationAgent>();
        }
    }

    public async Task<IEnumerable<ObfuscationAgent>> GetAgentsByCapabilityAsync(AgentCapabilities requiredCapabilities)
    {
        try
        {
            var allAgents = await _agentRepository.GetOnlineAgentsAsync();
            
            var compatibleAgents = allAgents.Where(agent =>
                agent.IsEnabled &&
                agent.ActiveJobs < agent.MaxConcurrentJobs);

            _logger.LogDebug("Found {AgentCount} agents with required capabilities", compatibleAgents.Count());
            
            return compatibleAgents.OrderBy(a => a.ActiveJobs)
                                  .ThenByDescending(a => a.Priority);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agents by capability");
            return Enumerable.Empty<ObfuscationAgent>();
        }
    }

    public async Task<ObfuscationAgent?> FindBestAgentForJobAsync(JobPriority jobPriority, AgentCapabilities? requiredCapabilities = null, string? requiredLocation = null)
    {
        try
        {
            var availableAgents = await GetAgentsByCapabilityAsync(requiredCapabilities ?? new AgentCapabilities());
            
            // Filter by location if specified
            if (!string.IsNullOrEmpty(requiredLocation))
            {
                availableAgents = availableAgents.Where(a => a.Location == requiredLocation);
                _logger.LogDebug("Filtered agents to location: {Location}, found {Count} agents", 
                    requiredLocation, availableAgents.Count());
            }
            
            if (!availableAgents.Any())
            {
                _logger.LogWarning("No available agents found for job with priority {Priority} and location {Location}", 
                    jobPriority, requiredLocation ?? "any");
                return null;
            }

            // Score agents based on various factors
            var scoredAgents = availableAgents.Select(agent => new
            {
                Agent = agent,
                Score = CalculateAgentScore(agent, jobPriority)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

            var bestAgent = scoredAgents.First().Agent;
            
            _logger.LogDebug("Selected agent {AgentName} (score: {Score}) for job with priority {Priority}",
                bestAgent.Name, scoredAgents.First().Score, jobPriority);

            return bestAgent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find best agent for job");
            return null;
        }
    }

    public async Task<AgentHealthStatus> GetAgentHealthStatusAsync(Guid agentId)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
            {
                return new AgentHealthStatus
                {
                    AgentId = agentId,
                    Status = AgentStatus.Offline,
                    IsHealthy = false,
                    Message = "Agent not found"
                };
            }

            var lastHeartbeat = agent.LastHeartbeat;
            var timeSinceHeartbeat = DateTime.UtcNow - lastHeartbeat;
            var isHealthy = agent.IsEnabled && 
                          agent.Status == AgentStatus.Online && 
                          timeSinceHeartbeat < TimeSpan.FromMinutes(5);

            return new AgentHealthStatus
            {
                AgentId = agentId,
                AgentName = agent.Name,
                Status = agent.Status,
                IsHealthy = isHealthy,
                LastHeartbeat = lastHeartbeat,
                TimeSinceHeartbeat = timeSinceHeartbeat,
                CpuUsage = agent.CurrentCpuUsage,
                MemoryUsage = agent.CurrentMemoryUsage,
                ActiveJobs = agent.ActiveJobs,
                MaxJobs = agent.MaxConcurrentJobs,
                Message = isHealthy ? "Healthy" : GetHealthIssueMessage(agent, timeSinceHeartbeat)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health status for agent {AgentId}", agentId);
            return new AgentHealthStatus
            {
                AgentId = agentId,
                Status = AgentStatus.Offline,
                IsHealthy = false,
                Message = "Error checking health status"
            };
        }
    }

    private bool HasRequiredCapabilities(ObfuscationAgent agent, AgentCapabilities requiredCapabilities)
    {
        // For now, just return true since capabilities are class-based, not flag-based
        return true;
    }

    private double CalculateAgentScore(ObfuscationAgent agent, JobPriority jobPriority)
    {
        double score = 0;

        // Base score from agent priority
        score += (int)agent.Priority * 10;

        // Capacity factor (prefer agents with more available slots)
        var availableSlots = agent.MaxConcurrentJobs - agent.ActiveJobs;
        score += availableSlots * 5;

        // Performance factor
        score += (100 - agent.CurrentCpuUsage) * 0.1;
        score += (100 - agent.CurrentMemoryUsage) * 0.1;

        // Job priority boost
        if (jobPriority == JobPriority.Critical)
            score += 20;
        else if (jobPriority == JobPriority.High)
            score += 10;

        // Recent performance factor - removed as AverageJobDuration doesn't exist yet
        // TODO: Add AverageJobDuration to ObfuscationAgent model

        return score;
    }

    private string GetHealthIssueMessage(ObfuscationAgent agent, TimeSpan timeSinceHeartbeat)
    {
        if (!agent.IsEnabled)
            return "Agent is disabled";
        
        if (agent.Status == AgentStatus.Offline)
            return "Agent is offline";
        
        if (timeSinceHeartbeat > TimeSpan.FromMinutes(5))
            return $"No heartbeat for {timeSinceHeartbeat.TotalMinutes:F1} minutes";
        
        if (agent.CurrentCpuUsage > 90)
            return "High CPU usage";
        
        if (agent.CurrentMemoryUsage > 90)
            return "High memory usage";
        
        return "Unknown health issue";
    }
}

public interface IAgentDiscoveryService
{
    Task<IEnumerable<ObfuscationAgent>> DiscoverAvailableAgentsAsync();
    Task<IEnumerable<ObfuscationAgent>> GetAgentsByCapabilityAsync(AgentCapabilities requiredCapabilities);
    Task<ObfuscationAgent?> FindBestAgentForJobAsync(JobPriority jobPriority, AgentCapabilities? requiredCapabilities = null, string? requiredLocation = null);
    Task<AgentHealthStatus> GetAgentHealthStatusAsync(Guid agentId);
}

public class AgentHealthStatus
{
    public Guid AgentId { get; set; }
    public string? AgentName { get; set; }
    public AgentStatus Status { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public TimeSpan TimeSinceHeartbeat { get; set; }
    public decimal CpuUsage { get; set; }
    public decimal MemoryUsage { get; set; }
    public int ActiveJobs { get; set; }
    public int MaxJobs { get; set; }
    public string Message { get; set; } = string.Empty;
}