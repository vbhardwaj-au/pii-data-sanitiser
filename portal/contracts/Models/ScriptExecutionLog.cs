using System.ComponentModel.DataAnnotations;

namespace Contracts.Models
{
    /// <summary>
    /// Represents an execution log entry for a pipeline stage script
    /// </summary>
    public class ScriptExecutionLog
    {
        /// <summary>
        /// Unique identifier for this log entry
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Script that was executed
        /// </summary>
        [Required]
        public Guid ScriptId { get; set; }

        /// <summary>
        /// Pipeline execution this log belongs to (optional, for grouping)
        /// </summary>
        public Guid? PipelineExecutionId { get; set; }

        /// <summary>
        /// When the script execution started
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the script execution completed (null if still running)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Execution status (Running, Completed, Failed, Cancelled)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Running";

        /// <summary>
        /// Number of rows affected by the script
        /// </summary>
        public long? RowsAffected { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Stack trace if execution failed
        /// </summary>
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// Actual SQL that was executed (may be different from script if templated)
        /// </summary>
        public string? ExecutedSql { get; set; }

        /// <summary>
        /// Execution duration in milliseconds
        /// </summary>
        public long? DurationMs { get; set; }

        /// <summary>
        /// Memory usage during execution in MB
        /// </summary>
        public decimal? MemoryUsageMB { get; set; }

        /// <summary>
        /// Connection string type used for execution
        /// </summary>
        [StringLength(50)]
        public string? ConnectionType { get; set; }

        /// <summary>
        /// Server/database where script was executed
        /// </summary>
        [StringLength(255)]
        public string? ExecutionTarget { get; set; }

        /// <summary>
        /// Additional execution metadata as JSON
        /// </summary>
        public string? ExecutionMetadata { get; set; }

        /// <summary>
        /// User who triggered this execution
        /// </summary>
        [StringLength(255)]
        public string? ExecutedBy { get; set; }

        /// <summary>
        /// Navigation property to the script
        /// </summary>
        public virtual PipelineStageScript? Script { get; set; }
    }

    /// <summary>
    /// Script execution status values
    /// </summary>
    public static class ScriptExecutionStatus
    {
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = { Running, Completed, Failed, Cancelled };

        public static bool IsValid(string status)
        {
            return Array.Exists(All, s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        }
    }
}