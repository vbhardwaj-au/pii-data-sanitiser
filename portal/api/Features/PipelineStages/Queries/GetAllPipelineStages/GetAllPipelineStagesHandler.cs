using MediatR;
using AutoMapper;
using Contracts.DTOs;
using DAL.Repositories;

namespace API.Features.PipelineStages.Queries.GetAllPipelineStages
{
    /// <summary>
    /// Handler for getting all pipeline stages
    /// </summary>
    public class GetAllPipelineStagesHandler : IRequestHandler<GetAllPipelineStagesQuery, IEnumerable<PipelineStageDto>>
    {
        private readonly IPipelineStageRepository _repository;
        private readonly IMapper _mapper;

        public GetAllPipelineStagesHandler(IPipelineStageRepository repository, IMapper mapper)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<IEnumerable<PipelineStageDto>> Handle(GetAllPipelineStagesQuery request, CancellationToken cancellationToken)
        {
            var stages = request.ActiveOnly 
                ? await _repository.GetActiveAsync()
                : await _repository.GetAllAsync();

            return _mapper.Map<IEnumerable<PipelineStageDto>>(stages);
        }
    }
}