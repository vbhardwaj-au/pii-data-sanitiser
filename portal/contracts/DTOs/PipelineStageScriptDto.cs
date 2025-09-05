namespace Contracts.DTOs
{
    /// <summary>
    /// DTO for pipeline stage script information
    /// </summary>
    public class PipelineStageScriptDto
    {
        /// <summary>
        /// Unique identifier for this script
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Product that owns this script
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Product name for display
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Pipeline stage this script belongs to
        /// </summary>
        public Guid PipelineStageId { get; set; }

        /// <summary>
        /// Pipeline stage information
        /// </summary>
        public PipelineStageDto? PipelineStage { get; set; }

        /// <summary>
        /// Name/title of the script
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this script does
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The SQL script content
        /// </summary>
        public string SqlScript { get; set; } = string.Empty;

        /// <summary>
        /// Order in which this script should be executed within the stage
        /// </summary>
        public int ExecutionOrder { get; set; }

        /// <summary>
        /// Whether this script is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Timeout for this script in minutes
        /// </summary>
        public int TimeoutMinutes { get; set; }

        /// <summary>
        /// Action to take when this script fails (Stop, Skip, Warn)
        /// </summary>
        public string OnFailureAction { get; set; } = string.Empty;

        /// <summary>
        /// Number of retry attempts for this script
        /// </summary>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Delay between retry attempts in seconds
        /// </summary>
        public int RetryDelaySeconds { get; set; }

        /// <summary>
        /// Whether this script has been validated by security checker
        /// </summary>
        public bool IsSecurityValidated { get; set; }

        /// <summary>
        /// Security validation results/notes
        /// </summary>
        public string? SecurityValidationNotes { get; set; }

        /// <summary>
        /// When this script was last validated
        /// </summary>
        public DateTime? LastValidatedAt { get; set; }

        /// <summary>
        /// Expected result type (None, RowCount, Scalar, ResultSet)
        /// </summary>
        public string ExpectedResultType { get; set; } = string.Empty;

        /// <summary>
        /// Expected minimum rows affected/returned (for validation)
        /// </summary>
        public long? ExpectedMinRows { get; set; }

        /// <summary>
        /// Expected maximum rows affected/returned (for validation)
        /// </summary>
        public long? ExpectedMaxRows { get; set; }

        /// <summary>
        /// Tags for categorizing scripts
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// When this script was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When this script was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// User who created this script
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User who last updated this script
        /// </summary>
        public string? UpdatedBy { get; set; }
    }
}