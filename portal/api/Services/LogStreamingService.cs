using Microsoft.AspNetCore.SignalR;
using Contracts.Models;
using System.Collections.Concurrent;

namespace API.Services;

public interface ILogStreamingService
{
    Task StreamLogAsync(Guid jobId, string message, Contracts.Models.LogLevel level, Dictionary<string, object>? metadata = null);
    Task StreamProgressAsync(Guid jobId, decimal progressPercent, string stageName, long rowsProcessed = 0);
    Task StreamStageUpdateAsync(Guid jobId, string stageName, StageStatus status, string? errorMessage = null);
    Task BatchStreamLogsAsync(Guid jobId, IEnumerable<LogEntry> logs);
}

public class LogStreamingService : ILogStreamingService
{
    private readonly IHubContext<PipelineMonitoringHub> _hubContext;
    private readonly ILogger<LogStreamingService> _logger;
    private readonly ConcurrentDictionary<Guid, DateTime> _lastProgressUpdate = new();
    private readonly TimeSpan _progressThrottle = TimeSpan.FromMilliseconds(500);

    public LogStreamingService(
        IHubContext<PipelineMonitoringHub> hubContext,
        ILogger<LogStreamingService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task StreamLogAsync(Guid jobId, string message, Contracts.Models.LogLevel level, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var logEntry = new PipelineLogEntry
            {
                JobId = jobId,
                Timestamp = DateTime.UtcNow,
                Level = level.ToString(),
                Message = message,
                Metadata = metadata
            };

            await _hubContext.Clients.Group($"job-{jobId}").SendAsync("LogReceived", logEntry);
            
            // Also broadcast to monitoring dashboard
            await _hubContext.Clients.Group("monitoring").SendAsync("JobLogReceived", logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream log for job {JobId}", jobId);
        }
    }

    public async Task StreamProgressAsync(Guid jobId, decimal progressPercent, string stageName, long rowsProcessed = 0)
    {
        try
        {
            // Throttle progress updates to avoid overwhelming clients
            if (_lastProgressUpdate.TryGetValue(jobId, out var lastUpdate))
            {
                if (DateTime.UtcNow - lastUpdate < _progressThrottle && progressPercent < 100)
                {
                    return; // Skip this update
                }
            }

            _lastProgressUpdate[jobId] = DateTime.UtcNow;

            var progress = new ProgressUpdate
            {
                JobId = jobId,
                ProgressPercent = progressPercent,
                StageName = stageName,
                RowsProcessed = rowsProcessed,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"job-{jobId}").SendAsync("ProgressUpdate", progress);
            await _hubContext.Clients.Group("monitoring").SendAsync("JobProgressUpdate", progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream progress for job {JobId}", jobId);
        }
    }

    public async Task StreamStageUpdateAsync(Guid jobId, string stageName, StageStatus status, string? errorMessage = null)
    {
        try
        {
            var update = new StageUpdate
            {
                JobId = jobId,
                StageName = stageName,
                Status = status.ToString(),
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"job-{jobId}").SendAsync("StageUpdate", update);
            await _hubContext.Clients.Group("monitoring").SendAsync("JobStageUpdate", update);

            // Log stage transitions
            var logLevel = status == StageStatus.Failed ? Contracts.Models.LogLevel.Error : Contracts.Models.LogLevel.Information;
            var message = status switch
            {
                StageStatus.Started => $"Stage '{stageName}' started",
                StageStatus.Completed => $"Stage '{stageName}' completed successfully",
                StageStatus.Failed => $"Stage '{stageName}' failed: {errorMessage}",
                StageStatus.Skipped => $"Stage '{stageName}' skipped",
                _ => $"Stage '{stageName}' status: {status}"
            };

            await StreamLogAsync(jobId, message, logLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream stage update for job {JobId}", jobId);
        }
    }

    public async Task BatchStreamLogsAsync(Guid jobId, IEnumerable<LogEntry> logs)
    {
        try
        {
            var logBatch = new LogBatch
            {
                JobId = jobId,
                Logs = logs.Select(l => new PipelineLogEntry
                {
                    JobId = jobId,
                    Timestamp = l.Timestamp,
                    Level = l.Level,
                    Message = l.Message,
                    Metadata = l.Metadata
                }).ToList(),
                BatchTimestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"job-{jobId}").SendAsync("LogBatchReceived", logBatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream log batch for job {JobId}", jobId);
        }
    }
}

// Hub for real-time communication
public class PipelineMonitoringHub : Hub
{
    private readonly ILogger<PipelineMonitoringHub> _logger;

    public PipelineMonitoringHub(ILogger<PipelineMonitoringHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinJobMonitoring(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
        _logger.LogDebug("Client {ConnectionId} joined job monitoring for {JobId}", 
            Context.ConnectionId, jobId);
    }

    public async Task LeaveJobMonitoring(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job-{jobId}");
        _logger.LogDebug("Client {ConnectionId} left job monitoring for {JobId}", 
            Context.ConnectionId, jobId);
    }

    public async Task JoinGlobalMonitoring()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "monitoring");
        _logger.LogDebug("Client {ConnectionId} joined global monitoring", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

// DTOs for SignalR communication
public class PipelineLogEntry
{
    public Guid JobId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

public class ProgressUpdate
{
    public Guid JobId { get; set; }
    public decimal ProgressPercent { get; set; }
    public string StageName { get; set; } = string.Empty;
    public long RowsProcessed { get; set; }
    public DateTime Timestamp { get; set; }
}

public class StageUpdate
{
    public Guid JobId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}

public class LogBatch
{
    public Guid JobId { get; set; }
    public List<PipelineLogEntry> Logs { get; set; } = new();
    public DateTime BatchTimestamp { get; set; }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

public enum StageStatus
{
    Pending,
    Started,
    Running,
    Completed,
    Failed,
    Skipped
}