using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using API.Authorization;
using API.Services;
using DAL.Repositories;
using Contracts.Models;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("System Monitoring")]
[RequireRole(Roles.Admin, Roles.ProductAdmin, Roles.Analyst)]
public class MonitoringController : ControllerBase
{
    private readonly IAgentDiscoveryService _agentDiscoveryService;
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IAgentDiscoveryService agentDiscoveryService,
        IObfuscationAgentRepository agentRepository,
        IObfuscationJobRepository jobRepository,
        ILogger<MonitoringController> logger)
    {
        _agentDiscoveryService = agentDiscoveryService;
        _agentRepository = agentRepository;
        _jobRepository = jobRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get system overview with agents and jobs status
    /// </summary>
    [HttpGet("overview")]
    [SwaggerOperation(Summary = "System Overview", Description = "Get overall system health and statistics")]
    [SwaggerResponse(200, "System overview retrieved successfully")]
    public async Task<ActionResult> GetSystemOverview()
    {
        try
        {
            var onlineAgents = await _agentRepository.GetOnlineAgentsAsync();
            var availableAgents = await _agentRepository.GetAvailableAgentsAsync();
            var allAgents = await _agentRepository.GetAllAsync();

            var pendingJobs = await _jobRepository.GetByStatusAsync(JobStatus.Pending);
            var runningJobs = await _jobRepository.GetByStatusAsync(JobStatus.Running);
            var assignedJobs = await _jobRepository.GetByStatusAsync(JobStatus.Assigned);
            var completedJobs = await _jobRepository.GetCompletedJobsAsync(1); // Today
            var failedJobs = await _jobRepository.GetFailedJobsAsync(1); // Today

            var overview = new
            {
                Timestamp = DateTime.UtcNow,
                Agents = new
                {
                    Total = allAgents.Count(),
                    Online = onlineAgents.Count(),
                    Available = availableAgents.Count(),
                    Offline = allAgents.Count(a => a.Status == AgentStatus.Offline),
                    TotalCapacity = onlineAgents.Sum(a => a.MaxConcurrentJobs),
                    UsedCapacity = onlineAgents.Sum(a => a.ActiveJobs),
                    UtilizationPercentage = onlineAgents.Any() ? 
                        (onlineAgents.Sum(a => a.ActiveJobs) * 100.0 / onlineAgents.Sum(a => a.MaxConcurrentJobs)) : 0
                },
                Jobs = new
                {
                    Pending = pendingJobs.Count(),
                    Assigned = assignedJobs.Count(),
                    Running = runningJobs.Count(),
                    CompletedToday = completedJobs.Count(),
                    FailedToday = failedJobs.Count(),
                    QueueLength = await _jobRepository.GetQueueLengthAsync(),
                    AverageJobDuration = completedJobs.Any() ? 
                        completedJobs.Where(j => j.Duration.HasValue)
                                   .Average(j => j.Duration!.Value.TotalMinutes) : 0
                },
                Performance = new
                {
                    AverageCpuUsage = onlineAgents.Any() ? onlineAgents.Average(a => a.CurrentCpuUsage) : 0,
                    AverageMemoryUsage = onlineAgents.Any() ? onlineAgents.Average(a => a.CurrentMemoryUsage) : 0,
                    TotalRowsProcessedToday = completedJobs.Sum(j => j.ProcessedRows),
                    AverageRowsPerSecond = completedJobs.Any() ?
                        completedJobs.Where(j => j.RowsPerSecond.HasValue)
                                   .Average(j => j.RowsPerSecond!.Value) : 0
                },
                Health = new
                {
                    SystemHealthy = onlineAgents.Any() && pendingJobs.Count() < 100, // Basic health check
                    Issues = GetHealthIssues(onlineAgents, availableAgents, pendingJobs.Count())
                }
            };

            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system overview");
            return StatusCode(500, new { error = "Failed to get system overview" });
        }
    }

    /// <summary>
    /// Get detailed health status for all agents
    /// </summary>
    [HttpGet("agents/health")]
    [SwaggerOperation(Summary = "Agent Health Status", Description = "Get health status for all agents")]
    [SwaggerResponse(200, "Agent health status retrieved successfully")]
    public async Task<ActionResult> GetAgentsHealthStatus()
    {
        try
        {
            var agents = await _agentRepository.GetAllAsync();
            var healthStatuses = new List<object>();

            foreach (var agent in agents)
            {
                var healthStatus = await _agentDiscoveryService.GetAgentHealthStatusAsync(agent.Id);
                healthStatuses.Add(new
                {
                    AgentId = agent.Id,
                    AgentName = agent.Name,
                    HostName = agent.HostName,
                    IpAddress = agent.IpAddress,
                    Status = healthStatus.Status,
                    IsHealthy = healthStatus.IsHealthy,
                    LastHeartbeat = healthStatus.LastHeartbeat,
                    TimeSinceHeartbeat = healthStatus.TimeSinceHeartbeat,
                    CpuUsage = healthStatus.CpuUsage,
                    MemoryUsage = healthStatus.MemoryUsage,
                    ActiveJobs = healthStatus.ActiveJobs,
                    MaxJobs = healthStatus.MaxJobs,
                    Utilization = healthStatus.MaxJobs > 0 ? 
                        (healthStatus.ActiveJobs * 100.0 / healthStatus.MaxJobs) : 0,
                    Message = healthStatus.Message,
                    Performance = new
                    {
                        CompletedJobs = agent.CompletedJobs,
                        FailedJobs = agent.FailedJobs,
                        // TODO: Add AverageJobDuration to ObfuscationAgent model
                        LastJobCompleted = agent.LastJobCompleted
                    }
                });
            }

            return Ok(new
            {
                Timestamp = DateTime.UtcNow,
                TotalAgents = agents.Count(),
                HealthyAgents = healthStatuses.Count(h => (bool)h.GetType().GetProperty("IsHealthy")!.GetValue(h)!),
                Agents = healthStatuses.OrderBy(h => h.GetType().GetProperty("AgentName")!.GetValue(h))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agents health status");
            return StatusCode(500, new { error = "Failed to get agents health status" });
        }
    }

    /// <summary>
    /// Get health status for a specific agent
    /// </summary>
    [HttpGet("agents/{agentId}/health")]
    [SwaggerOperation(Summary = "Specific Agent Health", Description = "Get detailed health status for a specific agent")]
    [SwaggerResponse(200, "Agent health status retrieved successfully")]
    [SwaggerResponse(404, "Agent not found")]
    public async Task<ActionResult> GetAgentHealthStatus(Guid agentId)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            var healthStatus = await _agentDiscoveryService.GetAgentHealthStatusAsync(agentId);
            
            return Ok(new
            {
                Agent = new
                {
                    agent.Id,
                    agent.Name,
                    agent.HostName,
                    agent.IpAddress,
                    agent.Port,
                    agent.Version,
                    agent.Environment,
                    agent.Location,
                    agent.Capabilities
                },
                Health = healthStatus,
                Performance = new
                {
                    agent.CompletedJobs,
                    agent.FailedJobs,
                    // TODO: Add AverageJobDuration to ObfuscationAgent model
                    agent.LastJobCompleted,
                    SuccessRate = agent.CompletedJobs + agent.FailedJobs > 0 ?
                        (agent.CompletedJobs * 100.0 / (agent.CompletedJobs + agent.FailedJobs)) : 0
                },
                Resources = new
                {
                    agent.CpuCores,
                    agent.MemoryMB,
                    agent.CurrentCpuUsage,
                    agent.CurrentMemoryUsage,
                    agent.ActiveJobs,
                    agent.MaxConcurrentJobs
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health status for agent {AgentId}", agentId);
            return StatusCode(500, new { error = "Failed to get agent health status" });
        }
    }

    /// <summary>
    /// Get performance metrics over time
    /// </summary>
    [HttpGet("performance")]
    [SwaggerOperation(Summary = "Performance Metrics", Description = "Get system performance metrics")]
    [SwaggerResponse(200, "Performance metrics retrieved successfully")]
    public async Task<ActionResult> GetPerformanceMetrics([FromQuery] int hours = 24)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-hours);
            
            var completedJobs = await _jobRepository.GetCompletedJobsAsync(hours / 24);
            var failedJobs = await _jobRepository.GetFailedJobsAsync(hours / 24);

            var metrics = new
            {
                TimeRange = $"Last {hours} hours",
                StartTime = cutoffTime,
                EndTime = DateTime.UtcNow,
                Jobs = new
                {
                    Completed = completedJobs.Count(),
                    Failed = failedJobs.Count(),
                    SuccessRate = completedJobs.Count() + failedJobs.Count() > 0 ?
                        (completedJobs.Count() * 100.0 / (completedJobs.Count() + failedJobs.Count())) : 0,
                    AverageDuration = completedJobs.Where(j => j.Duration.HasValue)
                                               .Select(j => j.Duration!.Value.TotalMinutes)
                                               .DefaultIfEmpty(0)
                                               .Average(),
                    TotalRowsProcessed = completedJobs.Sum(j => j.ProcessedRows),
                    AverageRowsPerSecond = completedJobs.Where(j => j.RowsPerSecond.HasValue)
                                                     .Select(j => j.RowsPerSecond!.Value)
                                                     .DefaultIfEmpty(0)
                                                     .Average()
                },
                Agents = new
                {
                    TotalActive = (await _agentRepository.GetOnlineAgentsAsync()).Count(),
                    AverageCpuUsage = (await _agentRepository.GetOnlineAgentsAsync())
                        .Select(a => a.CurrentCpuUsage)
                        .DefaultIfEmpty(0)
                        .Average(),
                    AverageMemoryUsage = (await _agentRepository.GetOnlineAgentsAsync())
                        .Select(a => a.CurrentMemoryUsage)
                        .DefaultIfEmpty(0)
                        .Average()
                }
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get performance metrics");
            return StatusCode(500, new { error = "Failed to get performance metrics" });
        }
    }

    private List<string> GetHealthIssues(IEnumerable<ObfuscationAgent> onlineAgents, 
                                        IEnumerable<ObfuscationAgent> availableAgents, 
                                        int pendingJobsCount)
    {
        var issues = new List<string>();

        if (!onlineAgents.Any())
            issues.Add("No agents are online");
        
        if (!availableAgents.Any() && onlineAgents.Any())
            issues.Add("All online agents are at capacity");
        
        if (pendingJobsCount > 50)
            issues.Add($"High number of pending jobs ({pendingJobsCount})");
        
        var highCpuAgents = onlineAgents.Where(a => a.CurrentCpuUsage > 90).Count();
        if (highCpuAgents > 0)
            issues.Add($"{highCpuAgents} agents have high CPU usage (>90%)");
        
        var highMemoryAgents = onlineAgents.Where(a => a.CurrentMemoryUsage > 90).Count();
        if (highMemoryAgents > 0)
            issues.Add($"{highMemoryAgents} agents have high memory usage (>90%)");

        return issues;
    }
}