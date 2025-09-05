using Microsoft.AspNetCore.Mvc;
using MediatR;
using Contracts.DTOs;
using API.Features.PipelineStages.Queries.GetAllPipelineStages;

namespace API.Controllers
{
    /// <summary>
    /// API controller for managing pipeline stages
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PipelineStagesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PipelineStagesController(IMediator mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        /// <summary>
        /// Get all pipeline stages
        /// </summary>
        /// <param name="activeOnly">Whether to include only active stages</param>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PipelineStageDto>>> GetAll([FromQuery] bool activeOnly = false)
        {
            var query = new GetAllPipelineStagesQuery(activeOnly);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}