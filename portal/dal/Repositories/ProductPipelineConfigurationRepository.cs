using Microsoft.EntityFrameworkCore;
using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository implementation for managing product pipeline configurations
    /// </summary>
    public class ProductPipelineConfigurationRepository : IProductPipelineConfigurationRepository
    {
        private readonly PortalDbContext _context;

        public ProductPipelineConfigurationRepository(PortalDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets all pipeline configurations for a specific product
        /// </summary>
        public async Task<IEnumerable<ProductPipelineConfiguration>> GetByProductIdAsync(Guid productId)
        {
            return await _context.ProductPipelineConfigurations
                .Include(ppc => ppc.PipelineStage)
                .Include(ppc => ppc.Product)
                .Where(ppc => ppc.ProductId == productId)
                .OrderBy(ppc => ppc.PipelineStage!.ExecutionOrder)
                .ThenBy(ppc => ppc.PipelineStage!.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a specific configuration by ID
        /// </summary>
        public async Task<ProductPipelineConfiguration?> GetByIdAsync(Guid id)
        {
            return await _context.ProductPipelineConfigurations
                .Include(ppc => ppc.PipelineStage)
                .Include(ppc => ppc.Product)
                .FirstOrDefaultAsync(ppc => ppc.Id == id);
        }

        /// <summary>
        /// Gets configuration for a specific product and pipeline stage
        /// </summary>
        public async Task<ProductPipelineConfiguration?> GetByProductAndStageAsync(Guid productId, Guid pipelineStageId)
        {
            return await _context.ProductPipelineConfigurations
                .Include(ppc => ppc.PipelineStage)
                .Include(ppc => ppc.Product)
                .FirstOrDefaultAsync(ppc => ppc.ProductId == productId && ppc.PipelineStageId == pipelineStageId);
        }

        /// <summary>
        /// Gets all enabled configurations for a product, ordered by execution order
        /// </summary>
        public async Task<IEnumerable<ProductPipelineConfiguration>> GetEnabledByProductAsync(Guid productId)
        {
            return await _context.ProductPipelineConfigurations
                .Include(ppc => ppc.PipelineStage)
                .Include(ppc => ppc.Product)
                .Where(ppc => ppc.ProductId == productId && ppc.IsEnabled && ppc.PipelineStage!.IsActive)
                .OrderBy(ppc => ppc.PipelineStage!.ExecutionOrder)
                .ThenBy(ppc => ppc.PipelineStage!.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Creates a new pipeline configuration
        /// </summary>
        public async Task<ProductPipelineConfiguration> CreateAsync(ProductPipelineConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.CreatedAt = DateTime.UtcNow;
            configuration.UpdatedAt = DateTime.UtcNow;

            _context.ProductPipelineConfigurations.Add(configuration);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(configuration.Id) ?? configuration;
        }

        /// <summary>
        /// Updates an existing pipeline configuration
        /// </summary>
        public async Task<ProductPipelineConfiguration> UpdateAsync(ProductPipelineConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var existing = await _context.ProductPipelineConfigurations
                .FirstOrDefaultAsync(ppc => ppc.Id == configuration.Id);

            if (existing == null)
                throw new InvalidOperationException($"Pipeline configuration with ID {configuration.Id} not found");

            // Update properties
            existing.IsEnabled = configuration.IsEnabled;
            existing.Configuration = configuration.Configuration;
            existing.TimeoutMinutes = configuration.TimeoutMinutes;
            existing.ParallelExecutionEnabled = configuration.ParallelExecutionEnabled;
            existing.OnFailureAction = configuration.OnFailureAction;
            existing.RetryAttempts = configuration.RetryAttempts;
            existing.RetryDelaySeconds = configuration.RetryDelaySeconds;
            existing.Notes = configuration.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = configuration.UpdatedBy;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(existing.Id) ?? existing;
        }

        /// <summary>
        /// Deletes a pipeline configuration
        /// </summary>
        public async Task<bool> DeleteAsync(Guid id)
        {
            var configuration = await _context.ProductPipelineConfigurations
                .FirstOrDefaultAsync(ppc => ppc.Id == id);

            if (configuration == null)
                return false;

            _context.ProductPipelineConfigurations.Remove(configuration);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Checks if a configuration exists for a product and stage
        /// </summary>
        public async Task<bool> ExistsAsync(Guid productId, Guid pipelineStageId, Guid? excludeId = null)
        {
            var query = _context.ProductPipelineConfigurations
                .Where(ppc => ppc.ProductId == productId && ppc.PipelineStageId == pipelineStageId);

            if (excludeId.HasValue)
            {
                query = query.Where(ppc => ppc.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// Creates default configurations for a product based on pipeline stages
        /// </summary>
        public async Task CreateDefaultConfigurationsAsync(Guid productId)
        {
            // Get all active pipeline stages
            var stages = await _context.PipelineStages
                .Where(ps => ps.IsActive)
                .OrderBy(ps => ps.ExecutionOrder)
                .ToListAsync();

            var configurations = new List<ProductPipelineConfiguration>();

            foreach (var stage in stages)
            {
                // Check if configuration already exists
                var exists = await ExistsAsync(productId, stage.Id);
                if (exists)
                    continue;

                var configuration = new ProductPipelineConfiguration
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    PipelineStageId = stage.Id,
                    IsEnabled = stage.IsRequired, // Required stages are enabled by default
                    Configuration = stage.ConfigurationTemplate,
                    TimeoutMinutes = stage.DefaultTimeoutMinutes,
                    ParallelExecutionEnabled = stage.SupportsParallelExecution,
                    OnFailureAction = stage.IsRequired ? FailureActions.Stop : FailureActions.Skip,
                    RetryAttempts = 0,
                    RetryDelaySeconds = 30,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                configurations.Add(configuration);
            }

            if (configurations.Any())
            {
                await _context.ProductPipelineConfigurations.AddRangeAsync(configurations);
                await _context.SaveChangesAsync();
            }
        }
    }
}