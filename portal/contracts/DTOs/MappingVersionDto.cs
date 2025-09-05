namespace Contracts.DTOs;

/// <summary>
/// DTO for mapping file version information
/// </summary>
public class MappingVersionDto
{
    /// <summary>
    /// Version identifier
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// When this version was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Size of the mapping file in bytes
    /// </summary>
    public long SizeBytes { get; set; }
    
    /// <summary>
    /// Who created this version
    /// </summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// Whether this is the latest version
    /// </summary>
    public bool IsLatest { get; set; }
    
    /// <summary>
    /// Blob URL for this version
    /// </summary>
    public string BlobUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// MD5 hash of the file content for integrity verification
    /// </summary>
    public string? ContentHash { get; set; }
}