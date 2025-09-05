using MediatR;
using AutoMapper;
using Contracts.DTOs;
using DAL.Repositories;

namespace API.Features.Scripts.Queries.GetScriptsByProductAndStage
{
    /// <summary>
    /// Handler for getting scripts by product and stage
    /// </summary>
    public class GetScriptsByProductAndStageHandler : IRequestHandler<GetScriptsByProductAndStageQuery, IEnumerable<PipelineStageScriptDto>>
    {
        private readonly IPipelineStageScriptRepository _repository;
        private readonly IMapper _mapper;

        public GetScriptsByProductAndStageHandler(IPipelineStageScriptRepository repository, IMapper mapper)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<IEnumerable<PipelineStageScriptDto>> Handle(GetScriptsByProductAndStageQuery request, CancellationToken cancellationToken)
        {
            var scripts = request.EnabledOnly 
                ? await _repository.GetEnabledByProductAndStageAsync(request.ProductId, request.PipelineStageId)
                : await _repository.GetByProductAndStageAsync(request.ProductId, request.PipelineStageId);

            var dtos = new List<PipelineStageScriptDto>();
            foreach (var script in scripts)
            {
                var dto = _mapper.Map<PipelineStageScriptDto>(script);
                dto.ProductName = script.Product?.Name;
                dto.PipelineStage = _mapper.Map<PipelineStageDto>(script.PipelineStage);
                dtos.Add(dto);
            }

            return dtos;
        }
    }
}