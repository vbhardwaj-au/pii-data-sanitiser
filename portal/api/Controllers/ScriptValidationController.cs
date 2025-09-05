using Microsoft.AspNetCore.Mvc;
using MediatR;
using Contracts.DTOs;
using API.Features.Scripts.Commands.ValidateScript;

namespace API.Controllers
{
    /// <summary>
    /// API controller for SQL script validation
    /// </summary>
    [ApiController]
    [Route("api/script-validation")]
    public class ScriptValidationController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ScriptValidationController(IMediator mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        /// <summary>
        /// Validate a SQL script for security issues
        /// </summary>
        /// <param name="command">The script validation command</param>
        [HttpPost("validate")]
        public async Task<ActionResult<ScriptValidationResultDto>> ValidateScript(ValidateScriptCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Perform a dry-run validation of a script against a connection
        /// </summary>
        /// <param name="request">The dry-run request</param>
        [HttpPost("dry-run")]
        public async Task<ActionResult<ScriptValidationResultDto>> DryRunValidation([FromBody] DryRunValidationRequest request)
        {
            var command = new ValidateScriptCommand
            {
                SqlScript = request.SqlScript,
                PerformDryRun = true,
                ConnectionString = request.ConnectionString,
                AllowDDL = request.AllowDDL,
                AllowDCL = request.AllowDCL
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }

    /// <summary>
    /// Request model for dry-run validation
    /// </summary>
    public class DryRunValidationRequest
    {
        public string SqlScript { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public bool AllowDDL { get; set; } = false;
        public bool AllowDCL { get; set; } = false;
    }
}