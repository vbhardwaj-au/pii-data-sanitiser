using System.ComponentModel.DataAnnotations;

namespace Contracts.Models
{
    /// <summary>
    /// Represents the execution of a specific pipeline stage within a pipeline execution
    /// </summary>
    public class StageExecution
    {
        /// <summary>
        /// Unique identifier for this stage execution
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Pipeline execution this stage belongs to
        /// </summary>
        [Required]
        public Guid PipelineExecutionId { get; set; }

        /// <summary>
        /// Pipeline stage being executed
        /// </summary>
        [Required]
        public Guid PipelineStageId { get; set; }

        /// <summary>
        /// Execution order within the pipeline (1-5)
        /// </summary>
        [Range(1, 5)]
        public int ExecutionOrder { get; set; }

        /// <summary>
        /// Stage name for convenience
        /// </summary>
        [Required]
        [StringLength(100)]
        public string StageName { get; set; } = string.Empty;

        /// <summary>
        /// Execution status (Queued, Running, Completed, Failed, Cancelled, Skipped)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = StageExecutionStatus.Queued;

        /// <summary>
        /// When the stage execution was queued
        /// </summary>
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the stage execution started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the stage execution completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Duration in milliseconds
        /// </summary>
        public long? DurationMs { get; set; }

        /// <summary>
        /// Number of scripts in this stage
        /// </summary>
        public int TotalScripts { get; set; } = 0;

        /// <summary>
        /// Number of scripts completed successfully
        /// </summary>
        public int ScriptsCompleted { get; set; } = 0;

        /// <summary>
        /// Number of scripts that failed
        /// </summary>
        public int ScriptsFailed { get; set; } = 0;

        /// <summary>
        /// Number of scripts that were skipped
        /// </summary>
        public int ScriptsSkipped { get; set; } = 0;

        /// <summary>
        /// Progress percentage for this stage (0-100)
        /// </summary>
        public decimal ProgressPercentage { get; set; } = 0;

        /// <summary>
        /// Total rows processed in this stage
        /// </summary>
        public long? RowsProcessed { get; set; }

        /// <summary>
        /// Total rows affected in this stage
        /// </summary>
        public long? RowsAffected { get; set; }

        /// <summary>
        /// Error message if stage failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Stack trace if stage failed
        /// </summary>
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// Action taken on failure (Stop, Skip, Warn)
        /// </summary>
        [StringLength(20)]
        public string OnFailureAction { get; set; } = "Stop";

        /// <summary>
        /// Number of retry attempts made
        /// </summary>
        public int RetryAttempts { get; set; } = 0;

        /// <summary>
        /// Maximum number of retries allowed
        /// </summary>
        public int MaxRetries { get; set; } = 0;

        /// <summary>
        /// Timeout for this stage in minutes
        /// </summary>
        [Range(1, 1440)]
        public int TimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Whether this stage supports parallel execution
        /// </summary>
        public bool SupportsParallelExecution { get; set; } = false;

        /// <summary>
        /// Whether this stage was executed in parallel
        /// </summary>
        public bool ExecutedInParallel { get; set; } = false;

        /// <summary>
        /// Stage configuration at execution time (JSON)
        /// </summary>
        public string? ConfigurationSnapshot { get; set; }

        /// <summary>
        /// Execution metadata (JSON)
        /// </summary>
        public string? ExecutionMetadata { get; set; }

        /// <summary>
        /// Memory usage during execution in MB
        /// </summary>
        public decimal? MemoryUsageMB { get; set; }

        /// <summary>
        /// CPU usage percentage during execution
        /// </summary>
        public decimal? CpuUsagePercentage { get; set; }

        /// <summary>
        /// Stage output/result summary
        /// </summary>
        public string? OutputSummary { get; set; }

        /// <summary>
        /// Warnings generated during stage execution
        /// </summary>
        public string? Warnings { get; set; }

        /// <summary>
        /// Navigation property to the pipeline execution
        /// </summary>
        public virtual PipelineExecution? PipelineExecution { get; set; }

        /// <summary>
        /// Navigation property to the pipeline stage
        /// </summary>
        public virtual PipelineStage? PipelineStage { get; set; }
    }

    /// <summary>
    /// Stage execution status values
    /// </summary>
    public static class StageExecutionStatus
    {
        public const string Queued = "Queued";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string Skipped = "Skipped";

        public static readonly string[] All = { Queued, Running, Completed, Failed, Cancelled, Skipped };

        public static bool IsValid(string status)
        {
            return Array.Exists(All, s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsTerminal(string status)
        {
            return status == Completed || status == Failed || status == Cancelled || status == Skipped;
        }

        public static bool CanTransitionTo(string currentStatus, string newStatus)
        {
            return (currentStatus, newStatus) switch
            {
                (Queued, Running) => true,
                (Queued, Cancelled) => true,
                (Queued, Skipped) => true,
                (Running, Completed) => true,
                (Running, Failed) => true,
                (Running, Cancelled) => true,
                _ => false
            };
        }
    }
}