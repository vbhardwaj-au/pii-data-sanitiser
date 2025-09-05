using System.ComponentModel.DataAnnotations;

namespace Contracts.Models;

public class ObfuscationJob
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid ProductId { get; set; }
    
    public Guid? AgentId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string JobName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    
    [Required]
    [MaxLength(50)]
    public string MappingVersion { get; set; } = string.Empty;
    
    [Required]
    public string MappingSasUrl { get; set; } = string.Empty;
    
    // Job configuration
    public JobConfiguration Configuration { get; set; } = new();
    
    // Timing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastUpdateAt { get; set; }
    
    // Progress tracking
    public decimal ProgressPercentage { get; set; } = 0;
    public long TotalRows { get; set; }
    public long ProcessedRows { get; set; }
    public int TablesCompleted { get; set; }
    public int TotalTables { get; set; }
    
    // Results and errors
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
    [MaxLength(4000)]
    public string? ResultSummary { get; set; }
    public string? ResultsBlobUrl { get; set; }
    
    // Performance metrics
    public TimeSpan? Duration { get; set; }
    public decimal? RowsPerSecond { get; set; }
    public long? PeakMemoryUsageMB { get; set; }
    
    // Retry logic
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    
    [MaxLength(100)]
    public string CreatedBy { get; set; } = "System";
    
    // Navigation properties
    public virtual Product? Product { get; set; }
    public virtual ObfuscationAgent? Agent { get; set; }
    public virtual ICollection<JobExecutionLog> ExecutionLogs { get; set; } = new List<JobExecutionLog>();
}

public enum JobStatus
{
    Pending = 0,
    Assigned = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Retrying = 6
}

public enum JobPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public class JobConfiguration
{
    public bool DryRun { get; set; } = false;
    public int BatchSize { get; set; } = 1000;
    public int SqlBatchSize { get; set; } = 500;
    public int ParallelThreads { get; set; } = 4;
    public int CommandTimeoutSeconds { get; set; } = 300;
    public bool GenerateReport { get; set; } = true;
    public bool ValidateResults { get; set; } = true;
    public List<string> SpecificTables { get; set; } = new();
    public List<string> ExcludedTables { get; set; } = new();
}