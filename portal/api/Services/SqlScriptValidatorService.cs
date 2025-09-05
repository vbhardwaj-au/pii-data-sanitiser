using Contracts.DTOs;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Text;

namespace API.Services
{
    /// <summary>
    /// Service for validating SQL scripts for security and safety
    /// </summary>
    public class SqlScriptValidatorService : ISqlScriptValidatorService
    {
        private readonly ILogger<SqlScriptValidatorService> _logger;

        // Dangerous SQL patterns that should be blocked
        private static readonly Dictionary<string, string> DangerousPatterns = new()
        {
            { @"\bxp_cmdshell\b", "Extended stored procedure xp_cmdshell is not allowed" },
            { @"\bsp_configure\b", "System configuration changes are not allowed" },
            { @"\bOPENROWSET\b", "OPENROWSET function is not allowed" },
            { @"\bOPENDATASOURCE\b", "OPENDATASOURCE function is not allowed" },
            { @"\bBULK\s+INSERT\b", "BULK INSERT operations are not allowed" },
            { @"\bSHUTDOWN\b", "SHUTDOWN command is not allowed" },
            { @"\bRECONFIGURE\b", "RECONFIGURE command is not allowed" },
            { @"\bDBCC\b", "DBCC commands are not allowed" },
            { @"\bBACKUP\b", "BACKUP operations are not allowed" },
            { @"\bRESTORE\b", "RESTORE operations are not allowed" },
            { @"--.*(?:\r?\n|$)", "SQL comments should be removed or replaced with /* */ style" }
        };

        // DDL patterns
        private static readonly string[] DDLPatterns = {
            @"\bCREATE\s+(TABLE|VIEW|INDEX|PROCEDURE|FUNCTION|TRIGGER|SCHEMA|DATABASE)\b",
            @"\bALTER\s+(TABLE|VIEW|INDEX|PROCEDURE|FUNCTION|TRIGGER|SCHEMA|DATABASE)\b",
            @"\bDROP\s+(TABLE|VIEW|INDEX|PROCEDURE|FUNCTION|TRIGGER|SCHEMA|DATABASE)\b",
            @"\bTRUNCATE\s+TABLE\b"
        };

        // DCL patterns
        private static readonly string[] DCLPatterns = {
            @"\bGRANT\b",
            @"\bREVOKE\b",
            @"\bDENY\b",
            @"\bCREATE\s+(USER|LOGIN|ROLE)\b",
            @"\bALTER\s+(USER|LOGIN|ROLE)\b",
            @"\bDROP\s+(USER|LOGIN|ROLE)\b"
        };

