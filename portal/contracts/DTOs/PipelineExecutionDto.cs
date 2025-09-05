namespace Contracts.DTOs
{
    /// <summary>
    /// DTO for pipeline execution information
    /// </summary>
    public class PipelineExecutionDto
    {
        /// <summary>
        /// Unique identifier for this pipeline execution
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Product this pipeline is executing for
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Product name for display
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Execution status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// When the pipeline execution was queued
        /// </summary>
        public DateTime QueuedAt { get; set; }

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
        public int TotalStages { get; set; }

        /// <summary>
        /// Number of stages completed successfully
        /// </summary>
        public int StagesCompleted { get; set; }

        /// <summary>
        /// Number of stages that failed
        /// </summary>
        public int StagesFailed { get; set; }

        /// <summary>
        /// Number of stages that were skipped
        /// </summary>
        public int StagesSkipped { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public decimal ProgressPercentage { get; set; }

        /// <summary>
        /// Error message if pipeline failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// User who initiated this pipeline execution
        /// </summary>
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// Connection string type used
        /// </summary>
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
        /// Execution priority
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Tags for categorizing executions
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// Whether this is a dry run execution
        /// </summary>
        public bool IsDryRun { get; set; }

        /// <summary>
        /// Stage executions within this pipeline
        /// </summary>
        public List<StageExecutionDto> StageExecutions { get; set; } = new();
    }
}