using Microsoft.Extensions.Logging;

namespace ObfuscationAgent.Services;

public interface IProgressReportingService
{
    event Action<ObfuscationProgress>? OnProgressUpdate;
    void ReportProgress(ObfuscationProgress progress);
}

public class ProgressReportingService : IProgressReportingService
{
    private readonly ILogger<ProgressReportingService> _logger;
    private DateTime _lastReportTime = DateTime.MinValue;
    private readonly TimeSpan _reportInterval = TimeSpan.FromSeconds(5);

    public event Action<ObfuscationProgress>? OnProgressUpdate;

    public ProgressReportingService(ILogger<ProgressReportingService> logger)
    {
        _logger = logger;
    }

    public void ReportProgress(ObfuscationProgress progress)
    {
        var now = DateTime.UtcNow;
        
        // Throttle progress reports to avoid overwhelming the portal
        if (now - _lastReportTime < _reportInterval)
        {
            return;
        }

        _lastReportTime = now;

        _logger.LogInformation(
            "Progress: {Progress:F1}% | Rows: {RowsProcessed:N0} | Table: {Table} | Speed: {Speed:N0} rows/sec",
            progress.ProgressPercentage,
            progress.RowsProcessed,
            progress.CurrentTable ?? "N/A",
            progress.RowsPerSecond ?? 0);

        OnProgressUpdate?.Invoke(progress);
    }
}

public class ObfuscationProgress
{
    public decimal ProgressPercentage { get; set; }
    public long RowsProcessed { get; set; }
    public long RowsAffected { get; set; }
    public string? CurrentTable { get; set; }
    public string? CurrentOperation { get; set; }
    public decimal? RowsPerSecond { get; set; }
    public long? EstimatedRemainingMs { get; set; }
}