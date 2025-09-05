using MediatR;
using Contracts.DTOs;

namespace API.Features.Scripts.Queries.GetScriptsByProductAndStage
{
    /// <summary>
    /// Query to get scripts for a specific product and pipeline stage
    /// </summary>
    public class GetScriptsByProductAndStageQuery : IRequest<IEnumerable<PipelineStageScriptDto>>
    {
        public Guid ProductId { get; set; }
        public Guid PipelineStageId { get; set; }
        public bool EnabledOnly { get; set; } = false;

        public GetScriptsByProductAndStageQuery(Guid productId, Guid pipelineStageId, bool enabledOnly = false)
        {
            ProductId = productId;
            PipelineStageId = pipelineStageId;
            EnabledOnly = enabledOnly;
        }
    }
}