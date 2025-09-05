using Microsoft.EntityFrameworkCore;
using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository implementation for managing product connection strings
    /// </summary>
    public class ProductConnectionStringRepository : IProductConnectionStringRepository
    {
        private readonly PortalDbContext _context;

        public ProductConnectionStringRepository(PortalDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets all connection strings for a specific product
        /// </summary>
        public async Task<IEnumerable<ProductConnectionString>> GetByProductIdAsync(Guid productId)
        {
            return await _context.ProductConnectionStrings
                .Where(cs => cs.ProductId == productId)
                .OrderBy(cs => cs.ConnectionType)
                .ThenBy(cs => cs.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a specific connection string by ID
        /// </summary>
        public async Task<ProductConnectionString?> GetByIdAsync(Guid id)
        {
            return await _context.ProductConnectionStrings
                .Include(cs => cs.Product)
                .FirstOrDefaultAsync(cs => cs.Id == id);
        }

        /// <summary>
        /// Gets a connection string by product ID and connection type
        /// </summary>
        public async Task<ProductConnectionString?> GetByProductIdAndTypeAsync(Guid productId, string connectionType)
        {
            return await _context.ProductConnectionStrings
                .Include(cs => cs.Product)
                .FirstOrDefaultAsync(cs => cs.ProductId == productId && 
                                         cs.ConnectionType.ToLower() == connectionType.ToLower());
        }

        /// <summary>
        /// Creates a new connection string
        /// </summary>
        public async Task<ProductConnectionString> CreateAsync(ProductConnectionString connectionString)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            // Set timestamps
            connectionString.CreatedAt = DateTime.UtcNow;
            connectionString.UpdatedAt = DateTime.UtcNow;

            _context.ProductConnectionStrings.Add(connectionString);
            await _context.SaveChangesAsync();

            // Return with product details
            return await GetByIdAsync(connectionString.Id) ?? connectionString;
        }

        /// <summary>
        /// Updates an existing connection string
        /// </summary>
        public async Task<ProductConnectionString> UpdateAsync(ProductConnectionString connectionString)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            var existing = await _context.ProductConnectionStrings
                .FirstOrDefaultAsync(cs => cs.Id == connectionString.Id);

            if (existing == null)
                throw new InvalidOperationException($"Connection string with ID {connectionString.Id} not found");

            // Update properties
            existing.ConnectionType = connectionString.ConnectionType;
            existing.ConnectionString = connectionString.ConnectionString;
            existing.Description = connectionString.Description;
            existing.IsActive = connectionString.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = connectionString.UpdatedBy;

            await _context.SaveChangesAsync();

            // Return with product details
            return await GetByIdAsync(existing.Id) ?? existing;
        }

        /// <summary>
        /// Deletes a connection string
        /// </summary>
        public async Task<bool> DeleteAsync(Guid id)
        {
            var connectionString = await _context.ProductConnectionStrings
                .FirstOrDefaultAsync(cs => cs.Id == id);

            if (connectionString == null)
                return false;

            _context.ProductConnectionStrings.Remove(connectionString);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Checks if a connection string exists for a product and type
        /// </summary>
        public async Task<bool> ExistsAsync(Guid productId, string connectionType, Guid? excludeId = null)
        {
            var query = _context.ProductConnectionStrings
                .Where(cs => cs.ProductId == productId && 
                           cs.ConnectionType.ToLower() == connectionType.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(cs => cs.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// Gets all active connection strings
        /// </summary>
        public async Task<IEnumerable<ProductConnectionString>> GetActiveAsync()
        {
            return await _context.ProductConnectionStrings
                .Include(cs => cs.Product)
                .Where(cs => cs.IsActive)
                .OrderBy(cs => cs.Product!.Name)
                .ThenBy(cs => cs.ConnectionType)
                .ToListAsync();
        }

        /// <summary>
        /// Gets connection strings with product information
        /// </summary>
        public async Task<IEnumerable<ProductConnectionString>> GetWithProductDetailsAsync(Guid? productId = null)
        {
            var query = _context.ProductConnectionStrings
                .Include(cs => cs.Product)
                .AsQueryable();

            if (productId.HasValue)
            {
                query = query.Where(cs => cs.ProductId == productId.Value);
            }

            return await query
                .OrderBy(cs => cs.Product!.Name)
                .ThenBy(cs => cs.ConnectionType)
                .ToListAsync();
        }
    }
}