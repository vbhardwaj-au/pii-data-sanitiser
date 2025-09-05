using MediatR;
using AutoMapper;
using Contracts.DTOs;
using DAL.Repositories;
using API.Services;

namespace API.Features.ConnectionStrings.Queries.GetConnectionStringsByProduct
{
    public class GetConnectionStringsByProductHandler : IRequestHandler<GetConnectionStringsByProductQuery, IEnumerable<ProductConnectionStringDto>>
    {
        private readonly IProductConnectionStringRepository _repository;
        private readonly IConnectionStringEncryptionService _encryptionService;
        private readonly IMapper _mapper;

        public GetConnectionStringsByProductHandler(
            IProductConnectionStringRepository repository,
            IConnectionStringEncryptionService encryptionService,
            IMapper mapper)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<IEnumerable<ProductConnectionStringDto>> Handle(GetConnectionStringsByProductQuery request, CancellationToken cancellationToken)
        {
            var connectionStrings = await _repository.GetWithProductDetailsAsync(request.ProductId);
            
            var dtos = new List<ProductConnectionStringDto>();
            foreach (var cs in connectionStrings)
            {
                var dto = _mapper.Map<ProductConnectionStringDto>(cs);
                
                // Decrypt and mask the connection string for display
                var decrypted = _encryptionService.DecryptConnectionString(cs.ConnectionString);
                dto.ConnectionString = _encryptionService.MaskConnectionString(decrypted);
                dto.ProductName = cs.Product?.Name;
                
                dtos.Add(dto);
            }

            return dtos;
        }
    }
}