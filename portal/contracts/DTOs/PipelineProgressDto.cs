namespace Contracts.DTOs
{
    /// <summary>
    /// DTO for pipeline execution progress information
    /// </summary>
    public class PipelineProgressDto
    {
        /// <summary>
        /// Unique identifier for this pipeline execution
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Current execution status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Overall progress percentage (0-100)
        /// </summary>
        public decimal ProgressPercentage { get; set; }

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
        /// Total rows processed across all stages
        /// </summary>
        public long? TotalRowsProcessed { get; set; }

        /// <summary>
        /// Total rows affected across all stages
        /// </summary>
        public long? TotalRowsAffected { get; set; }

        /// <summary>
        /// Error message if pipeline failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Progress information for individual stages
        /// </summary>
        public List<StageProgressDto> StageProgress { get; set; } = new();
    }

    /// <summary>
    /// DTO for stage execution progress information
    /// </summary>
    public class StageProgressDto
    {
        /// <summary>
        /// Unique identifier for this stage execution
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Stage name
        /// </summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>
        /// Execution order within the pipeline
        /// </summary>
        public int ExecutionOrder { get; set; }

        /// <summary>
        /// Current execution status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Progress percentage for this stage (0-100)
        /// </summary>
        public decimal ProgressPercentage { get; set; }

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
        /// Total number of scripts in this stage
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
        /// Error message if stage failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}