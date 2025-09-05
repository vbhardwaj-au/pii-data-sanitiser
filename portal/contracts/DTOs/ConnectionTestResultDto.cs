namespace Contracts.DTOs
{
    /// <summary>
    /// DTO representing the result of a connection test
    /// </summary>
    public class ConnectionTestResultDto
    {
        /// <summary>
        /// Whether the connection test was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if the connection test failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The time taken to establish the connection in milliseconds
        /// </summary>
        public long ConnectionTimeMs { get; set; }

        /// <summary>
        /// Server version information
        /// </summary>
        public string? ServerVersion { get; set; }

        /// <summary>
        /// Database name
        /// </summary>
        public string? DatabaseName { get; set; }

        /// <summary>
        /// Server name/instance
        /// </summary>
        public string? ServerName { get; set; }

        /// <summary>
        /// Additional connection details
        /// </summary>
        public Dictionary<string, string> AdditionalInfo { get; set; } = new();

        /// <summary>
        /// Timestamp when the test was performed
        /// </summary>
        public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    }
}