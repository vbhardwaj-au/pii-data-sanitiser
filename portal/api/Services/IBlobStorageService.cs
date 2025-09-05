using Contracts.DTOs;

namespace API.Services;

public interface IBlobStorageService
{
    /// <summary>
    /// Upload mapping file for a product to Azure Blob storage
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="mappingContent">JSON mapping content</param>
    /// <param name="version">Version identifier for the mapping</param>
    /// <returns>Blob URL for the uploaded file</returns>
    Task<string> UploadMappingAsync(Guid productId, string mappingContent, string? version = null);
    
    /// <summary>
    /// Download mapping file for a product from Azure Blob storage
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="version">Specific version to download (null for latest)</param>
    /// <returns>JSON mapping content</returns>
    Task<string?> DownloadMappingAsync(Guid productId, string? version = null);
    
    /// <summary>
    /// Get SAS URL for agent access to mapping file
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="version">Specific version (null for latest)</param>
    /// <param name="expiryHours">Hours until SAS token expires (default 24)</param>
    /// <returns>SAS URL for secure agent access</returns>
    Task<string> GetMappingSasUrlAsync(Guid productId, string? version = null, int expiryHours = 24);
    
    /// <summary>
    /// List all available mapping versions for a product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <returns>List of available versions with metadata</returns>
    Task<IEnumerable<MappingVersionDto>> ListMappingVersionsAsync(Guid productId);
    
    /// <summary>
    /// Delete a specific mapping version
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="version">Version to delete</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteMappingAsync(Guid productId, string version);
    
    /// <summary>
    /// Initialize blob containers if they don't exist
    /// </summary>
    Task InitializeContainersAsync();
    
    /// <summary>
    /// Upload obfuscation results/logs for a product execution
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="executionId">Execution identifier</param>
    /// <param name="fileName">Name of the file</param>
    /// <param name="content">File content</param>
    /// <returns>Blob URL for the uploaded file</returns>
    Task<string> UploadExecutionResultAsync(Guid productId, Guid executionId, string fileName, string content);
    
    /// <summary>
    /// Upload job execution results for a distributed agent job
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="jobId">Job identifier</param>
    /// <param name="resultsData">Results data content</param>
    /// <param name="fileName">Name of the file</param>
    /// <returns>Blob URL for the uploaded file</returns>
    Task<string> UploadJobResultsAsync(Guid productId, Guid jobId, string resultsData, string fileName);
}