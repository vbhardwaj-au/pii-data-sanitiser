using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository interface for managing product pipeline configurations
    /// </summary>
    public interface IProductPipelineConfigurationRepository
    {
        /// <summary>
        /// Gets all pipeline configurations for a specific product
        /// </summary>
        Task<IEnumerable<ProductPipelineConfiguration>> GetByProductIdAsync(Guid productId);

        /// <summary>
        /// Gets a specific configuration by ID
        /// </summary>
        Task<ProductPipelineConfiguration?> GetByIdAsync(Guid id);

        /// <summary>
        /// Gets configuration for a specific product and pipeline stage
        /// </summary>
        Task<ProductPipelineConfiguration?> GetByProductAndStageAsync(Guid productId, Guid pipelineStageId);

        /// <summary>
        /// Gets all enabled configurations for a product, ordered by execution order
        /// </summary>
        Task<IEnumerable<ProductPipelineConfiguration>> GetEnabledByProductAsync(Guid productId);

        /// <summary>
        /// Creates a new pipeline configuration
        /// </summary>
        Task<ProductPipelineConfiguration> CreateAsync(ProductPipelineConfiguration configuration);

        /// <summary>
        /// Updates an existing pipeline configuration
        /// </summary>
        Task<ProductPipelineConfiguration> UpdateAsync(ProductPipelineConfiguration configuration);

        /// <summary>
        /// Deletes a pipeline configuration
        /// </summary>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Checks if a configuration exists for a product and stage
        /// </summary>
        Task<bool> ExistsAsync(Guid productId, Guid pipelineStageId, Guid? excludeId = null);

        /// <summary>
        /// Creates default configurations for a product based on pipeline stages
        /// </summary>
        Task CreateDefaultConfigurationsAsync(Guid productId);
    }
}