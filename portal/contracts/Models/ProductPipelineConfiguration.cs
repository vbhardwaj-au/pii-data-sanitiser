using System.ComponentModel.DataAnnotations;

namespace Contracts.Models
{
    /// <summary>
    /// Represents a product's configuration for a specific pipeline stage
    /// </summary>
    public class ProductPipelineConfiguration
    {
        /// <summary>
        /// Unique identifier for this configuration
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Product that owns this configuration
        /// </summary>
        [Required]
        public Guid ProductId { get; set; }

        /// <summary>
        /// Pipeline stage this configuration applies to
        /// </summary>
        [Required]
        public Guid PipelineStageId { get; set; }

        /// <summary>
        /// Whether this stage is enabled for this product
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// JSON configuration specific to this product and stage
        /// </summary>
        public string? Configuration { get; set; }

        /// <summary>
        /// Timeout for this stage in minutes (overrides default)
        /// </summary>
        [Range(1, 1440)] // 1 minute to 24 hours
        public int? TimeoutMinutes { get; set; }

        /// <summary>
        /// Whether parallel execution is enabled for this stage
        /// </summary>
        public bool? ParallelExecutionEnabled { get; set; }

        /// <summary>
        /// Action to take when this stage fails (Stop, Skip, Warn)
        /// </summary>
        [StringLength(20)]
        public string OnFailureAction { get; set; } = "Stop";

        /// <summary>
        /// Number of retry attempts for this stage
        /// </summary>
        [Range(0, 10)]
        public int RetryAttempts { get; set; } = 0;

        /// <summary>
        /// Delay between retry attempts in seconds
        /// </summary>
        [Range(1, 3600)]
        public int RetryDelaySeconds { get; set; } = 30;

        /// <summary>
        /// Custom notes or instructions for this stage
        /// </summary>
        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>
        /// When this configuration was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this configuration was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User who created this configuration
        /// </summary>
        [StringLength(255)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User who last updated this configuration
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
    }

    /// <summary>
    /// Failure action options for pipeline stages
    /// </summary>
    public static class FailureActions
    {
        public const string Stop = "Stop";
        public const string Skip = "Skip";
        public const string Warn = "Warn";

        public static readonly string[] All = { Stop, Skip, Warn };

        public static bool IsValid(string action)
        {
            return Array.Exists(All, a => string.Equals(a, action, StringComparison.OrdinalIgnoreCase));
        }
    }
}