using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts.DTOs;

namespace API.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly IConfiguration _configuration;
    
    private const string MappingsContainerName = "product-mappings";
    private const string ResultsContainerName = "execution-results";

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var connectionString = _configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string not configured");
        }
        
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task InitializeContainersAsync()
    {
        try
        {
            // Create mappings container
            var mappingsContainer = _blobServiceClient.GetBlobContainerClient(MappingsContainerName);
            await mappingsContainer.CreateIfNotExistsAsync(PublicAccessType.None);
            
            // Create results container  
            var resultsContainer = _blobServiceClient.GetBlobContainerClient(ResultsContainerName);
            await resultsContainer.CreateIfNotExistsAsync(PublicAccessType.None);
            
            _logger.LogInformation("Azure Blob containers initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Blob containers");
            throw;
        }
    }

    public async Task<string> UploadMappingAsync(Guid productId, string mappingContent, string? version = null)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(MappingsContainerName);
            
            // Generate version if not provided
            if (string.IsNullOrEmpty(version))
            {
                version = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            }
            
            // Create blob name: {productId}/{version}/mapping.json
            var blobName = $"{productId:D}/{version}/mapping.json";
            var blobClient = containerClient.GetBlobClient(blobName);
            
            // Calculate content hash for integrity
            var contentBytes = Encoding.UTF8.GetBytes(mappingContent);
            var contentHash = Convert.ToBase64String(MD5.HashData(contentBytes));
            
            // Set metadata
            var metadata = new Dictionary<string, string>
            {
                ["ProductId"] = productId.ToString(),
                ["Version"] = version,
                ["CreatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["ContentHash"] = contentHash,
                ["CreatedBy"] = "System" // TODO: Get from current user context
            };
            
            // Upload with metadata
            using var stream = new MemoryStream(contentBytes);
            var uploadOptions = new BlobUploadOptions
            {
                Metadata = metadata,
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json"
                }
            };
            
            var response = await blobClient.UploadAsync(stream, uploadOptions);
            
            _logger.LogInformation("Uploaded mapping for product {ProductId}, version {Version} to blob {BlobName}", 
                productId, version, blobName);
            
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload mapping for product {ProductId}, version {Version}", 
                productId, version);
            throw;
        }
    }

    public async Task<string?> DownloadMappingAsync(Guid productId, string? version = null)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(MappingsContainerName);
            
            string blobName;
            if (string.IsNullOrEmpty(version))
            {
                // Get latest version
                var latestVersion = await GetLatestVersionAsync(productId);
                if (string.IsNullOrEmpty(latestVersion))
                {
                    _logger.LogWarning("No mapping versions found for product {ProductId}", productId);
                    return null;
                }
                blobName = $"{productId:D}/{latestVersion}/mapping.json";
            }
            else
            {
                blobName = $"{productId:D}/{version}/mapping.json";
            }
            
            var blobClient = containerClient.GetBlobClient(blobName);
            
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Mapping blob not found: {BlobName}", blobName);
                return null;
            }
            
            var response = await blobClient.DownloadContentAsync();
            var content = response.Value.Content.ToString();
            
            _logger.LogInformation("Downloaded mapping for product {ProductId}, version {Version}", 
                productId, version ?? "latest");
            
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download mapping for product {ProductId}, version {Version}", 
                productId, version);
            throw;
        }
    }

    public async Task<string> GetMappingSasUrlAsync(Guid productId, string? version = null, int expiryHours = 24)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(MappingsContainerName);
            
            string blobName;
            if (string.IsNullOrEmpty(version))
            {
                var latestVersion = await GetLatestVersionAsync(productId);
                if (string.IsNullOrEmpty(latestVersion))
                {
                    throw new InvalidOperationException($"No mapping versions found for product {productId}");
                }
                blobName = $"{productId:D}/{latestVersion}/mapping.json";
            }
            else
            {
                blobName = $"{productId:D}/{version}/mapping.json";
            }
            
            var blobClient = containerClient.GetBlobClient(blobName);
            
            if (!await blobClient.ExistsAsync())
            {
                throw new InvalidOperationException($"Mapping blob not found: {blobName}");
            }
            
            // Generate SAS token with read permissions
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = MappingsContainerName,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(expiryHours)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);
            
            var sasUrl = blobClient.GenerateSasUri(sasBuilder).ToString();
            
            _logger.LogInformation("Generated SAS URL for product {ProductId}, version {Version}, expires in {Hours} hours", 
                productId, version ?? "latest", expiryHours);
            
            return sasUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL for product {ProductId}, version {Version}", 
                productId, version);
            throw;
        }
    }

    public async Task<IEnumerable<MappingVersionDto>> ListMappingVersionsAsync(Guid productId)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(MappingsContainerName);
            var prefix = $"{productId:D}/";
            
            var versions = new List<MappingVersionDto>();
            var latestVersion = await GetLatestVersionAsync(productId);
            
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                if (!blobItem.Name.EndsWith("/mapping.json"))
                    continue;
                
                // Extract version from blob name: {productId}/{version}/mapping.json
                var pathParts = blobItem.Name.Split('/');
                if (pathParts.Length != 3)
                    continue;
                
                var version = pathParts[1];
                
                // Get blob metadata
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var properties = await blobClient.GetPropertiesAsync();
                
                var versionDto = new MappingVersionDto
                {
                    Version = version,
                    CreatedAt = blobItem.Properties.CreatedOn?.DateTime ?? DateTime.UtcNow,
                    SizeBytes = blobItem.Properties.ContentLength ?? 0,
                    IsLatest = version == latestVersion,
                    BlobUrl = blobClient.Uri.ToString(),
                    ContentHash = properties.Value.Metadata.TryGetValue("ContentHash", out var hash) ? hash : null,
                    CreatedBy = properties.Value.Metadata.TryGetValue("CreatedBy", out var createdBy) ? createdBy : null
                };
                
                versions.Add(versionDto);
            }
            
            return versions.OrderByDescending(v => v.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list mapping versions for product {ProductId}", productId);
            throw;
        }
    }

    public async Task<bool> DeleteMappingAsync(Guid productId, string version)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(MappingsContainerName);
            var blobName = $"{productId:D}/{version}/mapping.json";
            var blobClient = containerClient.GetBlobClient(blobName);
            
            var response = await blobClient.DeleteIfExistsAsync();
            
            if (response.Value)
            {
                _logger.LogInformation("Deleted mapping for product {ProductId}, version {Version}", 
                    productId, version);
            }
            else
            {
                _logger.LogWarning("Mapping not found for deletion: product {ProductId}, version {Version}", 
                    productId, version);
            }
            
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete mapping for product {ProductId}, version {Version}", 
                productId, version);
            throw;
        }
    }

    public async Task<string> UploadExecutionResultAsync(Guid productId, Guid executionId, string fileName, string content)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ResultsContainerName);
            
            // Create blob name: {productId}/{executionId}/{fileName}
            var blobName = $"{productId:D}/{executionId:D}/{fileName}";
            var blobClient = containerClient.GetBlobClient(blobName);
            
            var metadata = new Dictionary<string, string>
            {
                ["ProductId"] = productId.ToString(),
                ["ExecutionId"] = executionId.ToString(),
                ["UploadedAt"] = DateTimeOffset.UtcNow.ToString("O")
            };
            
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var uploadOptions = new BlobUploadOptions
            {
                Metadata = metadata,
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = GetContentType(fileName)
                }
            };
            
            await blobClient.UploadAsync(stream, uploadOptions);
            
            _logger.LogInformation("Uploaded execution result {FileName} for product {ProductId}, execution {ExecutionId}", 
                fileName, productId, executionId);
            
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload execution result {FileName} for product {ProductId}, execution {ExecutionId}", 
                fileName, productId, executionId);
            throw;
        }
    }

    private async Task<string?> GetLatestVersionAsync(Guid productId)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(MappingsContainerName);
        var prefix = $"{productId:D}/";
        
        var latestVersion = "";
        var latestDate = DateTimeOffset.MinValue;
        
        await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
        {
            if (!blobItem.Name.EndsWith("/mapping.json"))
                continue;
            
            var pathParts = blobItem.Name.Split('/');
            if (pathParts.Length != 3)
                continue;
            
            var version = pathParts[1];
            var createdDate = blobItem.Properties.CreatedOn ?? DateTimeOffset.MinValue;
            
            if (createdDate > latestDate)
            {
                latestDate = createdDate;
                latestVersion = version;
            }
        }
        
        return string.IsNullOrEmpty(latestVersion) ? null : latestVersion;
    }
    
    public async Task<string> UploadJobResultsAsync(Guid productId, Guid jobId, string resultsData, string fileName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ResultsContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            var blobPath = $"{productId:D}/jobs/{jobId:D}/{fileName}";
            var blobClient = containerClient.GetBlobClient(blobPath);
            
            var metadata = new Dictionary<string, string>
            {
                ["ProductId"] = productId.ToString(),
                ["JobId"] = jobId.ToString(),
                ["UploadedAt"] = DateTimeOffset.UtcNow.ToString("O")
            };
            
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(resultsData));
            var uploadOptions = new BlobUploadOptions
            {
                Metadata = metadata,
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = GetContentType(fileName)
                }
            };
            
            await blobClient.UploadAsync(stream, uploadOptions);
            
            _logger.LogInformation("Uploaded job results {FileName} for product {ProductId}, job {JobId}", 
                fileName, productId, jobId);
            
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload job results {FileName} for product {ProductId}, job {JobId}", 
                fileName, productId, jobId);
            throw;
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".json" => "application/json",
            ".log" => "text/plain",
            ".txt" => "text/plain",
            ".xml" => "application/xml",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }
}