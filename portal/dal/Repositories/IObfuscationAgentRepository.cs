using Contracts.Models;

namespace DAL.Repositories;

public interface IObfuscationAgentRepository
{
    Task<ObfuscationAgent?> GetByIdAsync(Guid id);
    Task<ObfuscationAgent?> GetByNameAsync(string name);
    Task<ObfuscationAgent?> GetByHostAndPortAsync(string hostName, int port);
    Task<IEnumerable<ObfuscationAgent>> GetAllAsync();
    Task<IEnumerable<ObfuscationAgent>> GetActiveAgentsAsync();
    Task<IEnumerable<ObfuscationAgent>> GetAvailableAgentsAsync();
    Task<IEnumerable<ObfuscationAgent>> GetOnlineAgentsAsync();
    Task<IEnumerable<ObfuscationAgent>> GetAgentsByLocationAsync(string location);
    Task<ObfuscationAgent> CreateAsync(ObfuscationAgent agent);
    Task<ObfuscationAgent> UpdateAsync(ObfuscationAgent agent);
    Task<bool> DeleteAsync(Guid id);
    Task UpdateLastHeartbeatAsync(Guid agentId, DateTime timestamp);
    Task UpdateStatusAsync(Guid agentId, AgentStatus status);
    Task UpdatePerformanceMetricsAsync(Guid agentId, int cpuUsage, long memoryUsage, int activeJobs);
    Task<int> GetActiveJobCountAsync(Guid agentId);
    Task<bool> IsAgentHealthyAsync(Guid agentId, TimeSpan healthyThreshold);
    Task MarkAgentsOfflineAsync(TimeSpan offlineThreshold);
}