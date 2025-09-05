using Contracts.Models;
using DAL.Repositories;

namespace API.Services;

public class JobDistributionService : IJobDistributionService
{
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly IAgentDiscoveryService _agentDiscoveryService;
    private readonly ILogger<JobDistributionService> _logger;

    public JobDistributionService(
        IObfuscationJobRepository jobRepository,
        IObfuscationAgentRepository agentRepository,
        IAgentDiscoveryService agentDiscoveryService,
        ILogger<JobDistributionService> logger)
    {
        _jobRepository = jobRepository;
        _agentRepository = agentRepository;
        _agentDiscoveryService = agentDiscoveryService;
        _logger = logger;
    }

    public async Task<JobDistributionResult> DistributePendingJobsAsync()
    {
        var result = new JobDistributionResult();
        
        try
        {
            // Get pending jobs ordered by priority
            var pendingJobs = await _jobRepository.GetPendingJobsAsync();
            result.PendingJobs = pendingJobs.Count();

            // Get available agents
            var availableAgents = (await _agentRepository.GetAvailableAgentsAsync()).ToList();
            result.AvailableAgents = availableAgents.Count;

            if (!availableAgents.Any())
            {
                result.Messages.Add("No available agents for job assignment");
                return result;
            }

            // Distribute jobs using priority and location consideration
            foreach (var job in pendingJobs)
            {
                try
                {
                    // Get the product's location to enforce location constraints
                    var productLocation = job.Product?.Location ?? "OnPremises";
                    
                    var bestAgent = await _agentDiscoveryService.FindBestAgentForJobAsync(
                        job.Priority, 
                        null, 
                        productLocation);
                        
                    if (bestAgent != null && availableAgents.Contains(bestAgent))
                    {
                        await _jobRepository.AssignJobToAgentAsync(job.Id, bestAgent.Id);
                        
                        result.AssignedJobs++;
                        if (result.AgentJobCounts.ContainsKey(bestAgent.Id))
                            result.AgentJobCounts[bestAgent.Id]++;
                        else
                            result.AgentJobCounts[bestAgent.Id] = 1;

                        // Update agent's active job count locally
                        bestAgent.ActiveJobs++;
                        
                        // Remove agent from available list if at capacity
                        if (bestAgent.ActiveJobs >= bestAgent.MaxConcurrentJobs)
                        {
                            availableAgents.Remove(bestAgent);
                        }

                        _logger.LogDebug("Assigned job {JobId} to agent {AgentName}", 
                            job.Id, bestAgent.Name);
                    }
                    else
                    {
                        result.FailedAssignments++;
                        _logger.LogWarning("Could not find suitable agent for job {JobId}", job.Id);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedAssignments++;
                    _logger.LogError(ex, "Failed to assign job {JobId}", job.Id);
                }

                // Break if no agents available
                if (!availableAgents.Any())
                {
                    result.Messages.Add("All agents at capacity");
                    break;
                }
            }

            result.Messages.Add($"Distributed {result.AssignedJobs} jobs to {result.AgentJobCounts.Count} agents");
            
            _logger.LogInformation("Job distribution completed: {AssignedJobs} assigned, {FailedAssignments} failed", 
                result.AssignedJobs, result.FailedAssignments);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to distribute pending jobs");
            result.Messages.Add($"Distribution failed: {ex.Message}");
            return result;
        }
    }

    public async Task<JobDistributionResult> DistributeJobsToSpecificAgentsAsync(List<Guid> agentIds)
    {
        var result = new JobDistributionResult();
        
        try
        {
            var agents = new List<ObfuscationAgent>();
            foreach (var agentId in agentIds)
            {
                var agent = await _agentRepository.GetByIdAsync(agentId);
                if (agent != null && agent.IsEnabled && agent.ActiveJobs < agent.MaxConcurrentJobs)
                {
                    agents.Add(agent);
                }
            }

            result.AvailableAgents = agents.Count;

            if (!agents.Any())
            {
                result.Messages.Add("No specified agents are available");
                return result;
            }

            var pendingJobs = await _jobRepository.GetPendingJobsAsync();
            result.PendingJobs = pendingJobs.Count();

            foreach (var job in pendingJobs)
            {
                var bestAgent = await FindBestAgentForJobInternal(job, agents);
                if (bestAgent != null)
                {
                    await _jobRepository.AssignJobToAgentAsync(job.Id, bestAgent.Id);
                    result.AssignedJobs++;
                    
                    if (result.AgentJobCounts.ContainsKey(bestAgent.Id))
                        result.AgentJobCounts[bestAgent.Id]++;
                    else
                        result.AgentJobCounts[bestAgent.Id] = 1;

                    bestAgent.ActiveJobs++;
                    if (bestAgent.ActiveJobs >= bestAgent.MaxConcurrentJobs)
                    {
                        agents.Remove(bestAgent);
                    }
                }

                if (!agents.Any())
                    break;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to distribute jobs to specific agents");
            result.Messages.Add($"Distribution failed: {ex.Message}");
            return result;
        }
    }

    public async Task<Guid?> FindBestAgentForJobAsync(Guid jobId)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
                return null;

            var availableAgents = (await _agentRepository.GetAvailableAgentsAsync()).ToList();
            var bestAgent = await FindBestAgentForJobInternal(job, availableAgents);
            
            return bestAgent?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find best agent for job {JobId}", jobId);
            return null;
        }
    }

