namespace Contracts.DTOs
{
    /// <summary>
    /// Result of SQL script security validation
    /// </summary>
    public class ScriptValidationResultDto
    {
        /// <summary>
        /// Whether the script passed security validation
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Security risk level (Low, Medium, High, Critical)
        /// </summary>
        public string RiskLevel { get; set; } = "Low";

        /// <summary>
        /// List of security issues found
        /// </summary>
        public List<SecurityIssueDto> SecurityIssues { get; set; } = new();

        /// <summary>
        /// List of warnings (non-blocking issues)
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// General validation notes
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Estimated execution complexity (Low, Medium, High)
        /// </summary>
        public string ExecutionComplexity { get; set; } = "Low";

        /// <summary>
        /// Whether the script contains DDL operations
        /// </summary>
        public bool ContainsDDL { get; set; }

        /// <summary>
        /// Whether the script contains DML operations
        /// </summary>
        public bool ContainsDML { get; set; }

        /// <summary>
        /// Whether the script contains DCL operations
        /// </summary>
        public bool ContainsDCL { get; set; }

        /// <summary>
        /// When this validation was performed
        /// </summary>
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a security issue found in a script
    /// </summary>
    public class SecurityIssueDto
    {
        /// <summary>
        /// Type of security issue
        /// </summary>
        public string IssueType { get; set; } = string.Empty;

        /// <summary>
        /// Severity level (Low, Medium, High, Critical)
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Line number where issue was found (if applicable)
        /// </summary>
        public int? LineNumber { get; set; }

        /// <summary>
        /// Suggested fix or mitigation
        /// </summary>
        public string? Suggestion { get; set; }
    }
}