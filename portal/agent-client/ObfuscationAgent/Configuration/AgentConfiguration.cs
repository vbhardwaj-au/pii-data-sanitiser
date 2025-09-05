namespace ObfuscationAgent.Configuration;

public class AgentConfiguration
{
    public Guid? AgentId { get; set; } // Can be set via OBFUSCATION_Agent__AgentId environment variable
    public string Name { get; set; } = System.Environment.MachineName;
    public string HostName { get; set; } = System.Environment.MachineName;
    public string? IpAddress { get; set; }
    public int Port { get; set; } = 5000;
    public string Version { get; set; } = "1.0.0";
    public string Environment { get; set; } = "Production";
    public string Location { get; set; } = "OnPremises";
    public int CpuCores { get; set; } = System.Environment.ProcessorCount;
    public int MemoryMB { get; set; } = 8192;
    public int MaxConcurrentJobs { get; set; } = 1;  // Only handle one job at a time
    public int Priority { get; set; } = 1;
    public AgentCapabilities Capabilities { get; set; } = new();
}

public class PortalConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:7001";
    public string RegistrationEndpoint { get; set; } = "/api/agents/register";
    public string HeartbeatEndpoint { get; set; } = "/api/agents/{0}/heartbeat";
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int JobPollIntervalSeconds { get; set; } = 10;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public bool UseSSL { get; set; } = false;
    public bool ValidateServerCertificate { get; set; } = true;
}

public class StorageConfiguration
{
    public string LocalWorkingDirectory { get; set; } = "./work";
    public string TempDirectory { get; set; } = "./temp";
    public string LogDirectory { get; set; } = "./logs";
    public string MappingCacheDirectory { get; set; } = "./cache/mappings";
    public int MaxLogFileSizeMB { get; set; } = 100;
    public int LogRetentionDays { get; set; } = 30;
}

public class AgentCapabilities
{
    public bool CanProcessSqlServer { get; set; } = true;
    public bool CanProcessMySQL { get; set; } = false;
    public bool CanProcessPostgreSQL { get; set; } = false;
    public bool CanProcessOracle { get; set; } = false;
    public long MaxDatabaseSizeGB { get; set; } = 100;
    public int MaxRowsPerBatch { get; set; } = 10000;
    public List<string> SupportedObfuscationTypes { get; set; } = new()
    {
        "Name", "Address", "Email", "Phone", "SSN", "CreditCard", "Date"
    };
}