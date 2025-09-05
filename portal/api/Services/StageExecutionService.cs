using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using DAL;
using DAL.Repositories;
using Contracts.Models;
using Contracts.DTOs;

namespace API.Services;

public class StageExecutionService : IStageExecutionService
{
    private readonly PortalDbContext _context;
    private readonly IPipelineStageScriptRepository _scriptRepository;
    private readonly IScriptExecutionLoggingService _loggingService;
    private readonly IConnectionStringEncryptionService _encryptionService;
    private readonly ISqlScriptValidatorService _validatorService;
    private readonly ILogger<StageExecutionService> _logger;

    public StageExecutionService(
        PortalDbContext context,
        IPipelineStageScriptRepository scriptRepository,
        IScriptExecutionLoggingService loggingService,
        IConnectionStringEncryptionService encryptionService,
        ISqlScriptValidatorService validatorService,
        ILogger<StageExecutionService> logger)
    {
        _context = context;
        _scriptRepository = scriptRepository;
        _loggingService = loggingService;
        _encryptionService = encryptionService;
        _validatorService = validatorService;
        _logger = logger;
    }

    public async Task<StageExecutionDto> ExecuteStageAsync(Guid stageExecutionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting execution of stage {StageExecutionId}", stageExecutionId);

        var stageExecution = await _context.StageExecutions
            .Include(se => se.PipelineStage)
            .Include(se => se.PipelineExecution)
                .ThenInclude(pe => pe!.Product)
            .FirstOrDefaultAsync(se => se.Id == stageExecutionId, cancellationToken);

        if (stageExecution == null)
        {
            throw new InvalidOperationException($"Stage execution {stageExecutionId} not found");
        }

        // Check retry logic - if this is a retry attempt
        if (stageExecution.Status == StageExecutionStatus.Failed && stageExecution.RetryAttempts < stageExecution.MaxRetries)
        {
            _logger.LogInformation("Retrying stage {StageName} - attempt {CurrentAttempt}/{MaxRetries}", 
                stageExecution.StageName, stageExecution.RetryAttempts + 1, stageExecution.MaxRetries);
            
            stageExecution.RetryAttempts++;
            stageExecution.Status = StageExecutionStatus.Running;
            stageExecution.ErrorMessage = null;
            stageExecution.ErrorStackTrace = null;
            await _context.SaveChangesAsync(cancellationToken);
        }
        else if (!await ValidateStagePrerequisitesAsync(stageExecutionId))
        {
            throw new InvalidOperationException($"Stage execution {stageExecutionId} prerequisites not met");
        }

        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;
        long totalRowsProcessed = 0;
        long totalRowsAffected = 0;

        try
        {
            stageExecution.Status = StageExecutionStatus.Running;
            stageExecution.StartedAt = startTime;
            await _context.SaveChangesAsync(cancellationToken);

            var scriptIds = await GetStageScriptsAsync(stageExecution.PipelineExecution!.ProductId, stageExecution.PipelineStageId);
            var scripts = scriptIds.ToList();
            
            stageExecution.TotalScripts = scripts.Count;
            await _context.SaveChangesAsync(cancellationToken);

            var connectionString = await GetConnectionStringAsync(stageExecution.PipelineExecution.ProductId);
            
            _logger.LogInformation("Stage {StageName} has {ScriptCount} scripts to execute", 
                stageExecution.StageName, scripts.Count);

            int completedScripts = 0;
            int failedScripts = 0;

            foreach (var scriptId in scripts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Stage execution {StageExecutionId} cancelled", stageExecutionId);
                    break;
                }

                try
                {
                    var scriptExecuted = await ExecuteScriptAsync(scriptId, stageExecutionId, connectionString);
                    
                    if (scriptExecuted)
                    {
                        completedScripts++;
                        totalRowsProcessed += 100; // Placeholder - actual implementation would track real metrics
                        totalRowsAffected += 50;   // Placeholder
                    }
                    else
                    {
                        failedScripts++;
                        
                        var script = await _scriptRepository.GetByIdAsync(scriptId);
                        if (script?.OnFailureAction == "Stop")
                        {
                            _logger.LogError("Critical script failed for stage {StageName}, stopping execution", stageExecution.StageName);
                            break;
                        }
                    }

                    stageExecution.ScriptsCompleted = completedScripts;
                    stageExecution.ScriptsFailed = failedScripts;
                    stageExecution.ProgressPercentage = (decimal)(completedScripts + failedScripts) / scripts.Count * 100;
                    
                    await _context.SaveChangesAsync(cancellationToken);

                    // Update progress metrics
                    await UpdateStageMetricsAsync(stageExecutionId, totalRowsProcessed, totalRowsAffected, 
                        GetMemoryUsage(), GetCpuUsage());
                }
                catch (Exception scriptEx)
                {
                    _logger.LogError(scriptEx, "Failed to execute script {ScriptId} in stage {StageExecutionId}", scriptId, stageExecutionId);
                    failedScripts++;
                    stageExecution.ScriptsFailed = failedScripts;
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            stopwatch.Stop();
            
            var finalStatus = failedScripts > 0 && completedScripts == 0 
                ? StageExecutionStatus.Failed 
                : StageExecutionStatus.Completed;

            stageExecution.Status = finalStatus;
            stageExecution.CompletedAt = DateTime.UtcNow;
            stageExecution.DurationMs = stopwatch.ElapsedMilliseconds;
            stageExecution.RowsProcessed = totalRowsProcessed;
            stageExecution.RowsAffected = totalRowsAffected;
            stageExecution.ProgressPercentage = 100;

            if (failedScripts > 0)
            {
                stageExecution.ErrorMessage = $"{failedScripts} out of {scripts.Count} scripts failed";
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Stage {StageName} completed with status {Status}. Duration: {Duration}ms, Scripts: {Completed}/{Total}",
                stageExecution.StageName, finalStatus, stopwatch.ElapsedMilliseconds, completedScripts, scripts.Count);

            return MapToStageExecutionDto(stageExecution);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Stage execution {StageExecutionId} failed", stageExecutionId);

            stageExecution.Status = StageExecutionStatus.Failed;
            stageExecution.CompletedAt = DateTime.UtcNow;
            stageExecution.DurationMs = stopwatch.ElapsedMilliseconds;
            stageExecution.ErrorMessage = ex.Message;
            stageExecution.ErrorStackTrace = ex.StackTrace;

            await _context.SaveChangesAsync(cancellationToken);

            return MapToStageExecutionDto(stageExecution);
        }
    }

    public async Task<bool> ValidateStagePrerequisitesAsync(Guid stageExecutionId)
    {
        var stageExecution = await _context.StageExecutions
            .Include(se => se.PipelineExecution)
                .ThenInclude(pe => pe!.StageExecutions)
            .FirstOrDefaultAsync(se => se.Id == stageExecutionId);

        if (stageExecution == null)
        {
            return false;
        }

        // Check if previous stages (lower execution order) are completed
        var previousStages = stageExecution.PipelineExecution!.StageExecutions
            .Where(se => se.ExecutionOrder < stageExecution.ExecutionOrder)
            .ToList();

        foreach (var previousStage in previousStages)
        {
            if (previousStage.Status != StageExecutionStatus.Completed && previousStage.Status != StageExecutionStatus.Skipped)
            {
                _logger.LogWarning("Stage {StageName} cannot start - prerequisite stage {PrereqStage} has status {Status}",
                    stageExecution.StageName, previousStage.StageName, previousStage.Status);
                return false;
            }
        }

        return true;
    }

    public async Task<IEnumerable<Guid>> GetStageScriptsAsync(Guid productId, Guid stageId)
    {
        var scripts = await _scriptRepository.GetByProductAndStageAsync(productId, stageId);
        return scripts
            .Where(s => s.IsEnabled && s.IsSecurityValidated)
            .OrderBy(s => s.ExecutionOrder)
            .Select(s => s.Id);
    }

    public async Task<bool> ExecuteScriptAsync(Guid scriptId, Guid stageExecutionId, string connectionString)
    {
        var script = await _scriptRepository.GetByIdAsync(scriptId);
        if (script == null)
        {
            _logger.LogError("Script {ScriptId} not found", scriptId);
            return false;
        }

        _logger.LogInformation("Executing script {ScriptName} for stage execution {StageExecutionId}", 
            script.Name, stageExecutionId);

        var logId = await _loggingService.StartExecutionLogAsync(scriptId, null, "System", "Default");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate script before execution
            var validationResult = await _validatorService.ValidateScriptAsync(script.SqlScript);
            if (!validationResult.IsValid)
            {
                var errorMessage = string.Join("; ", validationResult.SecurityIssues.Select(si => si.Description));
                await _loggingService.CompleteExecutionLogAsync(logId, "Failed", null, errorMessage);
                return false;
            }

            // Execute the script
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(script.SqlScript, connection)
            {
                CommandTimeout = script.TimeoutMinutes * 60
            };

            var rowsAffected = await command.ExecuteNonQueryAsync();

            stopwatch.Stop();
            await _loggingService.CompleteExecutionLogAsync(logId, "Completed", rowsAffected);

            _logger.LogInformation("Script {ScriptName} executed successfully. Rows affected: {RowsAffected}, Duration: {Duration}ms",
                script.Name, rowsAffected, stopwatch.ElapsedMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to execute script {ScriptName}", script.Name);

            await _loggingService.CompleteExecutionLogAsync(logId, "Failed", null, ex.Message, ex.StackTrace);
            return false;
        }
    }

    public async Task UpdateStageMetricsAsync(Guid stageExecutionId, long rowsProcessed, long rowsAffected, decimal memoryUsageMB, decimal cpuUsagePercentage)
    {
        var stageExecution = await _context.StageExecutions.FirstOrDefaultAsync(se => se.Id == stageExecutionId);
        if (stageExecution != null)
        {
            stageExecution.RowsProcessed = rowsProcessed;
            stageExecution.RowsAffected = rowsAffected;
            stageExecution.MemoryUsageMB = memoryUsageMB;
            stageExecution.CpuUsagePercentage = cpuUsagePercentage;
            
            await _context.SaveChangesAsync();
        }
    }

    private async Task<string> GetConnectionStringAsync(Guid productId)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null)
        {
            throw new InvalidOperationException($"Product {productId} not found");
        }

