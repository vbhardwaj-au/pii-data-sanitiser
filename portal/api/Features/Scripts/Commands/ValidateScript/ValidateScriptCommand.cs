using MediatR;
using Contracts.DTOs;

namespace API.Features.Scripts.Commands.ValidateScript
{
    /// <summary>
    /// Command to validate a SQL script for security and safety
    /// </summary>
    public class ValidateScriptCommand : IRequest<ScriptValidationResultDto>
    {
        /// <summary>
        /// The SQL script to validate
        /// </summary>
        public string SqlScript { get; set; } = string.Empty;

        /// <summary>
        /// Whether DDL operations are allowed
        /// </summary>
        public bool AllowDDL { get; set; } = false;

        /// <summary>
        /// Whether DCL operations are allowed
        /// </summary>
        public bool AllowDCL { get; set; } = false;

        /// <summary>
        /// Whether to perform a dry-run against a connection
        /// </summary>
        public bool PerformDryRun { get; set; } = false;

        /// <summary>
        /// Connection string for dry-run (if PerformDryRun is true)
        /// </summary>
        public string? ConnectionString { get; set; }
    }
}