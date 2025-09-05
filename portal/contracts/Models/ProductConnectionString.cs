using System;
using System.ComponentModel.DataAnnotations;

namespace Contracts.Models
{
    /// <summary>
    /// Represents a connection string configuration for a product database
    /// </summary>
    public class ProductConnectionString
    {
        /// <summary>
        /// Unique identifier for the connection string
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Product that owns this connection string
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
        /// The actual connection string (encrypted in storage)
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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this connection string was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User who created this connection string
        /// </summary>
        [StringLength(255)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User who last updated this connection string
        /// </summary>
        [StringLength(255)]
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Navigation property to the product
        /// </summary>
        public virtual Product? Product { get; set; }
    }

    /// <summary>
    /// Supported connection string types
    /// </summary>
    public static class ConnectionTypes
    {
        /// <summary>
        /// Source database containing original data
        /// </summary>
        public const string Source = "Source";

        /// <summary>
        /// Target database for obfuscated data
        /// </summary>
        public const string Target = "Target";

        /// <summary>
        /// Backup database for archival/rollback
        /// </summary>
        public const string Backup = "Backup";

        /// <summary>
        /// Get all valid connection types
        /// </summary>
        public static readonly string[] All = { Source, Target, Backup };

        /// <summary>
        /// Validate if a connection type is supported
        /// </summary>
        public static bool IsValid(string connectionType)
        {
            return Array.Exists(All, t => string.Equals(t, connectionType, StringComparison.OrdinalIgnoreCase));
        }
    }
}