    public async Task AssignJobToAgentAsync(Guid jobId, Guid agentId)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            var agent = await _agentRepository.GetByIdAsync(agentId);

            if (job == null || agent == null)
                throw new ArgumentException("Job or agent not found");

            if (!agent.IsEnabled || agent.ActiveJobs >= agent.MaxConcurrentJobs)
                throw new InvalidOperationException("Agent is not available for new jobs");

            await _jobRepository.AssignJobToAgentAsync(jobId, agentId);
            
            _logger.LogInformation("Manually assigned job {JobName} to agent {AgentName}", 
                job.JobName, agent.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign job {JobId} to agent {AgentId}", jobId, agentId);
            throw;
        }
    }

    public async Task RebalanceJobsAsync()
    {
        try
        {
            // Get all online agents and their current workloads
            var onlineAgents = (await _agentRepository.GetOnlineAgentsAsync()).ToList();
            if (onlineAgents.Count < 2)
                return; // No point in rebalancing with less than 2 agents

            // Calculate average workload
            var totalCapacity = onlineAgents.Sum(a => a.MaxConcurrentJobs);
            var totalActiveJobs = onlineAgents.Sum(a => a.ActiveJobs);
            var averageWorkload = (double)totalActiveJobs / onlineAgents.Count;

            // Find overloaded and underloaded agents
            var overloadedAgents = onlineAgents
                .Where(a => a.ActiveJobs > averageWorkload + 1)
                .OrderByDescending(a => a.ActiveJobs)
                .ToList();

            var underloadedAgents = onlineAgents
                .Where(a => a.ActiveJobs < a.MaxConcurrentJobs && a.ActiveJobs < averageWorkload - 1)
                .OrderBy(a => a.ActiveJobs)
                .ToList();

            if (!overloadedAgents.Any() || !underloadedAgents.Any())
                return;

            _logger.LogInformation("Rebalancing jobs: {OverloadedCount} overloaded, {UnderloadedCount} underloaded agents", 
                overloadedAgents.Count, underloadedAgents.Count);

            // Move pending/assigned jobs from overloaded to underloaded agents
            foreach (var overloadedAgent in overloadedAgents)
            {
                if (!underloadedAgents.Any())
                    break;

                var jobsToMove = await _jobRepository.GetByAgentIdAsync(overloadedAgent.Id);
                var pendingJobs = jobsToMove
                    .Where(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned)
                    .Take(overloadedAgent.ActiveJobs - (int)averageWorkload)
                    .ToList();

                foreach (var job in pendingJobs)
                {
                    var targetAgent = underloadedAgents.First();
                    
                    await _jobRepository.AssignJobToAgentAsync(job.Id, targetAgent.Id);
                    
                    targetAgent.ActiveJobs++;
                    overloadedAgent.ActiveJobs--;

                    if (targetAgent.ActiveJobs >= targetAgent.MaxConcurrentJobs)
                    {
                        underloadedAgents.Remove(targetAgent);
                    }

                    if (!underloadedAgents.Any())
                        break;
                }
            }

            _logger.LogInformation("Job rebalancing completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebalance jobs");
        }
    }

    public async Task HandleFailedJobRedistributionAsync()
    {
        try
        {
            var failedJobs = await _jobRepository.GetByStatusAsync(JobStatus.Failed);
            var retriableJobs = failedJobs.Where(j => j.RetryCount < j.MaxRetries).ToList();

            if (!retriableJobs.Any())
                return;

            _logger.LogInformation("Redistributing {Count} failed jobs for retry", retriableJobs.Count);

            foreach (var job in retriableJobs)
            {
                // Reset job for redistribution
                job.Status = JobStatus.Pending;
                job.ErrorMessage = null;
                job.AgentId = Guid.Empty;
                job.RetryCount++;

                await _jobRepository.UpdateAsync(job);
            }

            // Trigger normal distribution for the reset jobs
            await DistributePendingJobsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle failed job redistribution");
        }
    }

    private async Task<ObfuscationAgent?> FindBestAgentForJobInternal(ObfuscationJob job, List<ObfuscationAgent> availableAgents)
    {
        if (!availableAgents.Any())
            return null;

        // Scoring algorithm for agent selection
        var agentScores = new Dictionary<ObfuscationAgent, int>();

        foreach (var agent in availableAgents)
        {
            var score = 0;

            // Priority weight (higher priority agents get more jobs)
            score += agent.Priority * 10;

            // Capacity weight (agents with more available capacity are preferred)
            var availableCapacity = agent.MaxConcurrentJobs - agent.ActiveJobs;
            score += availableCapacity * 5;

            // Performance weight (lower CPU usage is better)
            score += (100 - agent.CurrentCpuUsage) / 10;

            // Location preference (prefer on-premises for certain jobs)
            if (job.Priority == JobPriority.High && agent.Location == "OnPremises")
                score += 15;

            // Capability matching
            if (job.Product != null)
            {
                var dbTech = job.Product.DatabaseTechnology?.ToLower();
                if (dbTech == "sqlserver" && agent.Capabilities.CanProcessSqlServer)
                    score += 10;
                // Add more database technology checks as needed
            }

            agentScores[agent] = score;
        }

        // Return agent with highest score
        return agentScores.OrderByDescending(kv => kv.Value).First().Key;
    }
}