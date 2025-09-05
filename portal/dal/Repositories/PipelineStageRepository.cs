using Microsoft.EntityFrameworkCore;
using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository implementation for managing pipeline stages
    /// </summary>
    public class PipelineStageRepository : IPipelineStageRepository
    {
        private readonly PortalDbContext _context;

        public PipelineStageRepository(PortalDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets all pipeline stages ordered by execution order
        /// </summary>
        public async Task<IEnumerable<PipelineStage>> GetAllAsync()
        {
            return await _context.PipelineStages
                .OrderBy(ps => ps.ExecutionOrder)
                .ThenBy(ps => ps.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Gets all active pipeline stages ordered by execution order
        /// </summary>
        public async Task<IEnumerable<PipelineStage>> GetActiveAsync()
        {
            return await _context.PipelineStages
                .Where(ps => ps.IsActive)
                .OrderBy(ps => ps.ExecutionOrder)
                .ThenBy(ps => ps.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a specific pipeline stage by ID
        /// </summary>
        public async Task<PipelineStage?> GetByIdAsync(Guid id)
        {
            return await _context.PipelineStages
                .FirstOrDefaultAsync(ps => ps.Id == id);
        }

        /// <summary>
        /// Gets a pipeline stage by name
        /// </summary>
        public async Task<PipelineStage?> GetByNameAsync(string name)
        {
            return await _context.PipelineStages
                .FirstOrDefaultAsync(ps => ps.Name.ToLower() == name.ToLower());
        }

        /// <summary>
        /// Creates a new pipeline stage
        /// </summary>
        public async Task<PipelineStage> CreateAsync(PipelineStage stage)
        {
            if (stage == null)
                throw new ArgumentNullException(nameof(stage));

            stage.CreatedAt = DateTime.UtcNow;
            stage.UpdatedAt = DateTime.UtcNow;

            _context.PipelineStages.Add(stage);
            await _context.SaveChangesAsync();

            return stage;
        }

        /// <summary>
        /// Updates an existing pipeline stage
        /// </summary>
        public async Task<PipelineStage> UpdateAsync(PipelineStage stage)
        {
            if (stage == null)
                throw new ArgumentNullException(nameof(stage));

            var existing = await _context.PipelineStages
                .FirstOrDefaultAsync(ps => ps.Id == stage.Id);

            if (existing == null)
                throw new InvalidOperationException($"Pipeline stage with ID {stage.Id} not found");

            // Update properties
            existing.Name = stage.Name;
            existing.DisplayName = stage.DisplayName;
            existing.Description = stage.Description;
            existing.ExecutionOrder = stage.ExecutionOrder;
            existing.IsRequired = stage.IsRequired;
            existing.IsActive = stage.IsActive;
            existing.DefaultTimeoutMinutes = stage.DefaultTimeoutMinutes;
            existing.SupportsParallelExecution = stage.SupportsParallelExecution;
            existing.ConfigurationTemplate = stage.ConfigurationTemplate;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return existing;
        }

        /// <summary>
        /// Deletes a pipeline stage
        /// </summary>
        public async Task<bool> DeleteAsync(Guid id)
        {
            var stage = await _context.PipelineStages
                .FirstOrDefaultAsync(ps => ps.Id == id);

            if (stage == null)
                return false;

            _context.PipelineStages.Remove(stage);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Checks if a pipeline stage exists by name
        /// </summary>
        public async Task<bool> ExistsAsync(string name, Guid? excludeId = null)
        {
            var query = _context.PipelineStages
                .Where(ps => ps.Name.ToLower() == name.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(ps => ps.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}