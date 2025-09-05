using System.ComponentModel.DataAnnotations;

namespace Contracts.Models
{
    /// <summary>
    /// Represents an execution instance of a product's pipeline
    /// </summary>
    public class PipelineExecution
    {
        /// <summary>
        /// Unique identifier for this pipeline execution
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Product this pipeline is executing for
        /// </summary>
        [Required]
        public Guid ProductId { get; set; }

        /// <summary>
        /// Execution status (Queued, Running, Completed, Failed, Cancelled, Paused)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = PipelineExecutionStatus.Queued;

        /// <summary>
        /// When the pipeline execution was queued
        /// </summary>
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the pipeline execution started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the pipeline execution completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Total duration in milliseconds
        /// </summary>
        public long? DurationMs { get; set; }

        /// <summary>
        /// Current stage being executed (1-5)
        /// </summary>
        public int? CurrentStage { get; set; }

        /// <summary>
        /// Total number of stages to execute
        /// </summary>
        public int TotalStages { get; set; } = 5;

        /// <summary>
        /// Number of stages completed successfully
        /// </summary>
        public int StagesCompleted { get; set; } = 0;

        /// <summary>
        /// Number of stages that failed
        /// </summary>
        public int StagesFailed { get; set; } = 0;

        /// <summary>
        /// Number of stages that were skipped
        /// </summary>
        public int StagesSkipped { get; set; } = 0;

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public decimal ProgressPercentage { get; set; } = 0;

        /// <summary>
        /// Error message if pipeline failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Stack trace if pipeline failed
        /// </summary>
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// User who initiated this pipeline execution
        /// </summary>
        [StringLength(255)]
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// Reason for cancellation (if cancelled)
        /// </summary>
        public string? CancellationReason { get; set; }

        /// <summary>
        /// Configuration snapshot at the time of execution (JSON)
        /// </summary>
        public string? ConfigurationSnapshot { get; set; }

        /// <summary>
        /// Execution parameters (JSON)
        /// </summary>
        public string? ExecutionParameters { get; set; }

        /// <summary>
        /// Connection string type used (Source, Target, etc.)
        /// </summary>
        [StringLength(50)]
        public string? ConnectionType { get; set; }

        /// <summary>
        /// Total rows processed across all stages
        /// </summary>
        public long? TotalRowsProcessed { get; set; }

        /// <summary>
        /// Total rows affected across all stages
        /// </summary>
        public long? TotalRowsAffected { get; set; }

        /// <summary>
        /// Execution priority (1-10, 1 being highest)
        /// </summary>
        [Range(1, 10)]
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Tags for categorizing executions
        /// </summary>
        [StringLength(500)]
        public string? Tags { get; set; }

        /// <summary>
        /// Whether this is a dry run execution
        /// </summary>
        public bool IsDryRun { get; set; } = false;

        /// <summary>
        /// Navigation property to the product
        /// </summary>
        public virtual Product? Product { get; set; }

        /// <summary>
        /// Navigation property to stage executions
        /// </summary>
        public virtual ICollection<StageExecution> StageExecutions { get; set; } = new List<StageExecution>();
    }

    /// <summary>
    /// Pipeline execution status values
    /// </summary>
    public static class PipelineExecutionStatus
    {
        public const string Queued = "Queued";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string Paused = "Paused";

        public static readonly string[] All = { Queued, Running, Completed, Failed, Cancelled, Paused };

        public static bool IsValid(string status)
        {
            return Array.Exists(All, s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsTerminal(string status)
        {
            return status == Completed || status == Failed || status == Cancelled;
        }

        public static bool CanTransitionTo(string currentStatus, string newStatus)
        {
            return (currentStatus, newStatus) switch
            {
                (Queued, Running) => true,
                (Queued, Cancelled) => true,
                (Running, Completed) => true,
                (Running, Failed) => true,
                (Running, Cancelled) => true,
                (Running, Paused) => true,
                (Paused, Running) => true,
                (Paused, Cancelled) => true,
                _ => false
            };
        }
    }
}