using Contracts.Models;

namespace Contracts.DTOs;

public class AgentRegistrationRequest
{
    public Guid? AgentId { get; set; } // For re-registration with existing ID
    public string Name { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 8080;
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = "Production";
    public string Location { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public long MemoryMB { get; set; }
    public int MaxConcurrentJobs { get; set; } = 1;
    public AgentCapabilities Capabilities { get; set; } = new();
    public string? Notes { get; set; }
}

public class AgentRegistrationResponse
{
    public Guid AgentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public string PortalBaseUrl { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AgentHeartbeatRequest
{
    public Guid AgentId { get; set; }
    public AgentStatus Status { get; set; }
    public int CurrentCpuUsage { get; set; }
    public long CurrentMemoryUsage { get; set; }
    public int ActiveJobs { get; set; }
    public List<JobStatusUpdate> JobUpdates { get; set; } = new();
    public string? StatusMessage { get; set; }
}

public class AgentHeartbeatResponse
{
    public bool Success { get; set; }
    public DateTime ServerTimestamp { get; set; }
    public List<JobAssignment> NewJobs { get; set; } = new();
    public List<Guid> CancelledJobs { get; set; } = new();
    public AgentCommands Commands { get; set; } = new();
    public string? Message { get; set; }
}

public class JobStatusUpdate
{
    public Guid JobId { get; set; }
    public JobStatus Status { get; set; }
    public decimal ProgressPercentage { get; set; }
    public long ProcessedRows { get; set; }
    public int TablesCompleted { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CurrentTable { get; set; }
    public decimal? RowsPerSecond { get; set; }
    public long? MemoryUsageMB { get; set; }
}

public class JobAssignment
{
    public Guid JobId { get; set; }
    public Guid ProductId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JobPriority Priority { get; set; }
    public string MappingVersion { get; set; } = string.Empty;
    public string MappingSasUrl { get; set; } = string.Empty;
    public JobConfiguration Configuration { get; set; } = new();
    public DateTime AssignedAt { get; set; }
    public string ProductConnectionString { get; set; } = string.Empty;
}

public class AgentCommands
{
    public bool RestartRequested { get; set; }
    public bool MaintenanceMode { get; set; }
    public bool UpdateConfiguration { get; set; }
    public Dictionary<string, object> ConfigurationUpdates { get; set; } = new();
}

public class AgentListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public DateTime RegisteredAt { get; set; }
    public int CpuCores { get; set; }
    public long MemoryMB { get; set; }
    public int CurrentCpuUsage { get; set; }
    public long CurrentMemoryUsage { get; set; }
    public int ActiveJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int MaxConcurrentJobs { get; set; }
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public TimeSpan? UptimeSinceLastStart { get; set; }
    public bool IsHealthy { get; set; }
    public string? ApiKey { get; set; }
}

public class JobCreationRequest
{
    public Guid ProductId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public string? MappingVersion { get; set; }
    public JobConfiguration Configuration { get; set; } = new();
    public Guid? PreferredAgentId { get; set; }
}

public class JobCreationResponse
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string MappingVersion { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}