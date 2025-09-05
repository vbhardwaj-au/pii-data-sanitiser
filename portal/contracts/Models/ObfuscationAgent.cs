using System.ComponentModel.DataAnnotations;

namespace Contracts.Models;

public class ObfuscationAgent
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string HostName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;
    
    [Required]
    public int Port { get; set; } = 8080;
    
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Environment { get; set; } = "Production"; // Production, Development, Test
    
    [MaxLength(50)]
    public string Location { get; set; } = string.Empty; // OnPremises, Azure, AWS, etc.
    
    public AgentStatus Status { get; set; } = AgentStatus.Offline;
    
    public AgentCapabilities Capabilities { get; set; } = new();
    
    public DateTime LastHeartbeat { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastJobStarted { get; set; }
    public DateTime? LastJobCompleted { get; set; }
    
    // Performance metrics
    public int CpuCores { get; set; }
    public long MemoryMB { get; set; }
    public int CurrentCpuUsage { get; set; }
    public long CurrentMemoryUsage { get; set; }
    public int ActiveJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    
    // Configuration
    public int MaxConcurrentJobs { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 1; // Higher number = higher priority
    
    // Authentication
    [MaxLength(255)]
    public string? ApiKey { get; set; } // Unique API key for this agent
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    // Navigation properties
    public virtual ICollection<ObfuscationJob> Jobs { get; set; } = new List<ObfuscationJob>();
}

public enum AgentStatus
{
    Offline = 0,
    Online = 1,
    Busy = 2,
    Error = 3,
    Maintenance = 4
}

public class AgentCapabilities
{
    public bool CanProcessSqlServer { get; set; } = true;
    public bool CanProcessMySQL { get; set; } = false;
    public bool CanProcessPostgreSQL { get; set; } = false;
    public bool CanProcessOracle { get; set; } = false;
    public long MaxDatabaseSizeGB { get; set; } = 100;
    public int MaxRowsPerBatch { get; set; } = 10000;
    public List<string> SupportedObfuscationTypes { get; set; } = new();
}