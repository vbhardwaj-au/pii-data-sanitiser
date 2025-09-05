using Microsoft.AspNetCore.Mvc;
using MediatR;
using Contracts.DTOs;
using API.Features.Scripts.Commands.CreateScript;
using API.Features.Scripts.Queries.GetScriptsByProductAndStage;

namespace API.Controllers
{
    /// <summary>
    /// API controller for managing pipeline stage scripts
    /// </summary>
    [ApiController]
    [Route("api/products/{productId}/stages/{stageId}/scripts")]
    public class ScriptsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ScriptsController(IMediator mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        /// <summary>
        /// Get scripts for a specific product and pipeline stage
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <param name="stageId">The pipeline stage ID</param>
        /// <param name="enabledOnly">Whether to include only enabled scripts</param>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PipelineStageScriptDto>>> GetByProductAndStage(
            Guid productId, 
            Guid stageId, 
            [FromQuery] bool enabledOnly = false)
        {
            var query = new GetScriptsByProductAndStageQuery(productId, stageId, enabledOnly);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Create a new script for a product and pipeline stage
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <param name="stageId">The pipeline stage ID</param>
        /// <param name="command">The script creation data</param>
        [HttpPost]
        public async Task<ActionResult<PipelineStageScriptDto>> Create(
            Guid productId, 
            Guid stageId, 
            CreateScriptCommand command)
        {
            // Ensure route parameters match command
            command.ProductId = productId;
            command.PipelineStageId = stageId;

            try
            {
                var result = await _mediator.Send(command);
                return CreatedAtAction(nameof(GetByProductAndStage), 
                    new { productId, stageId }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }
    }
}