namespace Contracts.DTOs
{
    /// <summary>
    /// DTO for stage execution information
    /// </summary>
    public class StageExecutionDto
    {
        /// <summary>
        /// Unique identifier for this stage execution
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Pipeline execution this stage belongs to
        /// </summary>
        public Guid PipelineExecutionId { get; set; }

        /// <summary>
        /// Pipeline stage being executed
        /// </summary>
        public Guid PipelineStageId { get; set; }

        /// <summary>
        /// Pipeline stage information
        /// </summary>
        public PipelineStageDto? PipelineStage { get; set; }

        /// <summary>
        /// Execution order within the pipeline
        /// </summary>
        public int ExecutionOrder { get; set; }

        /// <summary>
        /// Stage name
        /// </summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>
        /// Execution status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// When the stage execution was queued
        /// </summary>
        public DateTime QueuedAt { get; set; }

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
        public int TotalScripts { get; set; }

        /// <summary>
        /// Number of scripts completed successfully
        /// </summary>
        public int ScriptsCompleted { get; set; }

        /// <summary>
        /// Number of scripts that failed
        /// </summary>
        public int ScriptsFailed { get; set; }

        /// <summary>
        /// Number of scripts that were skipped
        /// </summary>
        public int ScriptsSkipped { get; set; }

        /// <summary>
        /// Progress percentage for this stage (0-100)
        /// </summary>
        public decimal ProgressPercentage { get; set; }

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
        /// Action taken on failure
        /// </summary>
        public string OnFailureAction { get; set; } = string.Empty;

        /// <summary>
        /// Number of retry attempts made
        /// </summary>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Maximum number of retries allowed
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Timeout for this stage in minutes
        /// </summary>
        public int TimeoutMinutes { get; set; }

        /// <summary>
        /// Whether this stage supports parallel execution
        /// </summary>
        public bool SupportsParallelExecution { get; set; }

        /// <summary>
        /// Whether this stage was executed in parallel
        /// </summary>
        public bool ExecutedInParallel { get; set; }

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
    }
}