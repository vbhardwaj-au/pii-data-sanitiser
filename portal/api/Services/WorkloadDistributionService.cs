using Contracts.Models;
using DAL.Repositories;

namespace API.Services;

public class WorkloadDistributionService : IWorkloadDistributionService
{
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly IAgentDiscoveryService _agentDiscoveryService;
    private readonly ILogger<WorkloadDistributionService> _logger;

    public WorkloadDistributionService(
        IObfuscationJobRepository jobRepository,
        IObfuscationAgentRepository agentRepository,
        IAgentDiscoveryService agentDiscoveryService,
        ILogger<WorkloadDistributionService> logger)
    {
        _jobRepository = jobRepository;
        _agentRepository = agentRepository;
        _agentDiscoveryService = agentDiscoveryService;
        _logger = logger;
    }

    public async Task<WorkloadDistributionResult> DistributeWorkloadAsync(DistributionStrategy strategy = DistributionStrategy.OptimalPerformance)
    {
        var result = new WorkloadDistributionResult
        {
            Strategy = strategy,
            StartTime = DateTime.UtcNow
        };

        try
        {
            var pendingJobs = (await _jobRepository.GetPendingJobsAsync()).ToList();
            var availableAgents = (await _agentRepository.GetAvailableAgentsAsync()).ToList();

            result.PendingJobs = pendingJobs.Count;
            result.AvailableAgents = availableAgents.Count;

            if (!availableAgents.Any())
            {
                result.Messages.Add("No available agents for workload distribution");
                return result;
            }

            if (!pendingJobs.Any())
            {
                result.Messages.Add("No pending jobs to distribute");
                return result;
            }

            result = strategy switch
            {
                DistributionStrategy.RoundRobin => await DistributeRoundRobinAsync(pendingJobs, availableAgents, result),
                DistributionStrategy.LoadBalanced => await DistributeLoadBalancedAsync(pendingJobs, availableAgents, result),
                DistributionStrategy.PriorityBased => await DistributePriorityBasedAsync(pendingJobs, availableAgents, result),
                DistributionStrategy.OptimalPerformance => await DistributeOptimalAsync(pendingJobs, availableAgents, result),
                DistributionStrategy.Geographic => await DistributeGeographicAsync(pendingJobs, availableAgents, result),
                _ => await DistributeOptimalAsync(pendingJobs, availableAgents, result)
            };

            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.Success = true;

            _logger.LogInformation("Workload distribution completed using {Strategy}: {AssignedJobs} jobs assigned to {AgentCount} agents in {Duration}ms",
                strategy, result.AssignedJobs, result.AgentJobCounts.Count, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to distribute workload using strategy {Strategy}", strategy);
            result.Messages.Add($"Distribution failed: {ex.Message}");
            result.Success = false;
            return result;
        }
    }

    private async Task<WorkloadDistributionResult> DistributeRoundRobinAsync(
        List<ObfuscationJob> pendingJobs, 
        List<ObfuscationAgent> availableAgents, 
        WorkloadDistributionResult result)
    {
        var agentIndex = 0;
        
        foreach (var job in pendingJobs)
        {
            var agent = availableAgents[agentIndex % availableAgents.Count];
            
            if (agent.ActiveJobs < agent.MaxConcurrentJobs)
            {
                await _jobRepository.AssignJobToAgentAsync(job.Id, agent.Id);
                result.AssignedJobs++;
                
                if (result.AgentJobCounts.ContainsKey(agent.Id))
                    result.AgentJobCounts[agent.Id]++;
                else
                    result.AgentJobCounts[agent.Id] = 1;

                agent.ActiveJobs++; // Update local count
                
                _logger.LogDebug("Round-robin: Assigned job {JobId} to agent {AgentName}", job.Id, agent.Name);
            }
            else
            {
                result.FailedAssignments++;
                _logger.LogWarning("Round-robin: Agent {AgentName} at capacity, skipping job {JobId}", agent.Name, job.Id);
            }
            
            agentIndex++;
        }

        result.Messages.Add($"Round-robin distribution: {result.AssignedJobs} jobs assigned");
        return result;
    }

    private async Task<WorkloadDistributionResult> DistributeLoadBalancedAsync(
        List<ObfuscationJob> pendingJobs, 
        List<ObfuscationAgent> availableAgents, 
        WorkloadDistributionResult result)
    {
        // Sort agents by available capacity (most available first)
        var sortedAgents = availableAgents
            .OrderBy(a => a.ActiveJobs)
            .ThenByDescending(a => a.MaxConcurrentJobs - a.ActiveJobs)
            .ToList();

        foreach (var job in pendingJobs)
        {
            // Find agent with most available capacity
            var bestAgent = sortedAgents.FirstOrDefault(a => a.ActiveJobs < a.MaxConcurrentJobs);
            
            if (bestAgent != null)
            {
                await _jobRepository.AssignJobToAgentAsync(job.Id, bestAgent.Id);
                result.AssignedJobs++;
                
                if (result.AgentJobCounts.ContainsKey(bestAgent.Id))
                    result.AgentJobCounts[bestAgent.Id]++;
                else
                    result.AgentJobCounts[bestAgent.Id] = 1;

                bestAgent.ActiveJobs++; // Update local count
                
                // Re-sort agents to maintain load balance
                sortedAgents = sortedAgents
                    .OrderBy(a => a.ActiveJobs)
                    .ThenByDescending(a => a.MaxConcurrentJobs - a.ActiveJobs)
                    .ToList();
                
                _logger.LogDebug("Load-balanced: Assigned job {JobId} to agent {AgentName} (load: {ActiveJobs}/{MaxJobs})",
                    job.Id, bestAgent.Name, bestAgent.ActiveJobs, bestAgent.MaxConcurrentJobs);
            }
            else
            {
                result.FailedAssignments++;
                break; // All agents at capacity
            }
        }

        result.Messages.Add($"Load-balanced distribution: {result.AssignedJobs} jobs assigned");
        return result;
    }

    private async Task<WorkloadDistributionResult> DistributePriorityBasedAsync(
        List<ObfuscationJob> pendingJobs, 
        List<ObfuscationAgent> availableAgents, 
        WorkloadDistributionResult result)
    {
        // Sort jobs by priority (highest first)
        var priorityJobs = pendingJobs.OrderByDescending(j => j.Priority).ToList();
        
        // Sort agents by priority and availability
        var priorityAgents = availableAgents
            .Where(a => a.ActiveJobs < a.MaxConcurrentJobs)
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.ActiveJobs)
            .ToList();

        foreach (var job in priorityJobs)
        {
            ObfuscationAgent? selectedAgent = null;

            // For critical and high priority jobs, prefer high priority agents
            if (job.Priority >= JobPriority.High)
            {
                selectedAgent = priorityAgents
                    .Where(a => a.Priority >= 2 && a.ActiveJobs < a.MaxConcurrentJobs) // High priority agents
                    .OrderBy(a => a.ActiveJobs)
                    .FirstOrDefault();
            }

            // If no high priority agent available, use any available agent
            selectedAgent ??= priorityAgents
                .Where(a => a.ActiveJobs < a.MaxConcurrentJobs)
                .OrderBy(a => a.ActiveJobs)
                .FirstOrDefault();

            if (selectedAgent != null)
            {
                await _jobRepository.AssignJobToAgentAsync(job.Id, selectedAgent.Id);
                result.AssignedJobs++;
                
                if (result.AgentJobCounts.ContainsKey(selectedAgent.Id))
                    result.AgentJobCounts[selectedAgent.Id]++;
                else
                    result.AgentJobCounts[selectedAgent.Id] = 1;

                selectedAgent.ActiveJobs++; // Update local count
                
                _logger.LogDebug("Priority-based: Assigned {Priority} priority job {JobId} to priority-{AgentPriority} agent {AgentName}",
                    job.Priority, job.Id, selectedAgent.Priority, selectedAgent.Name);
            }
            else
            {
                result.FailedAssignments++;
                _logger.LogWarning("Priority-based: No suitable agent available for {Priority} priority job {JobId}",
                    job.Priority, job.Id);
            }
        }

        result.Messages.Add($"Priority-based distribution: {result.AssignedJobs} jobs assigned");
        return result;
    }

