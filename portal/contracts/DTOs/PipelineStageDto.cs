namespace Contracts.DTOs
{
    /// <summary>
    /// DTO for pipeline stage information
    /// </summary>
    public class PipelineStageDto
    {
        /// <summary>
        /// Unique identifier for the pipeline stage
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name of the pipeline stage
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display name for UI
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this stage does
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Order in which this stage should be executed (1-5)
        /// </summary>
        public int ExecutionOrder { get; set; }

        /// <summary>
        /// Whether this stage is required in all pipelines
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Whether this stage is currently active/available
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Default timeout for this stage in minutes
        /// </summary>
        public int DefaultTimeoutMinutes { get; set; }

        /// <summary>
        /// Whether this stage supports parallel execution
        /// </summary>
        public bool SupportsParallelExecution { get; set; }

        /// <summary>
        /// JSON configuration template for this stage
        /// </summary>
        public string? ConfigurationTemplate { get; set; }

        /// <summary>
        /// When this stage definition was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When this stage definition was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}