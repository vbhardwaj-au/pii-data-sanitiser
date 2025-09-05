using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository interface for managing pipeline stages
    /// </summary>
    public interface IPipelineStageRepository
    {
        /// <summary>
        /// Gets all pipeline stages ordered by execution order
        /// </summary>
        Task<IEnumerable<PipelineStage>> GetAllAsync();

        /// <summary>
        /// Gets all active pipeline stages ordered by execution order
        /// </summary>
        Task<IEnumerable<PipelineStage>> GetActiveAsync();

        /// <summary>
        /// Gets a specific pipeline stage by ID
        /// </summary>
        Task<PipelineStage?> GetByIdAsync(Guid id);

        /// <summary>
        /// Gets a pipeline stage by name
        /// </summary>
        Task<PipelineStage?> GetByNameAsync(string name);

        /// <summary>
        /// Creates a new pipeline stage
        /// </summary>
        Task<PipelineStage> CreateAsync(PipelineStage stage);

        /// <summary>
        /// Updates an existing pipeline stage
        /// </summary>
        Task<PipelineStage> UpdateAsync(PipelineStage stage);

        /// <summary>
        /// Deletes a pipeline stage
        /// </summary>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Checks if a pipeline stage exists by name
        /// </summary>
        Task<bool> ExistsAsync(string name, Guid? excludeId = null);
    }
}