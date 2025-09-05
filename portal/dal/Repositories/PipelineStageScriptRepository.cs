using Microsoft.EntityFrameworkCore;
using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository implementation for managing pipeline stage scripts
    /// </summary>
    public class PipelineStageScriptRepository : IPipelineStageScriptRepository
    {
        private readonly PortalDbContext _context;

        public PipelineStageScriptRepository(PortalDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets all scripts for a specific product and pipeline stage
        /// </summary>
        public async Task<IEnumerable<PipelineStageScript>> GetByProductAndStageAsync(Guid productId, Guid pipelineStageId)
        {
            return await _context.PipelineStageScripts
                .Include(s => s.Product)
                .Include(s => s.PipelineStage)
                .Where(s => s.ProductId == productId && s.PipelineStageId == pipelineStageId)
                .OrderBy(s => s.ExecutionOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Gets all scripts for a specific product across all stages
        /// </summary>
        public async Task<IEnumerable<PipelineStageScript>> GetByProductIdAsync(Guid productId)
        {
            return await _context.PipelineStageScripts
                .Include(s => s.Product)
                .Include(s => s.PipelineStage)
                .Where(s => s.ProductId == productId)
                .OrderBy(s => s.PipelineStage!.ExecutionOrder)
                .ThenBy(s => s.ExecutionOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a specific script by ID
        /// </summary>
        public async Task<PipelineStageScript?> GetByIdAsync(Guid id)
        {
            return await _context.PipelineStageScripts
                .Include(s => s.Product)
                .Include(s => s.PipelineStage)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        /// <summary>
        /// Gets enabled scripts for a product and stage, ordered by execution order
        /// </summary>
        public async Task<IEnumerable<PipelineStageScript>> GetEnabledByProductAndStageAsync(Guid productId, Guid pipelineStageId)
        {
            return await _context.PipelineStageScripts
                .Include(s => s.Product)
                .Include(s => s.PipelineStage)
                .Where(s => s.ProductId == productId && 
                           s.PipelineStageId == pipelineStageId && 
                           s.IsEnabled &&
                           s.IsSecurityValidated)
                .OrderBy(s => s.ExecutionOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }

        /// <summary>
        /// Creates a new script
        /// </summary>
        public async Task<PipelineStageScript> CreateAsync(PipelineStageScript script)
        {
            if (script == null)
                throw new ArgumentNullException(nameof(script));

            script.CreatedAt = DateTime.UtcNow;
            script.UpdatedAt = DateTime.UtcNow;

            _context.PipelineStageScripts.Add(script);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(script.Id) ?? script;
        }

        /// <summary>
        /// Updates an existing script
        /// </summary>
        public async Task<PipelineStageScript> UpdateAsync(PipelineStageScript script)
        {
            if (script == null)
                throw new ArgumentNullException(nameof(script));

            var existing = await _context.PipelineStageScripts
                .FirstOrDefaultAsync(s => s.Id == script.Id);

            if (existing == null)
                throw new InvalidOperationException($"Script with ID {script.Id} not found");

            // Update properties
            existing.Name = script.Name;
            existing.Description = script.Description;
            existing.SqlScript = script.SqlScript;
            existing.ExecutionOrder = script.ExecutionOrder;
            existing.IsEnabled = script.IsEnabled;
            existing.TimeoutMinutes = script.TimeoutMinutes;
            existing.OnFailureAction = script.OnFailureAction;
            existing.RetryAttempts = script.RetryAttempts;
            existing.RetryDelaySeconds = script.RetryDelaySeconds;
            existing.ExpectedResultType = script.ExpectedResultType;
            existing.ExpectedMinRows = script.ExpectedMinRows;
            existing.ExpectedMaxRows = script.ExpectedMaxRows;
            existing.Tags = script.Tags;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = script.UpdatedBy;

            // Reset security validation when script changes
            existing.IsSecurityValidated = false;
            existing.SecurityValidationNotes = null;
            existing.LastValidatedAt = null;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(existing.Id) ?? existing;
        }

        /// <summary>
        /// Deletes a script
        /// </summary>
        public async Task<bool> DeleteAsync(Guid id)
        {
            var script = await _context.PipelineStageScripts
                .FirstOrDefaultAsync(s => s.Id == id);

            if (script == null)
                return false;

            _context.PipelineStageScripts.Remove(script);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Checks if a script with the same name exists for a product and stage
        /// </summary>
        public async Task<bool> ExistsAsync(Guid productId, Guid pipelineStageId, string name, Guid? excludeId = null)
        {
            var query = _context.PipelineStageScripts
                .Where(s => s.ProductId == productId && 
                           s.PipelineStageId == pipelineStageId && 
                           s.Name.ToLower() == name.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(s => s.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// Gets the next execution order number for a product and stage
        /// </summary>
        public async Task<int> GetNextExecutionOrderAsync(Guid productId, Guid pipelineStageId)
        {
            var maxOrder = await _context.PipelineStageScripts
                .Where(s => s.ProductId == productId && s.PipelineStageId == pipelineStageId)
                .MaxAsync(s => (int?)s.ExecutionOrder);

            return (maxOrder ?? 0) + 1;
        }

        /// <summary>
        /// Gets scripts that need security validation
        /// </summary>
        public async Task<IEnumerable<PipelineStageScript>> GetUnvalidatedScriptsAsync()
        {
            return await _context.PipelineStageScripts
                .Include(s => s.Product)
                .Include(s => s.PipelineStage)
                .Where(s => !s.IsSecurityValidated)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Updates the security validation status of a script
        /// </summary>
        public async Task UpdateSecurityValidationAsync(Guid scriptId, bool isValid, string? notes)
        {
            var script = await _context.PipelineStageScripts
                .FirstOrDefaultAsync(s => s.Id == scriptId);

            if (script == null)
                throw new InvalidOperationException($"Script with ID {scriptId} not found");

            script.IsSecurityValidated = isValid;
            script.SecurityValidationNotes = notes;
            script.LastValidatedAt = DateTime.UtcNow;
            script.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}