        // For now, return the connection string from Product. In a real implementation,
        // we might get it from ProductConnectionStrings table and decrypt it
        return product.ConnectionString;
    }

    private static decimal GetMemoryUsage()
    {
        // Simplified memory usage calculation
        var process = Process.GetCurrentProcess();
        return (decimal)(process.WorkingSet64 / (1024.0 * 1024.0)); // Convert to MB
    }

    private static decimal GetCpuUsage()
    {
        // Placeholder for CPU usage - in a real implementation, this would require more sophisticated monitoring
        return 0.0m;
    }

    private static StageExecutionDto MapToStageExecutionDto(StageExecution stageExecution)
    {
        return new StageExecutionDto
        {
            Id = stageExecution.Id,
            PipelineExecutionId = stageExecution.PipelineExecutionId,
            PipelineStageId = stageExecution.PipelineStageId,
            PipelineStage = stageExecution.PipelineStage != null ? new PipelineStageDto
            {
                Id = stageExecution.PipelineStage.Id,
                Name = stageExecution.PipelineStage.Name,
                DisplayName = stageExecution.PipelineStage.DisplayName,
                Description = stageExecution.PipelineStage.Description,
                ExecutionOrder = stageExecution.PipelineStage.ExecutionOrder,
                IsActive = stageExecution.PipelineStage.IsActive,
                IsRequired = stageExecution.PipelineStage.IsRequired
            } : null,
            ExecutionOrder = stageExecution.ExecutionOrder,
            StageName = stageExecution.StageName,
            Status = stageExecution.Status,
            QueuedAt = stageExecution.QueuedAt,
            StartedAt = stageExecution.StartedAt,
            CompletedAt = stageExecution.CompletedAt,
            DurationMs = stageExecution.DurationMs,
            TotalScripts = stageExecution.TotalScripts,
            ScriptsCompleted = stageExecution.ScriptsCompleted,
            ScriptsFailed = stageExecution.ScriptsFailed,
            ScriptsSkipped = stageExecution.ScriptsSkipped,
            ProgressPercentage = stageExecution.ProgressPercentage,
            RowsProcessed = stageExecution.RowsProcessed,
            RowsAffected = stageExecution.RowsAffected,
            ErrorMessage = stageExecution.ErrorMessage,
            OnFailureAction = stageExecution.OnFailureAction,
            RetryAttempts = stageExecution.RetryAttempts,
            MaxRetries = stageExecution.MaxRetries,
            TimeoutMinutes = stageExecution.TimeoutMinutes,
            SupportsParallelExecution = stageExecution.SupportsParallelExecution,
            ExecutedInParallel = stageExecution.ExecutedInParallel,
            MemoryUsageMB = stageExecution.MemoryUsageMB,
            CpuUsagePercentage = stageExecution.CpuUsagePercentage,
            OutputSummary = stageExecution.OutputSummary,
            Warnings = stageExecution.Warnings
        };
    }
}