        public SqlScriptValidatorService(ILogger<SqlScriptValidatorService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates a SQL script for security issues and dangerous operations
        /// </summary>
        public async Task<ScriptValidationResultDto> ValidateScriptAsync(string sqlScript, bool allowDDL = false, bool allowDCL = false)
        {
            var result = new ScriptValidationResultDto();
            
            if (string.IsNullOrWhiteSpace(sqlScript))
            {
                result.IsValid = false;
                result.SecurityIssues.Add(new SecurityIssueDto
                {
                    IssueType = "EmptyScript",
                    Severity = "High",
                    Description = "SQL script cannot be empty"
                });
                return result;
            }

            var cleanScript = SanitizeScript(sqlScript);
            var upperScript = cleanScript.ToUpperInvariant();

            // Check for dangerous patterns
            foreach (var pattern in DangerousPatterns)
            {
                var matches = Regex.Matches(upperScript, pattern.Key, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (matches.Count > 0)
                {
                    result.SecurityIssues.Add(new SecurityIssueDto
                    {
                        IssueType = "DangerousOperation",
                        Severity = "Critical",
                        Description = pattern.Value,
                        LineNumber = GetLineNumber(cleanScript, matches[0].Index)
                    });
                }
            }

            // Check DDL operations
            var containsDDL = CheckForPatterns(upperScript, DDLPatterns);
            result.ContainsDDL = containsDDL;
            if (containsDDL && !allowDDL)
            {
                result.SecurityIssues.Add(new SecurityIssueDto
                {
                    IssueType = "DDLNotAllowed",
                    Severity = "High",
                    Description = "DDL operations (CREATE, ALTER, DROP) are not allowed in this context"
                });
            }

            // Check DCL operations
            var containsDCL = CheckForPatterns(upperScript, DCLPatterns);
            result.ContainsDCL = containsDCL;
            if (containsDCL && !allowDCL)
            {
                result.SecurityIssues.Add(new SecurityIssueDto
                {
                    IssueType = "DCLNotAllowed",
                    Severity = "High",
                    Description = "DCL operations (GRANT, REVOKE, DENY) are not allowed in this context"
                });
            }

            // Check for DML operations
            result.ContainsDML = CheckForPatterns(upperScript, new[] { @"\b(SELECT|INSERT|UPDATE|DELETE)\b" });

            // Determine risk level and complexity
            result.RiskLevel = DetermineRiskLevel(result.SecurityIssues);
            result.ExecutionComplexity = DetermineComplexity(cleanScript);
            result.IsValid = result.SecurityIssues.Count == 0 || !result.SecurityIssues.Any(i => i.Severity == "Critical");

            // Add warnings for complex operations
            if (Regex.IsMatch(upperScript, @"\bWHILE\b|\bCURSOR\b"))
            {
                result.Warnings.Add("Script contains loops or cursors which may impact performance");
            }

            if (Regex.IsMatch(upperScript, @"\bTEMP\b|#"))
            {
                result.Warnings.Add("Script uses temporary tables which may require additional cleanup");
            }

            await Task.CompletedTask; // Make async for future extensions
            return result;
        }

        /// <summary>
        /// Performs a dry-run validation of a script against a connection string
        /// </summary>
        public async Task<ScriptValidationResultDto> DryRunValidationAsync(string sqlScript, string connectionString)
        {
            var result = await ValidateScriptAsync(sqlScript);
            
            if (!result.IsValid)
            {
                result.Notes = "Script failed security validation - dry run not performed";
                return result;
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Wrap in transaction that will be rolled back
                using var transaction = connection.BeginTransaction();
                
                try
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = sqlScript;
                    command.CommandTimeout = 30; // Short timeout for dry run

                    // Execute with minimal impact
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    
                    // Always rollback - this is a dry run
                    transaction.Rollback();

                    result.Notes = $"Dry run successful - would affect {rowsAffected} rows";
                    result.Warnings.Add("Dry run completed successfully (changes rolled back)");
                }
                catch (SqlException sqlEx)
                {
                    transaction.Rollback();
                    
                    result.SecurityIssues.Add(new SecurityIssueDto
                    {
                        IssueType = "SqlError",
                        Severity = "High",
                        Description = $"SQL execution error: {sqlEx.Message}"
                    });
                    result.IsValid = false;
                }
            }
            catch (Exception ex)
            {
                result.SecurityIssues.Add(new SecurityIssueDto
                {
                    IssueType = "ConnectionError",
                    Severity = "High",
                    Description = $"Connection error during dry run: {ex.Message}"
                });
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// Sanitizes a SQL script by removing comments and normalizing whitespace
        /// </summary>
        public string SanitizeScript(string sqlScript)
        {
            if (string.IsNullOrWhiteSpace(sqlScript))
                return string.Empty;

            var sanitized = sqlScript;

            // Remove single-line comments (but preserve strings)
            sanitized = Regex.Replace(sanitized, @"--.*$", "", RegexOptions.Multiline);

            // Remove multi-line comments
            sanitized = Regex.Replace(sanitized, @"/\*.*?\*/", "", RegexOptions.Singleline);

            // Normalize whitespace
            sanitized = Regex.Replace(sanitized, @"\s+", " ");

            return sanitized.Trim();
        }

        /// <summary>
        /// Extracts table and column references from a SQL script
        /// </summary>
        public async Task<List<string>> ExtractDatabaseObjectsAsync(string sqlScript)
        {
            var objects = new HashSet<string>();
            var sanitized = SanitizeScript(sqlScript);
            
            // Extract table references (simplified pattern matching)
            var tablePatterns = new[]
            {
                @"\bFROM\s+([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)?)",
                @"\bJOIN\s+([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)?)",
                @"\bINTO\s+([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)?)",
                @"\bUPDATE\s+([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)?)",
                @"\bDELETE\s+FROM\s+([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)?)"
            };

            foreach (var pattern in tablePatterns)
            {
                var matches = Regex.Matches(sanitized, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        objects.Add(match.Groups[1].Value);
                    }
                }
            }

            await Task.CompletedTask; // Make async for future extensions
            return objects.ToList();
        }

        private bool CheckForPatterns(string script, string[] patterns)
        {
            return patterns.Any(pattern => Regex.IsMatch(script, pattern, RegexOptions.IgnoreCase));
        }

        private string DetermineRiskLevel(List<SecurityIssueDto> issues)
        {
            if (issues.Any(i => i.Severity == "Critical"))
                return "Critical";
            if (issues.Any(i => i.Severity == "High"))
                return "High";
            if (issues.Any(i => i.Severity == "Medium"))
                return "Medium";
            return "Low";
        }

        private string DetermineComplexity(string script)
        {
            var complexity = 0;
            
            // Simple heuristics for complexity
            if (Regex.IsMatch(script, @"\bJOIN\b", RegexOptions.IgnoreCase))
                complexity += 1;
            if (Regex.IsMatch(script, @"\bSUBQUERY\b|\bEXISTS\b|\bIN\s*\(", RegexOptions.IgnoreCase))
                complexity += 2;
            if (Regex.IsMatch(script, @"\bCTE\b|\bWITH\b", RegexOptions.IgnoreCase))
                complexity += 2;
            if (Regex.IsMatch(script, @"\bWHILE\b|\bCURSOR\b", RegexOptions.IgnoreCase))
                complexity += 3;
            if (Regex.IsMatch(script, @"\bTRY\b|\bCATCH\b", RegexOptions.IgnoreCase))
                complexity += 1;

            return complexity switch
            {
                >= 5 => "High",
                >= 2 => "Medium",
                _ => "Low"
            };
        }

        private int GetLineNumber(string script, int charIndex)
        {
            if (charIndex < 0 || charIndex >= script.Length)
                return 1;

            return script.Take(charIndex).Count(c => c == '\n') + 1;
        }
    }
}