    private async Task<WorkloadDistributionResult> DistributeOptimalAsync(
        List<ObfuscationJob> pendingJobs, 
        List<ObfuscationAgent> availableAgents, 
        WorkloadDistributionResult result)
    {
        // Sort jobs by priority and estimated size
        var sortedJobs = pendingJobs
            .OrderByDescending(j => j.Priority)
            .ThenByDescending(j => j.TotalRows) // Larger jobs first for better packing
            .ToList();

        foreach (var job in sortedJobs)
        {
            // Use the AgentDiscoveryService to find the best agent for this job
            var bestAgent = await _agentDiscoveryService.FindBestAgentForJobAsync(job.Priority);
            
            if (bestAgent != null && availableAgents.Contains(bestAgent) && bestAgent.ActiveJobs < bestAgent.MaxConcurrentJobs)
            {
                await _jobRepository.AssignJobToAgentAsync(job.Id, bestAgent.Id);
                result.AssignedJobs++;
                
                if (result.AgentJobCounts.ContainsKey(bestAgent.Id))
                    result.AgentJobCounts[bestAgent.Id]++;
                else
                    result.AgentJobCounts[bestAgent.Id] = 1;

                bestAgent.ActiveJobs++; // Update local count
                
                // Remove agent from available list if at capacity
                if (bestAgent.ActiveJobs >= bestAgent.MaxConcurrentJobs)
                {
                    availableAgents.Remove(bestAgent);
                }
                
                _logger.LogDebug("Optimal: Assigned job {JobId} (priority: {Priority}, rows: {Rows}) to agent {AgentName} (score-based)",
                    job.Id, job.Priority, job.TotalRows, bestAgent.Name);
            }
            else
            {
                result.FailedAssignments++;
                _logger.LogWarning("Optimal: No suitable agent found for job {JobId}", job.Id);
            }
        }

        result.Messages.Add($"Optimal distribution: {result.AssignedJobs} jobs assigned using performance scoring");
        return result;
    }

