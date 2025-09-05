using MediatR;
using AutoMapper;
using Contracts.DTOs;
using DAL.Repositories;

namespace API.Features.ProductPipelineConfigurations.Queries.GetProductPipelineConfigurations
{
    /// <summary>
    /// Handler for getting product pipeline configurations
    /// </summary>
    public class GetProductPipelineConfigurationsHandler : IRequestHandler<GetProductPipelineConfigurationsQuery, IEnumerable<ProductPipelineConfigurationDto>>
    {
        private readonly IProductPipelineConfigurationRepository _repository;
        private readonly IMapper _mapper;

        public GetProductPipelineConfigurationsHandler(
            IProductPipelineConfigurationRepository repository, 
            IMapper mapper)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<IEnumerable<ProductPipelineConfigurationDto>> Handle(GetProductPipelineConfigurationsQuery request, CancellationToken cancellationToken)
        {
            var configurations = request.EnabledOnly 
                ? await _repository.GetEnabledByProductAsync(request.ProductId)
                : await _repository.GetByProductIdAsync(request.ProductId);

            var dtos = new List<ProductPipelineConfigurationDto>();
            foreach (var config in configurations)
            {
                var dto = _mapper.Map<ProductPipelineConfigurationDto>(config);
                dto.ProductName = config.Product?.Name;
                dto.PipelineStage = _mapper.Map<PipelineStageDto>(config.PipelineStage);
                dtos.Add(dto);
            }

            return dtos;
        }
    }
}