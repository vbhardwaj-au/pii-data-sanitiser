using Contracts.DTOs;

namespace API.Services
{
    /// <summary>
    /// Service interface for validating SQL scripts for security and safety
    /// </summary>
    public interface ISqlScriptValidatorService
    {
        /// <summary>
        /// Validates a SQL script for security issues and dangerous operations
        /// </summary>
        /// <param name="sqlScript">The SQL script to validate</param>
        /// <param name="allowDDL">Whether DDL operations are allowed</param>
        /// <param name="allowDCL">Whether DCL operations are allowed</param>
        /// <returns>Validation result with security analysis</returns>
        Task<ScriptValidationResultDto> ValidateScriptAsync(string sqlScript, bool allowDDL = false, bool allowDCL = false);

        /// <summary>
        /// Performs a dry-run validation of a script against a connection string
        /// </summary>
        /// <param name="sqlScript">The SQL script to validate</param>
        /// <param name="connectionString">Connection string to test against</param>
        /// <returns>Validation result with execution analysis</returns>
        Task<ScriptValidationResultDto> DryRunValidationAsync(string sqlScript, string connectionString);

        /// <summary>
        /// Sanitizes a SQL script by removing comments and normalizing whitespace
        /// </summary>
        /// <param name="sqlScript">The SQL script to sanitize</param>
        /// <returns>Sanitized SQL script</returns>
        string SanitizeScript(string sqlScript);

        /// <summary>
        /// Extracts table and column references from a SQL script
        /// </summary>
        /// <param name="sqlScript">The SQL script to analyze</param>
        /// <returns>List of database objects referenced</returns>
        Task<List<string>> ExtractDatabaseObjectsAsync(string sqlScript);
    }
}