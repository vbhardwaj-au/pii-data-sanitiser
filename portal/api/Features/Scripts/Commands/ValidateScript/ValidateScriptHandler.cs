using MediatR;
using Contracts.DTOs;
using API.Services;

namespace API.Features.Scripts.Commands.ValidateScript
{
    /// <summary>
    /// Handler for validating SQL scripts
    /// </summary>
    public class ValidateScriptHandler : IRequestHandler<ValidateScriptCommand, ScriptValidationResultDto>
    {
        private readonly ISqlScriptValidatorService _validatorService;

        public ValidateScriptHandler(ISqlScriptValidatorService validatorService)
        {
            _validatorService = validatorService ?? throw new ArgumentNullException(nameof(validatorService));
        }

        public async Task<ScriptValidationResultDto> Handle(ValidateScriptCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SqlScript))
            {
                return new ScriptValidationResultDto
                {
                    IsValid = false,
                    RiskLevel = "High",
                    SecurityIssues = new List<SecurityIssueDto>
                    {
                        new SecurityIssueDto
                        {
                            IssueType = "EmptyScript",
                            Severity = "High",
                            Description = "SQL script cannot be empty"
                        }
                    }
                };
            }

            // Perform basic security validation
            if (request.PerformDryRun && !string.IsNullOrWhiteSpace(request.ConnectionString))
            {
                return await _validatorService.DryRunValidationAsync(request.SqlScript, request.ConnectionString);
            }
            else
            {
                return await _validatorService.ValidateScriptAsync(request.SqlScript, request.AllowDDL, request.AllowDCL);
            }
        }
    }
}