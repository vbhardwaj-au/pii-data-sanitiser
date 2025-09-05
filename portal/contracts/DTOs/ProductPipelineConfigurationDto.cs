namespace Contracts.DTOs
{
    /// <summary>
    /// DTO for product pipeline configuration
    /// </summary>
    public class ProductPipelineConfigurationDto
    {
        /// <summary>
        /// Unique identifier for this configuration
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Product that owns this configuration
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Product name for display
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Pipeline stage this configuration applies to
        /// </summary>
        public Guid PipelineStageId { get; set; }

        /// <summary>
        /// Pipeline stage information
        /// </summary>
        public PipelineStageDto? PipelineStage { get; set; }

        /// <summary>
        /// Whether this stage is enabled for this product
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// JSON configuration specific to this product and stage
        /// </summary>
        public string? Configuration { get; set; }

        /// <summary>
        /// Timeout for this stage in minutes (overrides default)
        /// </summary>
        public int? TimeoutMinutes { get; set; }

        /// <summary>
        /// Whether parallel execution is enabled for this stage
        /// </summary>
        public bool? ParallelExecutionEnabled { get; set; }

        /// <summary>
        /// Action to take when this stage fails (Stop, Skip, Warn)
        /// </summary>
        public string OnFailureAction { get; set; } = "Stop";

        /// <summary>
        /// Number of retry attempts for this stage
        /// </summary>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Delay between retry attempts in seconds
        /// </summary>
        public int RetryDelaySeconds { get; set; }

        /// <summary>
        /// Custom notes or instructions for this stage
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// When this configuration was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When this configuration was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// User who created this configuration
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User who last updated this configuration
        /// </summary>
        public string? UpdatedBy { get; set; }
    }
}