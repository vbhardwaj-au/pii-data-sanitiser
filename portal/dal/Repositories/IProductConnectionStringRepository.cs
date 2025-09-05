using Contracts.Models;

namespace DAL.Repositories
{
    /// <summary>
    /// Repository interface for managing product connection strings
    /// </summary>
    public interface IProductConnectionStringRepository
    {
        /// <summary>
        /// Gets all connection strings for a specific product
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <returns>List of connection strings</returns>
        Task<IEnumerable<ProductConnectionString>> GetByProductIdAsync(Guid productId);

        /// <summary>
        /// Gets a specific connection string by ID
        /// </summary>
        /// <param name="id">The connection string ID</param>
        /// <returns>The connection string or null if not found</returns>
        Task<ProductConnectionString?> GetByIdAsync(Guid id);

        /// <summary>
        /// Gets a connection string by product ID and connection type
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <param name="connectionType">The connection type (Source, Target, Backup)</param>
        /// <returns>The connection string or null if not found</returns>
        Task<ProductConnectionString?> GetByProductIdAndTypeAsync(Guid productId, string connectionType);

        /// <summary>
        /// Creates a new connection string
        /// </summary>
        /// <param name="connectionString">The connection string to create</param>
        /// <returns>The created connection string</returns>
        Task<ProductConnectionString> CreateAsync(ProductConnectionString connectionString);

        /// <summary>
        /// Updates an existing connection string
        /// </summary>
        /// <param name="connectionString">The connection string to update</param>
        /// <returns>The updated connection string</returns>
        Task<ProductConnectionString> UpdateAsync(ProductConnectionString connectionString);

        /// <summary>
        /// Deletes a connection string
        /// </summary>
        /// <param name="id">The connection string ID to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Checks if a connection string exists for a product and type
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <param name="connectionType">The connection type</param>
        /// <param name="excludeId">Optional ID to exclude from the check (for updates)</param>
        /// <returns>True if exists, false otherwise</returns>
        Task<bool> ExistsAsync(Guid productId, string connectionType, Guid? excludeId = null);

        /// <summary>
        /// Gets all active connection strings
        /// </summary>
        /// <returns>List of active connection strings</returns>
        Task<IEnumerable<ProductConnectionString>> GetActiveAsync();

        /// <summary>
        /// Gets connection strings with product information
        /// </summary>
        /// <param name="productId">Optional product ID filter</param>
        /// <returns>Connection strings with product details</returns>
        Task<IEnumerable<ProductConnectionString>> GetWithProductDetailsAsync(Guid? productId = null);
    }
}