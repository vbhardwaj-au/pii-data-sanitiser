using System.ComponentModel.DataAnnotations;

namespace Contracts.Models
{
    /// <summary>
    /// Represents a pipeline stage definition
    /// </summary>
    public class PipelineStage
    {
        /// <summary>
        /// Unique identifier for the pipeline stage
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name of the pipeline stage
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display name for UI
        /// </summary>
        [Required]
        [StringLength(150)]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this stage does
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Order in which this stage should be executed (1-5)
        /// </summary>
        [Range(1, 5)]
        public int ExecutionOrder { get; set; }

        /// <summary>
        /// Whether this stage is required in all pipelines
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Whether this stage is currently active/available
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Default timeout for this stage in minutes
        /// </summary>
        [Range(1, 1440)] // 1 minute to 24 hours
        public int DefaultTimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Whether this stage supports parallel execution
        /// </summary>
        public bool SupportsParallelExecution { get; set; } = false;

        /// <summary>
        /// JSON configuration template for this stage
        /// </summary>
        public string? ConfigurationTemplate { get; set; }

        /// <summary>
        /// When this stage definition was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this stage definition was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property to product pipeline configurations
        /// </summary>
        public virtual ICollection<ProductPipelineConfiguration> ProductPipelineConfigurations { get; set; } = new List<ProductPipelineConfiguration>();

        /// <summary>
        /// Navigation property to pipeline stage scripts
        /// </summary>
        public virtual ICollection<PipelineStageScript> PipelineStageScripts { get; set; } = new List<PipelineStageScript>();
    }

    /// <summary>
    /// Standard pipeline stage names
    /// </summary>
    public static class PipelineStageNames
    {
        public const string DataReduction = "DataReduction";
        public const string PreObfuscation = "PreObfuscation";
        public const string Obfuscation = "Obfuscation";
        public const string PostObfuscation = "PostObfuscation";
        public const string Testing = "Testing";

        public static readonly string[] All = { DataReduction, PreObfuscation, Obfuscation, PostObfuscation, Testing };

        public static bool IsValid(string stageName)
        {
            return Array.Exists(All, s => string.Equals(s, stageName, StringComparison.OrdinalIgnoreCase));
        }
    }
}