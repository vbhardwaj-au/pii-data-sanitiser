using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace ObfuscationAgent.Services;

public interface IObfuscationEngineService
{
    Task<ObfuscationResult> ExecuteObfuscationAsync(
        string connectionString, 
        string mappingConfig, 
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken);
}

public class ObfuscationEngineService : IObfuscationEngineService
{
    private readonly ILogger<ObfuscationEngineService> _logger;
    private readonly IProgressReportingService _progressReporting;
    private readonly string _obfuscationEnginePath;

    public ObfuscationEngineService(
        ILogger<ObfuscationEngineService> logger,
        IProgressReportingService progressReporting)
    {
        _logger = logger;
        _progressReporting = progressReporting;
        
        // Path to the obfuscation engine DLL
        var baseDllName = "DataObfuscation.dll";
        
        // Try different paths for the DLL
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data-obfuscation", baseDllName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data-obfuscation", "bin", "Release", "net8.0", baseDllName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "data-obfuscation", "bin", "Release", "net8.0", baseDllName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "data-obfuscation", "bin", "Debug", "net8.0", baseDllName)
        };

        _obfuscationEnginePath = possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    public async Task<ObfuscationResult> ExecuteObfuscationAsync(
        string connectionString,
        string mappingConfig,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        var result = new ObfuscationResult();
        var startTime = DateTime.UtcNow;

        try
        {
            // Save mapping config to temporary file
            var tempMappingFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempMappingFile, mappingConfig, cancellationToken);

            try
            {
                // Prepare command line arguments
                var arguments = $"\"{tempMappingFile}\"";
                
                // Add parameters if provided
                if (parameters != null)
                {
                    if (parameters.ContainsKey("dryRun") && (bool)parameters["dryRun"])
                    {
                        arguments += " --dry-run";
                    }
                    
                    if (parameters.ContainsKey("batchSize"))
                    {
                        arguments += $" --batch-size {parameters["batchSize"]}";
                    }
                    
                    if (parameters.ContainsKey("threads"))
                    {
                        arguments += $" --threads {parameters["threads"]}";
                    }
                }

                // Override connection string if provided
                if (!string.IsNullOrEmpty(connectionString))
                {
                    arguments += $" --connection \"{connectionString}\"";
                }

                _logger.LogInformation("Starting obfuscation engine with arguments: {Arguments}", arguments);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{_obfuscationEnginePath}\" {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogInformation("[Engine] {Output}", e.Data);
                        ParseProgressFromOutput(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogError("[Engine Error] {Error}", e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0)
                {
                    result.Success = true;
                    result.Message = "Obfuscation completed successfully";
                    
                    // Try to read the report file
                    var reportPath = Path.Combine(
                        Path.GetDirectoryName(_obfuscationEnginePath) ?? "",
                        "reports",
                        $"*-obfuscation-report.json"
                    );
                    
                    var reportFiles = Directory.GetFiles(
                        Path.GetDirectoryName(reportPath) ?? "", 
                        Path.GetFileName(reportPath));
                    
                    if (reportFiles.Length > 0)
                    {
                        var latestReport = reportFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                        var reportJson = await File.ReadAllTextAsync(latestReport, cancellationToken);
                        var report = JsonSerializer.Deserialize<ObfuscationReport>(reportJson);
                        
                        if (report != null)
                        {
                            result.TotalRowsProcessed = report.TotalRowsProcessed;
                            result.TotalRowsAffected = report.TotalRowsAffected;
                            result.TablesProcessed = report.TablesProcessed;
                            result.ColumnsObfuscated = report.ColumnsObfuscated;
                        }
                    }
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = $"Obfuscation engine exited with code {process.ExitCode}";
                }
            }
            finally
            {
                // Clean up temporary file
                try
                {
                    File.Delete(tempMappingFile);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing obfuscation");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private void ParseProgressFromOutput(string output)
    {
        try
        {
            // Parse progress from engine output
            // Example: "Progress: 45.2% | Rows: 123456 | Table: Customers | Speed: 1523 rows/sec"
            if (output.Contains("Progress:"))
            {
                var parts = output.Split('|');
                var progress = new ObfuscationProgress();

                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    
                    if (trimmed.StartsWith("Progress:"))
                    {
                        var percentStr = trimmed.Replace("Progress:", "").Replace("%", "").Trim();
                        if (decimal.TryParse(percentStr, out var percent))
                        {
                            progress.ProgressPercentage = percent;
                        }
                    }
                    else if (trimmed.StartsWith("Rows:"))
                    {
                        var rowsStr = trimmed.Replace("Rows:", "").Trim();
                        if (long.TryParse(rowsStr, out var rows))
                        {
                            progress.RowsProcessed = rows;
                        }
                    }
                    else if (trimmed.StartsWith("Table:"))
                    {
                        progress.CurrentTable = trimmed.Replace("Table:", "").Trim();
                    }
                    else if (trimmed.StartsWith("Speed:"))
                    {
                        var speedStr = trimmed.Replace("Speed:", "").Replace("rows/sec", "").Trim();
                        if (decimal.TryParse(speedStr, out var speed))
                        {
                            progress.RowsPerSecond = speed;
                        }
                    }
                }

                _progressReporting.ReportProgress(progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse progress from output");
        }
    }
}

public class ObfuscationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public long TotalRowsProcessed { get; set; }
    public long TotalRowsAffected { get; set; }
    public int TablesProcessed { get; set; }
    public int ColumnsObfuscated { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ObfuscationReport
{
    public long TotalRowsProcessed { get; set; }
    public long TotalRowsAffected { get; set; }
    public int TablesProcessed { get; set; }
    public int ColumnsObfuscated { get; set; }
}