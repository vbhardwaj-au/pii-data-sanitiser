using MediatR;
using Contracts.DTOs;

namespace API.Features.ProductPipelineConfigurations.Queries.GetProductPipelineConfigurations
{
    /// <summary>
    /// Query to get pipeline configurations for a product
    /// </summary>
    public class GetProductPipelineConfigurationsQuery : IRequest<IEnumerable<ProductPipelineConfigurationDto>>
    {
        /// <summary>
        /// Product ID to get configurations for
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Whether to include only enabled configurations
        /// </summary>
        public bool EnabledOnly { get; set; } = false;

        public GetProductPipelineConfigurationsQuery(Guid productId, bool enabledOnly = false)
        {
            ProductId = productId;
            EnabledOnly = enabledOnly;
        }
    }
}