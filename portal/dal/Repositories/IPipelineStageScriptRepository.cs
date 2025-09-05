using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository interface for managing pipeline stage scripts
    /// </summary>
    public interface IPipelineStageScriptRepository
    {
        /// <summary>
        /// Gets all scripts for a specific product and pipeline stage
        /// </summary>
        Task<IEnumerable<PipelineStageScript>> GetByProductAndStageAsync(Guid productId, Guid pipelineStageId);

        /// <summary>
        /// Gets all scripts for a specific product across all stages
        /// </summary>
        Task<IEnumerable<PipelineStageScript>> GetByProductIdAsync(Guid productId);

        /// <summary>
        /// Gets a specific script by ID
        /// </summary>
        Task<PipelineStageScript?> GetByIdAsync(Guid id);

        /// <summary>
        /// Gets enabled scripts for a product and stage, ordered by execution order
        /// </summary>
        Task<IEnumerable<PipelineStageScript>> GetEnabledByProductAndStageAsync(Guid productId, Guid pipelineStageId);

        /// <summary>
        /// Creates a new script
        /// </summary>
        Task<PipelineStageScript> CreateAsync(PipelineStageScript script);

        /// <summary>
        /// Updates an existing script
        /// </summary>
        Task<PipelineStageScript> UpdateAsync(PipelineStageScript script);

        /// <summary>
        /// Deletes a script
        /// </summary>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Checks if a script with the same name exists for a product and stage
        /// </summary>
        Task<bool> ExistsAsync(Guid productId, Guid pipelineStageId, string name, Guid? excludeId = null);

        /// <summary>
        /// Gets the next execution order number for a product and stage
        /// </summary>
        Task<int> GetNextExecutionOrderAsync(Guid productId, Guid pipelineStageId);

        /// <summary>
        /// Gets scripts that need security validation
        /// </summary>
        Task<IEnumerable<PipelineStageScript>> GetUnvalidatedScriptsAsync();

        /// <summary>
        /// Updates the security validation status of a script
        /// </summary>
        Task UpdateSecurityValidationAsync(Guid scriptId, bool isValid, string? notes);
    }
}