using System.ComponentModel.DataAnnotations;

namespace Contracts.Models;

public class JobExecutionLog
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid JobId { get; set; }
    
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [Required]
    public LogLevel LogLevel { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Component { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public string? Details { get; set; }
    public string? Exception { get; set; }
    
    [MaxLength(100)]
    public string? TableName { get; set; }
    
    [MaxLength(100)]
    public string? ColumnName { get; set; }
    
    public long? RowsProcessed { get; set; }
    public decimal? ProgressPercentage { get; set; }
    public TimeSpan? ElapsedTime { get; set; }
    
    // Performance metrics
    public decimal? CpuUsagePercentage { get; set; }
    public long? MemoryUsageMB { get; set; }
    public decimal? RowsPerSecond { get; set; }
    
    // Navigation property
    public virtual ObfuscationJob? Job { get; set; }
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}