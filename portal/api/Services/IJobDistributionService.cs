using Contracts.DTOs;

namespace API.Services;

public interface IJobDistributionService
{
    Task<JobDistributionResult> DistributePendingJobsAsync();
    Task<JobDistributionResult> DistributeJobsToSpecificAgentsAsync(List<Guid> agentIds);
    Task<Guid?> FindBestAgentForJobAsync(Guid jobId);
    Task AssignJobToAgentAsync(Guid jobId, Guid agentId);
    Task RebalanceJobsAsync();
    Task HandleFailedJobRedistributionAsync();
}

public class JobDistributionResult
{
    public int PendingJobs { get; set; }
    public int AvailableAgents { get; set; }
    public int AssignedJobs { get; set; }
    public int FailedAssignments { get; set; }
    public Dictionary<Guid, int> AgentJobCounts { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}