    private async Task<WorkloadDistributionResult> DistributeGeographicAsync(
        List<ObfuscationJob> pendingJobs, 
        List<ObfuscationAgent> availableAgents, 
        WorkloadDistributionResult result)
    {
        // Group agents by location
        var agentGroups = availableAgents
            .Where(a => a.ActiveJobs < a.MaxConcurrentJobs)
            .GroupBy(a => a.Location)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.ActiveJobs).ToList());

        // Prefer on-premises agents for high-priority jobs, cloud agents for others
        foreach (var job in pendingJobs)
        {
            ObfuscationAgent? selectedAgent = null;

            // High priority jobs prefer on-premises agents
            if (job.Priority >= JobPriority.High && agentGroups.ContainsKey("OnPremises"))
            {
                selectedAgent = agentGroups["OnPremises"]
                    .FirstOrDefault(a => a.ActiveJobs < a.MaxConcurrentJobs);
                    
                if (selectedAgent != null)
                {
                    _logger.LogDebug("Geographic: Assigned high-priority job {JobId} to on-premises agent {AgentName}",
                        job.Id, selectedAgent.Name);
                }
            }

            // If no on-premises agent available, or for normal priority jobs, use cloud agents
            if (selectedAgent == null)
            {
                // Try Azure agents first, then AWS, then any available
                var locationPreferences = new[] { "Azure", "AWS", "OnPremises" };
                
                foreach (var location in locationPreferences)
                {
                    if (agentGroups.ContainsKey(location))
                    {
                        selectedAgent = agentGroups[location]
                            .FirstOrDefault(a => a.ActiveJobs < a.MaxConcurrentJobs);
                        if (selectedAgent != null) break;
                    }
                }

                // Fallback to any available agent regardless of location
                selectedAgent ??= availableAgents
                    .FirstOrDefault(a => a.ActiveJobs < a.MaxConcurrentJobs);
            }

            if (selectedAgent != null)
            {
                await _jobRepository.AssignJobToAgentAsync(job.Id, selectedAgent.Id);
                result.AssignedJobs++;
                
                if (result.AgentJobCounts.ContainsKey(selectedAgent.Id))
                    result.AgentJobCounts[selectedAgent.Id]++;
                else
                    result.AgentJobCounts[selectedAgent.Id] = 1;

                selectedAgent.ActiveJobs++; // Update local count
                
                // Update agent group
                if (agentGroups.ContainsKey(selectedAgent.Location))
                {
                    agentGroups[selectedAgent.Location] = agentGroups[selectedAgent.Location]
                        .OrderBy(a => a.ActiveJobs)
                        .ToList();
                }
                
                _logger.LogDebug("Geographic: Assigned job {JobId} to {Location} agent {AgentName}",
                    job.Id, selectedAgent.Location, selectedAgent.Name);
            }
            else
            {
                result.FailedAssignments++;
                _logger.LogWarning("Geographic: No suitable agent available in any location for job {JobId}", job.Id);
            }
        }

        result.Messages.Add($"Geographic distribution: {result.AssignedJobs} jobs assigned with location preferences");
        return result;
    }

    public async Task<WorkloadRebalanceResult> RebalanceWorkloadAsync()
    {
        var result = new WorkloadRebalanceResult
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            var onlineAgents = (await _agentRepository.GetOnlineAgentsAsync()).ToList();
            if (onlineAgents.Count < 2)
            {
                result.Messages.Add("Not enough agents online for rebalancing");
                return result;
            }

            // Calculate workload statistics
            var totalCapacity = onlineAgents.Sum(a => a.MaxConcurrentJobs);
            var totalActiveJobs = onlineAgents.Sum(a => a.ActiveJobs);
            var averageLoad = totalActiveJobs / (double)onlineAgents.Count;
            var targetLoad = totalActiveJobs / (double)totalCapacity;

            result.AverageLoad = averageLoad;
            result.TargetUtilization = targetLoad;

            // Find overloaded and underloaded agents
            var overloadedAgents = onlineAgents
                .Where(a => a.ActiveJobs > averageLoad + 1)
                .OrderByDescending(a => a.ActiveJobs)
                .ToList();

            var underloadedAgents = onlineAgents
                .Where(a => a.ActiveJobs < a.MaxConcurrentJobs && a.ActiveJobs < averageLoad - 1)
                .OrderBy(a => a.ActiveJobs)
                .ToList();

            result.OverloadedAgents = overloadedAgents.Count;
            result.UnderloadedAgents = underloadedAgents.Count;

            if (!overloadedAgents.Any() || !underloadedAgents.Any())
            {
                result.Messages.Add("Workload already balanced");
                result.Success = true;
                return result;
            }

            // Move jobs from overloaded to underloaded agents
            foreach (var overloadedAgent in overloadedAgents)
            {
                if (!underloadedAgents.Any())
                    break;

                var jobsToMove = await _jobRepository.GetByAgentIdAsync(overloadedAgent.Id);
                var movableJobs = jobsToMove
                    .Where(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned)
                    .Take((int)(overloadedAgent.ActiveJobs - averageLoad))
                    .ToList();

                foreach (var job in movableJobs)
                {
                    var targetAgent = underloadedAgents.FirstOrDefault(a => a.ActiveJobs < a.MaxConcurrentJobs);
                    if (targetAgent == null)
                        break;

                    // Move the job
                    await _jobRepository.AssignJobToAgentAsync(job.Id, targetAgent.Id);
                    result.JobsMoved++;

                    // Update local counts
                    overloadedAgent.ActiveJobs--;
                    targetAgent.ActiveJobs++;

                    // Remove target agent if now at capacity
                    if (targetAgent.ActiveJobs >= targetAgent.MaxConcurrentJobs)
                    {
                        underloadedAgents.Remove(targetAgent);
                    }

                    _logger.LogDebug("Rebalanced: Moved job {JobId} from agent {FromAgent} to agent {ToAgent}",
                        job.Id, overloadedAgent.Name, targetAgent.Name);
                }
            }

            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.Success = true;

            _logger.LogInformation("Workload rebalancing completed: {JobsMoved} jobs moved between {OverloadedCount} overloaded and {UnderloadedCount} underloaded agents",
                result.JobsMoved, overloadedAgents.Count, underloadedAgents.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebalance workload");
            result.Messages.Add($"Rebalancing failed: {ex.Message}");
            result.Success = false;
            return result;
        }
    }
}

public interface IWorkloadDistributionService
{
    Task<WorkloadDistributionResult> DistributeWorkloadAsync(DistributionStrategy strategy = DistributionStrategy.OptimalPerformance);
    Task<WorkloadRebalanceResult> RebalanceWorkloadAsync();
}

public enum DistributionStrategy
{
    RoundRobin = 0,
    LoadBalanced = 1,
    PriorityBased = 2,
    OptimalPerformance = 3,
    Geographic = 4
}

public class WorkloadDistributionResult
{
    public DistributionStrategy Strategy { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    
    public int PendingJobs { get; set; }
    public int AvailableAgents { get; set; }
    public int AssignedJobs { get; set; }
    public int FailedAssignments { get; set; }
    
    public Dictionary<Guid, int> AgentJobCounts { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

public class WorkloadRebalanceResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    
    public double AverageLoad { get; set; }
    public double TargetUtilization { get; set; }
    public int OverloadedAgents { get; set; }
    public int UnderloadedAgents { get; set; }
    public int JobsMoved { get; set; }
    
    public List<string> Messages { get; set; } = new();
}