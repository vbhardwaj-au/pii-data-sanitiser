using Microsoft.AspNetCore.SignalR;
using API.Hubs;
using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;

namespace API.Services;

public interface IRealtimeMonitoringService
{
    Task BroadcastAgentStatusUpdate(Guid agentId, AgentStatus status, decimal? cpuUsage = null, decimal? memoryUsage = null);
    Task BroadcastJobUpdate(Guid jobId, JobStatus status, decimal? progressPercentage = null);
    Task BroadcastSystemMetrics();
    Task SendAgentAlert(string message, AlertSeverity severity);
    Task SendJobAlert(Guid jobId, string message, AlertSeverity severity);
}

public class RealtimeMonitoringService : IRealtimeMonitoringService
{
    private readonly IHubContext<MonitoringHub> _hubContext;
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly ILogger<RealtimeMonitoringService> _logger;

    public RealtimeMonitoringService(
        IHubContext<MonitoringHub> hubContext,
        IObfuscationAgentRepository agentRepository,
        IObfuscationJobRepository jobRepository,
        ILogger<RealtimeMonitoringService> logger)
    {
        _hubContext = hubContext;
        _agentRepository = agentRepository;
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task BroadcastAgentStatusUpdate(Guid agentId, AgentStatus status, decimal? cpuUsage = null, decimal? memoryUsage = null)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null) return;

            var updateData = new
            {
                AgentId = agentId,
                Name = agent.Name,
                Status = status.ToString(),
                CpuUsage = cpuUsage ?? agent.CurrentCpuUsage,
                MemoryUsage = memoryUsage ?? agent.CurrentMemoryUsage,
                LastHeartbeat = DateTime.UtcNow,
                ActiveJobs = agent.ActiveJobs,
                CompletedJobs = agent.CompletedJobs,
                FailedJobs = agent.FailedJobs
            };

            await _hubContext.Clients.Group("AgentMonitoring")
                .SendAsync("AgentStatusUpdate", updateData);

            _logger.LogDebug("Broadcasted agent status update for {AgentName}: {Status}", 
                agent.Name, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast agent status update for {AgentId}", agentId);
        }
    }

    public async Task BroadcastJobUpdate(Guid jobId, JobStatus status, decimal? progressPercentage = null)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null) return;

            var updateData = new
            {
                JobId = jobId,
                JobName = job.JobName,
                Status = status.ToString(),
                Progress = progressPercentage ?? job.ProgressPercentage,
                AgentId = job.AgentId,
                ProductId = job.ProductId,
                ProcessedRows = job.ProcessedRows,
                TotalRows = job.TotalRows,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt
            };

            await _hubContext.Clients.Group("JobMonitoring")
                .SendAsync("JobStatusUpdate", updateData);

            _logger.LogDebug("Broadcasted job status update for {JobName}: {Status} ({Progress}%)", 
                job.JobName, status, progressPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast job status update for {JobId}", jobId);
        }
    }

    public async Task BroadcastSystemMetrics()
    {
        try
        {
            var agents = await _agentRepository.GetAllAsync();
            var jobs = await _jobRepository.GetAllAsync();

            var metrics = new
            {
                TotalAgents = agents.Count(),
                OnlineAgents = agents.Count(a => a.Status == AgentStatus.Online),
                BusyAgents = agents.Count(a => a.Status == AgentStatus.Busy),
                OfflineAgents = agents.Count(a => a.Status == AgentStatus.Offline),
                TotalJobs = jobs.Count(),
                RunningJobs = jobs.Count(j => j.Status == JobStatus.Running),
                PendingJobs = jobs.Count(j => j.Status == JobStatus.Pending),
                CompletedJobs = jobs.Count(j => j.Status == JobStatus.Completed),
                FailedJobs = jobs.Count(j => j.Status == JobStatus.Failed),
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group("MonitoringUsers")
                .SendAsync("SystemMetricsUpdate", metrics);

            _logger.LogDebug("Broadcasted system metrics update");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast system metrics");
        }
    }

    public async Task SendAgentAlert(string message, AlertSeverity severity)
    {
        try
        {
            var alert = new
            {
                Type = "Agent",
                Message = message,
                Severity = severity.ToString(),
                Timestamp = DateTime.UtcNow,
                Id = Guid.NewGuid()
            };

            await _hubContext.Clients.Group("MonitoringUsers")
                .SendAsync("SystemAlert", alert);

            _logger.LogInformation("Sent agent alert: {Message} (Severity: {Severity})", 
                message, severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send agent alert: {Message}", message);
        }
    }

    public async Task SendJobAlert(Guid jobId, string message, AlertSeverity severity)
    {
        try
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            
            var alert = new
            {
                Type = "Job",
                JobId = jobId,
                JobName = job?.JobName ?? "Unknown Job",
                Message = message,
                Severity = severity.ToString(),
                Timestamp = DateTime.UtcNow,
                Id = Guid.NewGuid()
            };

            await _hubContext.Clients.Group("MonitoringUsers")
                .SendAsync("SystemAlert", alert);

            _logger.LogInformation("Sent job alert for {JobId}: {Message} (Severity: {Severity})", 
                jobId, message, severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send job alert for {JobId}: {Message}", jobId, message);
        }
    }
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}