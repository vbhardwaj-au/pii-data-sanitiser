using MediatR;
using AutoMapper;
using Contracts.DTOs;
using DAL.Repositories;
using API.Services;

namespace API.Features.ConnectionStrings.Queries.GetConnectionString
{
    public class GetConnectionStringHandler : IRequestHandler<GetConnectionStringQuery, ProductConnectionStringDto?>
    {
        private readonly IProductConnectionStringRepository _repository;
        private readonly IConnectionStringEncryptionService _encryptionService;
        private readonly IMapper _mapper;

        public GetConnectionStringHandler(
            IProductConnectionStringRepository repository,
            IConnectionStringEncryptionService encryptionService,
            IMapper mapper)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<ProductConnectionStringDto?> Handle(GetConnectionStringQuery request, CancellationToken cancellationToken)
        {
            var connectionString = await _repository.GetByIdAsync(request.Id);
            if (connectionString == null)
                return null;

            var dto = _mapper.Map<ProductConnectionStringDto>(connectionString);
            
            // Decrypt and mask the connection string for display
            var decrypted = _encryptionService.DecryptConnectionString(connectionString.ConnectionString);
            dto.ConnectionString = _encryptionService.MaskConnectionString(decrypted);
            dto.ProductName = connectionString.Product?.Name;

            return dto;
        }
    }
}