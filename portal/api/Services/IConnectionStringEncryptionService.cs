namespace API.Services
{
    /// <summary>
    /// Service for encrypting and decrypting connection strings
    /// </summary>
    public interface IConnectionStringEncryptionService
    {
        /// <summary>
        /// Encrypts a connection string for secure storage
        /// </summary>
        /// <param name="plainTextConnectionString">The plain text connection string</param>
        /// <returns>The encrypted connection string</returns>
        string EncryptConnectionString(string plainTextConnectionString);

        /// <summary>
        /// Decrypts a connection string from storage
        /// </summary>
        /// <param name="encryptedConnectionString">The encrypted connection string</param>
        /// <returns>The plain text connection string</returns>
        string DecryptConnectionString(string encryptedConnectionString);

        /// <summary>
        /// Masks a connection string for display in API responses
        /// </summary>
        /// <param name="connectionString">The connection string to mask</param>
        /// <returns>The masked connection string</returns>
        string MaskConnectionString(string connectionString);

        /// <summary>
        /// Validates that a connection string has the correct format
        /// </summary>
        /// <param name="connectionString">The connection string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        bool ValidateConnectionStringFormat(string connectionString);

        /// <summary>
        /// Extracts the database name from a connection string
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <returns>The database name or null if not found</returns>
        string? ExtractDatabaseName(string connectionString);

        /// <summary>
        /// Extracts the server name from a connection string
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <returns>The server name or null if not found</returns>
        string? ExtractServerName(string connectionString);
    }
}