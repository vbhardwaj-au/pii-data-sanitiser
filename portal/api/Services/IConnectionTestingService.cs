using Contracts.DTOs;

namespace API.Services
{
    /// <summary>
    /// Service interface for testing database connections
    /// </summary>
    public interface IConnectionTestingService
    {
        /// <summary>
        /// Tests a connection string to verify it can connect to the database
        /// </summary>
        /// <param name="connectionString">The connection string to test</param>
        /// <returns>Connection test result</returns>
        Task<ConnectionTestResultDto> TestConnectionAsync(string connectionString);

        /// <summary>
        /// Tests a connection string and returns basic database information
        /// </summary>
        /// <param name="connectionString">The connection string to test</param>
        /// <returns>Connection test result with database info</returns>
        Task<ConnectionTestResultDto> TestConnectionWithInfoAsync(string connectionString);
    }
}