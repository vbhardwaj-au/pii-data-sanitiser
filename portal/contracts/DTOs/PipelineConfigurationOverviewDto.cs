namespace Contracts.DTOs
{
    /// <summary>
    /// Complete overview of pipeline configuration for a product
    /// </summary>
    public class PipelineConfigurationOverviewDto
    {
        /// <summary>
        /// Product information
        /// </summary>
        public ProductDto Product { get; set; } = new();

        /// <summary>
        /// Pipeline stages with their configurations
        /// </summary>
        public List<PipelineStageConfigurationDto> Stages { get; set; } = new();

        /// <summary>
        /// Overall pipeline settings
        /// </summary>
        public PipelineSettingsDto Settings { get; set; } = new();

        /// <summary>
        /// Pipeline execution statistics
        /// </summary>
        public PipelineStatisticsDto Statistics { get; set; } = new();
    }

    /// <summary>
    /// Pipeline stage with its configuration and scripts
    /// </summary>
    public class PipelineStageConfigurationDto
    {
        /// <summary>
        /// Stage information
        /// </summary>
        public PipelineStageDto Stage { get; set; } = new();

        /// <summary>
        /// Product-specific configuration for this stage
        /// </summary>
        public ProductPipelineConfigurationDto? Configuration { get; set; }

        /// <summary>
        /// Scripts associated with this stage
        /// </summary>
        public List<PipelineStageScriptDto> Scripts { get; set; } = new();

        /// <summary>
        /// Whether this stage is enabled for the product
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Stage execution order
        /// </summary>
        public int ExecutionOrder { get; set; }
    }

    /// <summary>
    /// Overall pipeline settings
    /// </summary>
    public class PipelineSettingsDto
    {
        /// <summary>
        /// Estimated total duration in minutes
        /// </summary>
        public int EstimatedDurationMinutes { get; set; } = 0;

        /// <summary>
        /// Maximum retry count for failed stages
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// Whether to continue pipeline execution if a stage fails
        /// </summary>
        public bool ContinueOnFailure { get; set; } = false;

        /// <summary>
        /// Parallel execution settings
        /// </summary>
        public bool ParallelExecutionEnabled { get; set; } = false;

        /// <summary>
        /// Maximum concurrent stages when parallel execution is enabled
        /// </summary>
        public int MaxConcurrentStages { get; set; } = 1;
    }

    /// <summary>
    /// Pipeline execution statistics
    /// </summary>
    public class PipelineStatisticsDto
    {
        /// <summary>
        /// Total number of pipeline executions
        /// </summary>
        public int TotalExecutions { get; set; }

        /// <summary>
        /// Number of successful executions
        /// </summary>
        public int SuccessfulExecutions { get; set; }

        /// <summary>
        /// Number of failed executions
        /// </summary>
        public int FailedExecutions { get; set; }

        /// <summary>
        /// Average execution duration in minutes
        /// </summary>
        public double AverageExecutionMinutes { get; set; }

        /// <summary>
        /// Last execution date
        /// </summary>
        public DateTime? LastExecutionDate { get; set; }

        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
    }
}