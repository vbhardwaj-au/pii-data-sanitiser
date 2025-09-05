using System.ComponentModel.DataAnnotations;

namespace Contracts.Models
{
    /// <summary>
    /// Represents a SQL script associated with a pipeline stage for a specific product
    /// </summary>
    public class PipelineStageScript
    {
        /// <summary>
        /// Unique identifier for this script
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Product that owns this script
        /// </summary>
        [Required]
        public Guid ProductId { get; set; }

        /// <summary>
        /// Pipeline stage this script belongs to
        /// </summary>
        [Required]
        public Guid PipelineStageId { get; set; }

        /// <summary>
        /// Name/title of the script
        /// </summary>
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this script does
        /// </summary>
        [StringLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// The SQL script content
        /// </summary>
        [Required]
        public string SqlScript { get; set; } = string.Empty;

        /// <summary>
        /// Order in which this script should be executed within the stage
        /// </summary>
        [Range(1, 999)]
        public int ExecutionOrder { get; set; } = 1;

        /// <summary>
        /// Whether this script is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Timeout for this script in minutes
        /// </summary>
        [Range(1, 1440)] // 1 minute to 24 hours
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Action to take when this script fails (Stop, Skip, Warn)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string OnFailureAction { get; set; } = "Stop";

        /// <summary>
        /// Number of retry attempts for this script
        /// </summary>
        [Range(0, 10)]
        public int RetryAttempts { get; set; } = 0;

        /// <summary>
        /// Delay between retry attempts in seconds
        /// </summary>
        [Range(1, 3600)]
        public int RetryDelaySeconds { get; set; } = 30;

        /// <summary>
        /// Whether this script has been validated by security checker
        /// </summary>
        public bool IsSecurityValidated { get; set; } = false;

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
        [StringLength(50)]
        public string ExpectedResultType { get; set; } = "None";

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
        [StringLength(500)]
        public string? Tags { get; set; }

        /// <summary>
        /// When this script was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this script was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User who created this script
        /// </summary>
        [StringLength(255)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User who last updated this script
        /// </summary>
        [StringLength(255)]
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Navigation property to the product
        /// </summary>
        public virtual Product? Product { get; set; }

        /// <summary>
        /// Navigation property to the pipeline stage
        /// </summary>
        public virtual PipelineStage? PipelineStage { get; set; }

        /// <summary>
        /// Navigation property to script execution logs
        /// </summary>
        public virtual ICollection<ScriptExecutionLog> ExecutionLogs { get; set; } = new List<ScriptExecutionLog>();
    }

    /// <summary>
    /// Expected result types for script validation
    /// </summary>
    public static class ScriptResultTypes
    {
        public const string None = "None";
        public const string RowCount = "RowCount";
        public const string Scalar = "Scalar";
        public const string ResultSet = "ResultSet";

        public static readonly string[] All = { None, RowCount, Scalar, ResultSet };

        public static bool IsValid(string resultType)
        {
            return Array.Exists(All, rt => string.Equals(rt, resultType, StringComparison.OrdinalIgnoreCase));
        }
    }
}