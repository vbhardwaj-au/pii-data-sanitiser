using System;
using System.ComponentModel.DataAnnotations;

namespace Contracts.DTOs
{
    /// <summary>
    /// Data transfer object for ProductConnectionString
    /// </summary>
    public class ProductConnectionStringDto
    {
        /// <summary>
        /// Unique identifier for the connection string
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Product that owns this connection string
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Type of connection (Source, Target, Backup)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ConnectionType { get; set; } = string.Empty;

        /// <summary>
        /// The actual connection string (will be masked in responses)
        /// </summary>
        [Required]
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Optional description for this connection
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Whether this connection string is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When this connection string was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When this connection string was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// User who created this connection string
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User who last updated this connection string
        /// </summary>
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Product name (for display purposes)
        /// </summary>
        public string? ProductName { get; set; }
    }

    /// <summary>
    /// Request object for creating/updating connection strings
    /// </summary>
    public class CreateProductConnectionStringRequest
    {
        /// <summary>
        /// Product that will own this connection string
        /// </summary>
        [Required]
        public Guid ProductId { get; set; }

        /// <summary>
        /// Type of connection (Source, Target, Backup)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ConnectionType { get; set; } = string.Empty;

        /// <summary>
        /// The actual connection string
        /// </summary>
        [Required]
        [StringLength(4000, ErrorMessage = "Connection string cannot exceed 4000 characters")]
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Optional description for this connection
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Whether this connection string is active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Request object for updating existing connection strings
    /// </summary>
    public class UpdateProductConnectionStringRequest
    {
        /// <summary>
        /// Type of connection (Source, Target, Backup)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ConnectionType { get; set; } = string.Empty;

        /// <summary>
        /// The actual connection string
        /// </summary>
        [Required]
        [StringLength(4000, ErrorMessage = "Connection string cannot exceed 4000 characters")]
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Optional description for this connection
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Whether this connection string is active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Response object for connection test results
    /// </summary>
    public class ConnectionTestResult
    {
        /// <summary>
        /// Whether the connection test was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the test result
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Connection details (masked for security)
        /// </summary>
        public string? ConnectionDetails { get; set; }

        /// <summary>
        /// Time taken to establish connection (in milliseconds)
        /// </summary>
        public int ConnectionTimeMs { get; set; }

        /// <summary>
        /// Database server version (if connection successful)
        /// </summary>
        public string? ServerVersion { get; set; }

        /// <summary>
        /// Error details (if connection failed)
        /// </summary>
        public string? ErrorDetails { get; set; }
    }
}