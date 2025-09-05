using Microsoft.Data.SqlClient;
using Contracts.DTOs;
using System.Diagnostics;

namespace API.Services
{
    /// <summary>
    /// Service for testing database connections
    /// </summary>
    public class ConnectionTestingService : IConnectionTestingService
    {
        private readonly ILogger<ConnectionTestingService> _logger;

        public ConnectionTestingService(ILogger<ConnectionTestingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Tests a connection string to verify it can connect to the database
        /// </summary>
        public async Task<ConnectionTestResultDto> TestConnectionAsync(string connectionString)
        {
            var result = new ConnectionTestResultDto();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var connection = new SqlConnection(connectionString);
                
                // Set a reasonable timeout for connection testing
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = 10 // 10 seconds timeout
                };
                
                using var testConnection = new SqlConnection(builder.ToString());
                await testConnection.OpenAsync();
                
                stopwatch.Stop();
                result.ConnectionTimeMs = stopwatch.ElapsedMilliseconds;
                result.IsSuccessful = true;
                result.DatabaseName = testConnection.Database;
                result.ServerName = testConnection.DataSource;
                
                _logger.LogInformation("Connection test successful to {Server}/{Database} in {Ms}ms", 
                    testConnection.DataSource, testConnection.Database, result.ConnectionTimeMs);
                    
            }
            catch (SqlException sqlEx)
            {
                stopwatch.Stop();
                result.IsSuccessful = false;
                result.ErrorMessage = $"SQL Server Error: {sqlEx.Message}";
                result.ConnectionTimeMs = stopwatch.ElapsedMilliseconds;
                
                _logger.LogWarning("Connection test failed with SQL error: {Error}", sqlEx.Message);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.IsSuccessful = false;
                result.ErrorMessage = $"Connection Error: {ex.Message}";
                result.ConnectionTimeMs = stopwatch.ElapsedMilliseconds;
                
                _logger.LogWarning("Connection test failed with general error: {Error}", ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Tests a connection string and returns basic database information
        /// </summary>
        public async Task<ConnectionTestResultDto> TestConnectionWithInfoAsync(string connectionString)
        {
            var result = new ConnectionTestResultDto();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = 10 // 10 seconds timeout
                };
                
                using var connection = new SqlConnection(builder.ToString());
                await connection.OpenAsync();
                
                result.IsSuccessful = true;
                result.DatabaseName = connection.Database;
                result.ServerName = connection.DataSource;
                result.ServerVersion = connection.ServerVersion;
                
                // Get additional database information
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        @@VERSION as ServerInfo,
                        DB_NAME() as CurrentDatabase,
                        USER_NAME() as CurrentUser,
                        GETDATE() as ServerTime";
                        
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.AdditionalInfo["ServerInfo"] = reader["ServerInfo"]?.ToString() ?? "";
                    result.AdditionalInfo["CurrentDatabase"] = reader["CurrentDatabase"]?.ToString() ?? "";
                    result.AdditionalInfo["CurrentUser"] = reader["CurrentUser"]?.ToString() ?? "";
                    result.AdditionalInfo["ServerTime"] = reader["ServerTime"]?.ToString() ?? "";
                }
                
                stopwatch.Stop();
                result.ConnectionTimeMs = stopwatch.ElapsedMilliseconds;
                
                _logger.LogInformation("Extended connection test successful to {Server}/{Database} in {Ms}ms", 
                    connection.DataSource, connection.Database, result.ConnectionTimeMs);
                    
            }
            catch (SqlException sqlEx)
            {
                stopwatch.Stop();
                result.IsSuccessful = false;
                result.ErrorMessage = $"SQL Server Error: {sqlEx.Message}";
                result.ConnectionTimeMs = stopwatch.ElapsedMilliseconds;
                
                _logger.LogWarning("Extended connection test failed with SQL error: {Error}", sqlEx.Message);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.IsSuccessful = false;
                result.ErrorMessage = $"Connection Error: {ex.Message}";
                result.ConnectionTimeMs = stopwatch.ElapsedMilliseconds;
                
                _logger.LogWarning("Extended connection test failed with general error: {Error}", ex.Message);
            }

            return result;
        }
    }
}