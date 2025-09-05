using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace API.Services
{
    /// <summary>
    /// Service for encrypting, decrypting, and masking connection strings
    /// </summary>
    public class ConnectionStringEncryptionService : IConnectionStringEncryptionService
    {
        private readonly string _encryptionKey;
        private readonly byte[] _encryptionKeyBytes;

        public ConnectionStringEncryptionService(IConfiguration configuration)
        {
            // In production, this should come from Azure Key Vault or similar secure store
            _encryptionKey = configuration["ConnectionStringEncryption:Key"] 
                ?? "PIIObfuscationPortal2024!DefaultKey#ForConnectionStrings";
            
            // Ensure key is exactly 32 bytes for AES-256
            _encryptionKeyBytes = GetKeyBytes(_encryptionKey);
        }

        /// <summary>
        /// Encrypts a connection string using AES-256 encryption
        /// </summary>
        public string EncryptConnectionString(string plainTextConnectionString)
        {
            if (string.IsNullOrEmpty(plainTextConnectionString))
                return string.Empty;

            try
            {
                using var aes = Aes.Create();
                aes.Key = _encryptionKeyBytes;
                aes.GenerateIV();

                var plainTextBytes = Encoding.UTF8.GetBytes(plainTextConnectionString);

                using var encryptor = aes.CreateEncryptor();
                var encryptedBytes = encryptor.TransformFinalBlock(plainTextBytes, 0, plainTextBytes.Length);

                // Combine IV and encrypted data
                var result = new byte[aes.IV.Length + encryptedBytes.Length];
                Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
                Array.Copy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to encrypt connection string", ex);
            }
        }

        /// <summary>
        /// Decrypts a connection string from storage
        /// </summary>
        public string DecryptConnectionString(string encryptedConnectionString)
        {
            if (string.IsNullOrEmpty(encryptedConnectionString))
                return string.Empty;

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedConnectionString);

                using var aes = Aes.Create();
                aes.Key = _encryptionKeyBytes;

                // Extract IV from the beginning of encrypted data
                var iv = new byte[aes.IV.Length];
                var encrypted = new byte[encryptedBytes.Length - iv.Length];
                
                Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);
                Array.Copy(encryptedBytes, iv.Length, encrypted, 0, encrypted.Length);
                
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                var decryptedBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to decrypt connection string", ex);
            }
        }

        /// <summary>
        /// Masks a connection string for safe display in API responses
        /// </summary>
        public string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return string.Empty;

            // Regex patterns for common connection string parameters
            var patterns = new[]
            {
                new { Pattern = @"(Password\s*=\s*)[^;]+", Replacement = "${1}***" },
                new { Pattern = @"(Pwd\s*=\s*)[^;]+", Replacement = "${1}***" },
                new { Pattern = @"(User\s+Password\s*=\s*)[^;]+", Replacement = "${1}***" },
                new { Pattern = @"(SA\s+Password\s*=\s*)[^;]+", Replacement = "${1}***" }
            };

            var maskedConnectionString = connectionString;

            foreach (var pattern in patterns)
            {
                maskedConnectionString = Regex.Replace(maskedConnectionString, pattern.Pattern, pattern.Replacement, RegexOptions.IgnoreCase);
            }

            return maskedConnectionString;
        }

        /// <summary>
        /// Validates connection string format
        /// </summary>
        public bool ValidateConnectionStringFormat(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            try
            {
                // Basic validation - must contain Server and Database parameters
                var hasServer = Regex.IsMatch(connectionString, @"Server\s*=\s*[^;]+", RegexOptions.IgnoreCase) ||
                               Regex.IsMatch(connectionString, @"Data\s+Source\s*=\s*[^;]+", RegexOptions.IgnoreCase);

                var hasDatabase = Regex.IsMatch(connectionString, @"Database\s*=\s*[^;]+", RegexOptions.IgnoreCase) ||
                                 Regex.IsMatch(connectionString, @"Initial\s+Catalog\s*=\s*[^;]+", RegexOptions.IgnoreCase);

                return hasServer && hasDatabase;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts database name from connection string
        /// </summary>
        public string? ExtractDatabaseName(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return null;

            var patterns = new[]
            {
                @"Database\s*=\s*([^;]+)",
                @"Initial\s+Catalog\s*=\s*([^;]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(connectionString, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts server name from connection string
        /// </summary>
        public string? ExtractServerName(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return null;

            var patterns = new[]
            {
                @"Server\s*=\s*([^;]+)",
                @"Data\s+Source\s*=\s*([^;]+)",
                @"Address\s*=\s*([^;]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(connectionString, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Ensures the encryption key is exactly 32 bytes for AES-256
        /// </summary>
        private static byte[] GetKeyBytes(string key)
        {
            var keyBytes = new byte[32]; // 256 bits / 8 = 32 bytes
            var sourceBytes = Encoding.UTF8.GetBytes(key);
            
            if (sourceBytes.Length >= 32)
            {
                Array.Copy(sourceBytes, keyBytes, 32);
            }
            else
            {
                Array.Copy(sourceBytes, keyBytes, sourceBytes.Length);
                // Fill remaining bytes with a pattern
                for (int i = sourceBytes.Length; i < 32; i++)
                {
                    keyBytes[i] = (byte)(i % 256);
                }
            }

            return keyBytes;
        }
    }
}