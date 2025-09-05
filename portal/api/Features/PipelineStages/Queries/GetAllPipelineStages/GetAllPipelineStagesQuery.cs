using MediatR;
using Contracts.DTOs;

namespace API.Features.PipelineStages.Queries.GetAllPipelineStages
{
    /// <summary>
    /// Query to get all pipeline stages
    /// </summary>
    public class GetAllPipelineStagesQuery : IRequest<IEnumerable<PipelineStageDto>>
    {
        /// <summary>
        /// Whether to include only active stages
        /// </summary>
        public bool ActiveOnly { get; set; } = false;

        public GetAllPipelineStagesQuery(bool activeOnly = false)
        {
            ActiveOnly = activeOnly;
        }
    }
}