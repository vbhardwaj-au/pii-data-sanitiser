using Contracts.Models;

namespace ObfuscationAgent.Models;

public class AgentRegistrationRequest
{
    public Guid? AgentId { get; set; } // For re-registration with existing ID
    public string Name { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public int Port { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public int MemoryMB { get; set; }
    public int MaxConcurrentJobs { get; set; }
    public int Priority { get; set; }
    public AgentCapabilities Capabilities { get; set; } = new();
}

public class AgentRegistrationResponse
{
    public bool Success { get; set; }
    public Guid AgentId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class AgentHeartbeatRequest
{
    public AgentStatus Status { get; set; }
    public int ActiveJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public decimal CpuUsage { get; set; }
    public decimal MemoryUsage { get; set; }
    public long DiskSpaceAvailable { get; set; }
    public int MaxConcurrentJobs { get; set; }
}

public class AgentHeartbeatResponse
{
    public bool Success { get; set; }
    public bool RequiresReregistration { get; set; }
    public List<JobAssignment>? NewJobs { get; set; }
    public string? Message { get; set; }
}

public class JobRequest
{
    public int MaxConcurrentJobs { get; set; }
}

public class JobAssignmentResponse
{
    public bool HasJob { get; set; }
    public JobAssignment? Job { get; set; }
}

public class JobAssignment
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string MappingBlobUrl { get; set; } = string.Empty;
    public string? SasToken { get; set; }
    public JobPriority Priority { get; set; }
    public int? TimeoutMinutes { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

public class JobStatusUpdate
{
    public JobStatus Status { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }
}

public class JobProgressUpdate
{
    public decimal ProgressPercentage { get; set; }
    public long RowsProcessed { get; set; }
    public long RowsAffected { get; set; }
    public string? CurrentTable { get; set; }
    public string? CurrentOperation { get; set; }
    public decimal? RowsPerSecond { get; set; }
    public long? EstimatedRemainingMs { get; set; }
}

public class JobResultsUpload
{
    public string ResultsData { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Format { get; set; } = "json";
}

public class MappingUrlResponse
{
    public string MappingUrl { get; set; } = string.Empty;
    public string? SasToken { get; set; }
}

// Pipeline Configuration Models for Stage-based Execution
public class ProductObfuscationConfiguration
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SourceConnectionString { get; set; } = string.Empty;
    public string TargetConnectionString { get; set; } = string.Empty;
    public List<StageConfiguration> Stages { get; set; } = new();
    public int BatchSize { get; set; } = 1000;
    public int SqlBatchSize { get; set; } = 10000;
    public int ParallelThreads { get; set; } = 4;
    public int CommandTimeoutSeconds { get; set; } = 300;
    public string? GlobalSeed { get; set; }
    public int MaxCacheSize { get; set; } = 1000000;
}

public class StageConfiguration
{
    public string StageName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ExecutionOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int TimeoutMinutes { get; set; } = 60;
    public List<ScriptConfiguration>? Scripts { get; set; }
}

public class ScriptConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ScriptType { get; set; } = "sql"; // sql, powershell, bash
    public int ExecutionOrder { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
    public bool ContinueOnError { get; set; } = false;
}

public class JobConfiguration
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

public class ObfuscationConfiguration
{
    public string SourceConnectionString { get; set; } = string.Empty;
    public string TargetConnectionString { get; set; } = string.Empty;
    public string MappingJson { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 1000;
    public int SqlBatchSize { get; set; } = 10000;
    public int ParallelThreads { get; set; } = 4;
    public int CommandTimeoutSeconds { get; set; } = 300;
    public string? GlobalSeed { get; set; }
    public int MaxCacheSize { get; set; } = 1000000;
}

public enum StageStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4,
    Cancelled = 5
}