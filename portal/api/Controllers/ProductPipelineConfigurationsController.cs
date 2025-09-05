using Microsoft.AspNetCore.Mvc;
using MediatR;
using Contracts.DTOs;
using API.Features.ProductPipelineConfigurations.Queries.GetProductPipelineConfigurations;

namespace API.Controllers
{
    /// <summary>
    /// API controller for managing product pipeline configurations
    /// </summary>
    [ApiController]
    [Route("api/products/{productId}/pipeline-configurations")]
    public class ProductPipelineConfigurationsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProductPipelineConfigurationsController(IMediator mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        /// <summary>
        /// Get pipeline configurations for a specific product
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <param name="enabledOnly">Whether to include only enabled configurations</param>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductPipelineConfigurationDto>>> GetByProductId(
            Guid productId, 
            [FromQuery] bool enabledOnly = false)
        {
            var query = new GetProductPipelineConfigurationsQuery(productId, enabledOnly);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}