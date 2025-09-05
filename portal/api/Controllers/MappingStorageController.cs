using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using API.Services;
using API.Authorization;
using Contracts.DTOs;
using DAL.Repositories;
using System.Text.Json;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Mapping File Storage Management")]
[RequireRole(Roles.Analyst)]
public class MappingStorageController : ControllerBase
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<MappingStorageController> _logger;
    
    public MappingStorageController(
        IBlobStorageService blobStorageService,
        IProductRepository productRepository,
        ILogger<MappingStorageController> logger)
    {
        _blobStorageService = blobStorageService;
        _productRepository = productRepository;
        _logger = logger;
    }

    /// <summary>
    /// Export and upload product mappings to Azure Blob Storage
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="version">Version identifier (optional, auto-generated if not provided)</param>
    [HttpPost("{productId}/export")]
    [SwaggerOperation(Summary = "Export Product Mappings", Description = "Export product mappings to Azure Blob Storage")]
    [SwaggerResponse(200, "Mappings exported successfully", typeof(MappingExportResult))]
    [SwaggerResponse(404, "Product not found")]
    public async Task<ActionResult<MappingExportResult>> ExportProductMappings(
        Guid productId, 
        [FromQuery] string? version = null)
    {
        try
        {
            // Get product with mappings
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
            {
                return NotFound($"Product {productId} not found");
            }

            // Generate unified mapping configuration
            var mappingConfig = await GenerateUnifiedMappingAsync(product);
            var mappingJson = JsonSerializer.Serialize(mappingConfig, new JsonSerializerOptions { WriteIndented = true });
            
            // Upload to Azure Blob Storage
            var blobUrl = await _blobStorageService.UploadMappingAsync(productId, mappingJson, version);
            
            // Generate SAS URL for agent access
            var sasUrl = await _blobStorageService.GetMappingSasUrlAsync(productId, version, 168); // 7 days
            
            var result = new MappingExportResult
            {
                ProductId = productId,
                ProductName = product.Name,
                Version = version ?? DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"),
                BlobUrl = blobUrl,
                SasUrl = sasUrl,
                ExportedAt = DateTimeOffset.UtcNow,
                MappingCount = CountMappings(mappingConfig),
                Success = true
            };
            
            _logger.LogInformation("Successfully exported mappings for product {ProductId} to blob storage", productId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export mappings for product {ProductId}", productId);
            return StatusCode(500, new { error = "Failed to export mappings", details = ex.Message });
        }
    }

    /// <summary>
    /// Get mapping file download URL for agents
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="version">Version to download (null for latest)</param>
    /// <param name="expiryHours">SAS token expiry in hours (default 24)</param>
    [HttpGet("{productId}/download-url")]
    [SwaggerOperation(Summary = "Get Mapping Download URL", Description = "Get SAS URL for agent access to mapping file")]
    [SwaggerResponse(200, "Download URL generated successfully")]
    [SwaggerResponse(404, "Product or mapping version not found")]
    public async Task<ActionResult<MappingDownloadUrlResult>> GetMappingDownloadUrl(
        Guid productId, 
        [FromQuery] string? version = null,
        [FromQuery] int expiryHours = 24)
    {
        try
        {
            var sasUrl = await _blobStorageService.GetMappingSasUrlAsync(productId, version, expiryHours);
            
            var result = new MappingDownloadUrlResult
            {
                ProductId = productId,
                Version = version ?? "latest",
                SasUrl = sasUrl,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(expiryHours),
                Success = true
            };
            
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate download URL for product {ProductId}, version {Version}", 
                productId, version);
            return StatusCode(500, new { error = "Failed to generate download URL", details = ex.Message });
        }
    }

    /// <summary>
    /// List all mapping versions for a product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    [HttpGet("{productId}/versions")]
    [SwaggerOperation(Summary = "List Mapping Versions", Description = "List all available mapping versions for a product")]
    [SwaggerResponse(200, "Mapping versions retrieved successfully", typeof(IEnumerable<MappingVersionDto>))]
    public async Task<ActionResult<IEnumerable<MappingVersionDto>>> ListMappingVersions(Guid productId)
    {
        try
        {
            var versions = await _blobStorageService.ListMappingVersionsAsync(productId);
            return Ok(versions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list mapping versions for product {ProductId}", productId);
            return StatusCode(500, new { error = "Failed to list mapping versions", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete a specific mapping version
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="version">Version to delete</param>
    [HttpDelete("{productId}/versions/{version}")]
    [SwaggerOperation(Summary = "Delete Mapping Version", Description = "Delete a specific mapping version")]
    [SwaggerResponse(200, "Mapping version deleted successfully")]
    [SwaggerResponse(404, "Mapping version not found")]
    public async Task<IActionResult> DeleteMappingVersion(Guid productId, string version)
    {
        try
        {
            var success = await _blobStorageService.DeleteMappingAsync(productId, version);
            
            if (success)
            {
                return Ok(new { message = "Mapping version deleted successfully" });
            }
            else
            {
                return NotFound(new { error = "Mapping version not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete mapping version {Version} for product {ProductId}", 
                version, productId);
            return StatusCode(500, new { error = "Failed to delete mapping version", details = ex.Message });
        }
    }

    /// <summary>
    /// Download mapping content directly (for debugging/testing)
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="version">Version to download (null for latest)</param>
    [HttpGet("{productId}/content")]
    [SwaggerOperation(Summary = "Download Mapping Content", Description = "Download mapping file content directly")]
    [SwaggerResponse(200, "Mapping content retrieved successfully")]
    [SwaggerResponse(404, "Mapping not found")]
    public async Task<IActionResult> DownloadMappingContent(Guid productId, [FromQuery] string? version = null)
    {
        try
        {
            var content = await _blobStorageService.DownloadMappingAsync(productId, version);
            
            if (content == null)
            {
                return NotFound(new { error = "Mapping not found" });
            }
            
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download mapping content for product {ProductId}, version {Version}", 
                productId, version);
            return StatusCode(500, new { error = "Failed to download mapping content", details = ex.Message });
        }
    }

    private async Task<object> GenerateUnifiedMappingAsync(Contracts.Models.Product product)
    {
        // Generate the unified mapping configuration that the obfuscation engine can consume
        var config = new
        {
            ProductInfo = new
            {
                Id = product.Id,
                Name = product.Name,
                DatabaseTechnology = product.DatabaseTechnology,
                ConnectionString = product.ConnectionString, // Note: This should be handled securely
                GlobalSeed = product.GlobalSeed
            },
            ProcessingSettings = new
            {
                BatchSize = product.BatchSize,
                SqlBatchSize = product.SqlBatchSize,
                ParallelThreads = product.ParallelThreads,
                CommandTimeoutSeconds = product.CommandTimeoutSeconds,
                MaxCacheSize = product.MaxCacheSize
            },
            TableMappings = product.DatabaseSchemas?.Where(schema => 
                    schema.TableColumns != null && 
                    schema.TableColumns.Any(col => col.ColumnObfuscationMapping?.IsEnabled == true))
                .Select(schema => new
                {
                    SchemaName = schema.SchemaName,
                    TableName = schema.TableName,
                    Columns = schema.TableColumns
                        .Where(col => col.ColumnObfuscationMapping?.IsEnabled == true)
                        .Select(col => new
                        {
                            ColumnName = col.ColumnName,
                            DataType = col.SqlDataType,
                            MaxLength = col.MaxLength,
                            IsNullable = col.IsNullable,
                            ObfuscationType = col.ColumnObfuscationMapping?.ObfuscationDataType,
                            PreserveLength = col.ColumnObfuscationMapping?.PreserveLength ?? false,
                            IsEnabled = col.ColumnObfuscationMapping?.IsEnabled ?? false
                        }).ToList()
                }).ToList(),
            GeneratedAt = DateTimeOffset.UtcNow,
            Version = "1.0"
        };

        return config;
    }

    private static int CountMappings(object mappingConfig)
    {
        // Count the total number of column mappings
        try
        {
            var json = JsonSerializer.Serialize(mappingConfig);
            var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("TableMappings", out var tableMappings))
            {
                int count = 0;
                foreach (var table in tableMappings.EnumerateArray())
                {
                    if (table.TryGetProperty("Columns", out var columns))
                    {
                        count += columns.GetArrayLength();
                    }
                }
                return count;
            }
            
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}

public class MappingExportResult
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public string SasUrl { get; set; } = string.Empty;
    public DateTimeOffset ExportedAt { get; set; }
    public int MappingCount { get; set; }
    public bool Success { get; set; }
}

public class MappingDownloadUrlResult
{
    public Guid ProductId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string SasUrl { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Success { get; set; }
}