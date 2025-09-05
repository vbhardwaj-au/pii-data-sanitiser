namespace Contracts.DTOs
{
    /// <summary>
    /// DTO for script execution log information
    /// </summary>
    public class ScriptExecutionLogDto
    {
        /// <summary>
        /// Unique identifier for this log entry
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Script that was executed
        /// </summary>
        public Guid ScriptId { get; set; }

        /// <summary>
        /// Script information
        /// </summary>
        public PipelineStageScriptDto? Script { get; set; }

        /// <summary>
        /// Pipeline execution this log belongs to (optional)
        /// </summary>
        public Guid? PipelineExecutionId { get; set; }

        /// <summary>
        /// When the script execution started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// When the script execution completed (null if still running)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Execution status (Running, Completed, Failed, Cancelled)
        /// </summary>
        public string Status { get; set; } = string.Empty;

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
        /// Actual SQL that was executed
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
        public string? ConnectionType { get; set; }

        /// <summary>
        /// Server/database where script was executed
        /// </summary>
        public string? ExecutionTarget { get; set; }

        /// <summary>
        /// Additional execution metadata
        /// </summary>
        public Dictionary<string, object> ExecutionMetadata { get; set; } = new();

        /// <summary>
        /// User who triggered this execution
        /// </summary>
        public string? ExecutedBy { get; set; }
    }
}