using Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories;

public class ObfuscationAgentRepository : IObfuscationAgentRepository
{
    private readonly PortalDbContext _context;

    public ObfuscationAgentRepository(PortalDbContext context)
    {
        _context = context;
    }

    public async Task<ObfuscationAgent?> GetByIdAsync(Guid id)
    {
        return await _context.ObfuscationAgents
            .Include(a => a.Jobs.Take(10))
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<ObfuscationAgent?> GetByNameAsync(string name)
    {
        return await _context.ObfuscationAgents
            .FirstOrDefaultAsync(a => a.Name == name);
    }

    public async Task<ObfuscationAgent?> GetByHostAndPortAsync(string hostName, int port)
    {
        return await _context.ObfuscationAgents
            .FirstOrDefaultAsync(a => a.HostName == hostName && a.Port == port);
    }

    public async Task<IEnumerable<ObfuscationAgent>> GetAllAsync()
    {
        return await _context.ObfuscationAgents
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationAgent>> GetActiveAgentsAsync()
    {
        return await _context.ObfuscationAgents
            .Where(a => a.IsEnabled && a.Status != AgentStatus.Maintenance)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationAgent>> GetAvailableAgentsAsync()
    {
        return await _context.ObfuscationAgents
            .Where(a => a.IsEnabled && 
                       (a.Status == AgentStatus.Online || a.Status == AgentStatus.Busy) && 
                       a.ActiveJobs < a.MaxConcurrentJobs)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.ActiveJobs)
            .ThenBy(a => a.CurrentCpuUsage)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationAgent>> GetOnlineAgentsAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-5); // Consider offline if no heartbeat for 5 minutes
        return await _context.ObfuscationAgents
            .Where(a => a.IsEnabled && 
                       a.LastHeartbeat > threshold &&
                       (a.Status == AgentStatus.Online || a.Status == AgentStatus.Busy))
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ObfuscationAgent>> GetAgentsByLocationAsync(string location)
    {
        return await _context.ObfuscationAgents
            .Where(a => a.Location == location)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<ObfuscationAgent> CreateAsync(ObfuscationAgent agent)
    {
        agent.Id = Guid.NewGuid();
        agent.RegisteredAt = DateTime.UtcNow;
        agent.LastHeartbeat = DateTime.UtcNow;
        
        _context.ObfuscationAgents.Add(agent);
        await _context.SaveChangesAsync();
        return agent;
    }

    public async Task<ObfuscationAgent> UpdateAsync(ObfuscationAgent agent)
    {
        _context.ObfuscationAgents.Update(agent);
        await _context.SaveChangesAsync();
        return agent;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var agent = await _context.ObfuscationAgents.FindAsync(id);
        if (agent == null)
            return false;

        _context.ObfuscationAgents.Remove(agent);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task UpdateLastHeartbeatAsync(Guid agentId, DateTime timestamp)
    {
        await _context.ObfuscationAgents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastHeartbeat, timestamp));
    }

    public async Task UpdateStatusAsync(Guid agentId, AgentStatus status)
    {
        await _context.ObfuscationAgents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, status)
                .SetProperty(a => a.LastHeartbeat, DateTime.UtcNow));
    }

    public async Task UpdatePerformanceMetricsAsync(Guid agentId, int cpuUsage, long memoryUsage, int activeJobs)
    {
        await _context.ObfuscationAgents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.CurrentCpuUsage, cpuUsage)
                .SetProperty(a => a.CurrentMemoryUsage, memoryUsage)
                .SetProperty(a => a.ActiveJobs, activeJobs)
                .SetProperty(a => a.LastHeartbeat, DateTime.UtcNow));
    }

    public async Task<int> GetActiveJobCountAsync(Guid agentId)
    {
        return await _context.ObfuscationJobs
            .CountAsync(j => j.AgentId == agentId && 
                           (j.Status == JobStatus.Running || j.Status == JobStatus.Assigned));
    }

    public async Task<bool> IsAgentHealthyAsync(Guid agentId, TimeSpan healthyThreshold)
    {
        var agent = await _context.ObfuscationAgents.FindAsync(agentId);
        if (agent == null)
            return false;

        return DateTime.UtcNow - agent.LastHeartbeat < healthyThreshold;
    }

    public async Task MarkAgentsOfflineAsync(TimeSpan offlineThreshold)
    {
        var cutoffTime = DateTime.UtcNow - offlineThreshold;
        await _context.ObfuscationAgents
            .Where(a => a.LastHeartbeat < cutoffTime && a.Status != AgentStatus.Offline)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AgentStatus.Offline));
    }
}