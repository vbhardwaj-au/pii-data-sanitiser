# Distributed Obfuscation Pipeline Implementation Plan

## Overview
Complete implementation of distributed obfuscation pipeline with agent-product assignment, real-time log streaming, and 5-stage pipeline execution.

## Architecture Flow

```
Portal UI → Trigger Pipeline → API → Assign to Agent → Agent Executes 5 Stages → Stream Logs → Display in Portal
```

## 1. Agent-Product Assignment

### API Changes Needed

#### A. Product-Agent Assignment Endpoint
```csharp
// POST /api/products/{productId}/agents/{agentId}/assign
[HttpPost("{productId}/agents/{agentId}/assign")]
public async Task<IActionResult> AssignAgentToProduct(Guid productId, Guid agentId)
{
    // Create ProductAgentAssignment record
    // Update agent capabilities for product
    return Ok();
}
```

#### B. Get Product Configuration for Agent
```csharp
// GET /api/agent/products/{productId}/configuration
[HttpGet("products/{productId}/configuration")]
[AgentAuthentication]
public async Task<IActionResult> GetProductConfiguration(Guid productId)
{
    // Verify agent is assigned to product
    // Return:
    // - Connection strings (source, target, backup)
    // - Global seed
    // - Batch settings
    // - Pipeline stages with scripts
    return Ok(configuration);
}
```

#### C. Download Mapping File
```csharp
// GET /api/agent/products/{productId}/mapping
[HttpGet("products/{productId}/mapping")]
[AgentAuthentication]
public async Task<IActionResult> GetProductMapping(Guid productId)
{
    // Return unified mapping JSON with:
    // - Table mappings
    // - Column obfuscation rules
    // - Exclusions
    // - Custom scripts
    return File(mappingJson);
}
```

## 2. Pipeline Trigger from Portal

### Trigger Mechanism
```csharp
// POST /api/pipeline/execute
[HttpPost("execute")]
public async Task<IActionResult> ExecutePipeline([FromBody] PipelineExecutionRequest request)
{
    // 1. Create PipelineExecution record
    // 2. Find available agent for product
    // 3. Create ObfuscationJob
    // 4. Notify agent via SignalR or polling
    // 5. Return execution ID for monitoring
}
```

## 3. Log Streaming Implementation

### A. Agent Side - Log Sender
```csharp
public class LogStreamingService
{
    private readonly HttpClient _httpClient;
    private readonly Queue<LogEntry> _logBuffer = new();
    
    public async Task SendLogBatchAsync()
    {
        var logs = _logBuffer.Take(100).ToList();
        await _httpClient.PostAsJsonAsync("/api/agent/logs/stream", new
        {
            JobId = _currentJobId,
            Logs = logs
        });
    }
    
    public void LogProgress(string message, LogLevel level, Dictionary<string, object> metadata)
    {
        _logBuffer.Enqueue(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Message = message,
            Level = level,
            Metadata = metadata
        });
    }
}
```

### B. Server Side - SignalR Hub
```csharp
public class PipelineMonitoringHub : Hub
{
    public async Task JoinJobMonitoring(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
    }
    
    public async Task BroadcastLogEntry(string jobId, LogEntry log)
    {
        await Clients.Group($"job-{jobId}").SendAsync("LogReceived", log);
    }
    
    public async Task BroadcastProgress(string jobId, ProgressUpdate progress)
    {
        await Clients.Group($"job-{jobId}").SendAsync("ProgressUpdate", progress);
    }
}
```

### C. Log Storage
```csharp
// Store logs in database for persistence
public class LogEntry
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Stage { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

## 4. Five-Stage Pipeline Implementation

### Pipeline Stages
1. **Data Reduction** (Optional)
2. **Pre-Obfuscation Validation** (Required)
3. **PII Data Obfuscation** (Required)
4. **Post-Obfuscation Verification** (Required)
5. **Testing & Validation** (Optional)

### Agent Pipeline Executor
```csharp
public class PipelineExecutor
{
    private readonly ILogger<PipelineExecutor> _logger;
    private readonly IScriptExecutor _scriptExecutor;
    private readonly IObfuscationEngine _obfuscationEngine;
    private readonly ILogStreamingService _logStreaming;
    
    public async Task<PipelineResult> ExecutePipelineAsync(PipelineConfiguration config)
    {
        var result = new PipelineResult();
        
        foreach (var stage in config.Stages.OrderBy(s => s.Order))
        {
            _logger.LogInformation($"Starting stage: {stage.Name}");
            await _logStreaming.SendLogAsync($"Starting stage: {stage.Name}", LogLevel.Info);
            
            try
            {
                if (stage.Name == "PIIDataObfuscation")
                {
                    // Execute obfuscation
                    await ExecuteObfuscationAsync(config);
                }
                else if (stage.Scripts.Any())
                {
                    // Execute stage scripts
                    foreach (var script in stage.Scripts)
                    {
                        var scriptResult = await _scriptExecutor.ExecuteAsync(script);
                        if (!scriptResult.Success && stage.IsRequired)
                        {
                            throw new PipelineException($"Required stage {stage.Name} failed");
                        }
                    }
                }
                else if (stage.IsRequired)
                {
                    // Required stage with no scripts - log warning but continue
                    _logger.LogWarning($"Required stage {stage.Name} has no scripts");
                }
                
                await _logStreaming.SendLogAsync($"Completed stage: {stage.Name}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Stage {stage.Name} failed");
                await _logStreaming.SendLogAsync($"Stage {stage.Name} failed: {ex.Message}", LogLevel.Error);
                
                if (stage.IsRequired)
                {
                    result.Success = false;
                    result.FailedStage = stage.Name;
                    result.ErrorMessage = ex.Message;
                    return result;
                }
            }
        }
        
        result.Success = true;
        return result;
    }
    
    private async Task ExecuteObfuscationAsync(PipelineConfiguration config)
    {
        // Stream obfuscation logs
        _obfuscationEngine.OnProgress += async (sender, args) =>
        {
            await _logStreaming.SendLogAsync(args.Message, LogLevel.Info, new
            {
                Table = args.TableName,
                Progress = args.ProgressPercent,
                RowsProcessed = args.RowsProcessed
            });
        };
        
        await _obfuscationEngine.ObfuscateAsync(config);
    }
}
```

## 5. Script Execution Service
```csharp
public class ScriptExecutor
{
    public async Task<ScriptResult> ExecuteAsync(PipelineScript script)
    {
        switch (script.Type)
        {
            case ScriptType.SQL:
                return await ExecuteSqlScriptAsync(script);
            case ScriptType.PowerShell:
                return await ExecutePowerShellScriptAsync(script);
            case ScriptType.Bash:
                return await ExecuteBashScriptAsync(script);
            default:
                throw new NotSupportedException($"Script type {script.Type} not supported");
        }
    }
    
    private async Task<ScriptResult> ExecuteSqlScriptAsync(PipelineScript script)
    {
        using var connection = new SqlConnection(script.ConnectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(script.Content, connection);
        command.CommandTimeout = script.TimeoutSeconds;
        
        var result = new ScriptResult();
        try
        {
            await command.ExecuteNonQueryAsync();
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        
        return result;
    }
}
```

## 6. Portal UI for Monitoring

### Real-time Log Display
```html
<div id="pipeline-monitor">
    <div class="pipeline-stages">
        @foreach (var stage in Model.Stages)
        {
            <div class="stage @GetStageStatusClass(stage.Status)">
                <h4>@stage.Name</h4>
                <div class="progress">
                    <div class="progress-bar" style="width: @stage.Progress%"></div>
                </div>
            </div>
        }
    </div>
    
    <div class="log-viewer">
        <div id="log-container" class="log-scroll">
            <!-- Logs will be added here via SignalR -->
        </div>
    </div>
</div>

<script>
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/pipeline")
        .build();
    
    connection.on("LogReceived", function (log) {
        const logElement = createLogElement(log);
        document.getElementById("log-container").appendChild(logElement);
        scrollToBottom();
    });
    
    connection.on("ProgressUpdate", function (progress) {
        updateStageProgress(progress.stage, progress.percent);
    });
    
    connection.start().then(function () {
        connection.invoke("JoinJobMonitoring", "@Model.JobId");
    });
</script>
```

## 7. Database Schema Updates

```sql
-- Product-Agent Assignment
CREATE TABLE ProductAgentAssignments (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    AgentId UNIQUEIDENTIFIER NOT NULL,
    AssignedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    AssignedBy NVARCHAR(100),
    Priority INT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    FOREIGN KEY (ProductId) REFERENCES Products(Id),
    FOREIGN KEY (AgentId) REFERENCES ObfuscationAgents(Id)
);

-- Pipeline Execution Logs
CREATE TABLE PipelineExecutionLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    JobId UNIQUEIDENTIFIER NOT NULL,
    StageId UNIQUEIDENTIFIER,
    Timestamp DATETIME2 NOT NULL,
    LogLevel NVARCHAR(20),
    Message NVARCHAR(MAX),
    Details NVARCHAR(MAX),
    Metadata NVARCHAR(MAX), -- JSON
    FOREIGN KEY (JobId) REFERENCES ObfuscationJobs(Id),
    FOREIGN KEY (StageId) REFERENCES PipelineStages(Id)
);

-- Add indexes for performance
CREATE INDEX IX_PipelineExecutionLogs_JobId ON PipelineExecutionLogs(JobId);
CREATE INDEX IX_PipelineExecutionLogs_Timestamp ON PipelineExecutionLogs(Timestamp);
```

## 8. Agent Service Updates

```csharp
public class AgentWorkerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Poll for assigned jobs
            var job = await _portalClient.GetNextJobAsync();
            
            if (job != null)
            {
                // 2. Fetch product configuration
                var config = await _portalClient.GetProductConfigurationAsync(job.ProductId);
                
                // 3. Download mapping file
                var mapping = await _portalClient.DownloadMappingAsync(job.ProductId);
                
                // 4. Execute pipeline with log streaming
                var executor = new PipelineExecutor(_logStreaming);
                var result = await executor.ExecutePipelineAsync(config);
                
                // 5. Report completion
                await _portalClient.ReportJobCompletionAsync(job.Id, result);
            }
            
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
```

## Implementation Priority

1. **Phase 1**: Agent-Product Assignment & Configuration API
2. **Phase 2**: Log Streaming Infrastructure
3. **Phase 3**: 5-Stage Pipeline Execution
4. **Phase 4**: Portal UI for Monitoring
5. **Phase 5**: Testing & Optimization

## Success Criteria

- [ ] Agents can be assigned to products
- [ ] Agents fetch configuration and mappings from API
- [ ] Pipeline can be triggered from portal
- [ ] All logs stream in real-time to portal
- [ ] 5-stage pipeline executes in sequence
- [ ] Stage failures stop pipeline execution
- [ ] Users can monitor progress in real-time
- [ ] Complete audit trail of all operations