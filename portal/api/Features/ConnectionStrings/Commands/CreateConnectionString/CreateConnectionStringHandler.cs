using MediatR;
using AutoMapper;
using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;
using API.Services;

namespace API.Features.ConnectionStrings.Commands.CreateConnectionString
{
    /// <summary>
    /// Handler for creating new connection strings
    /// </summary>
    public class CreateConnectionStringHandler : IRequestHandler<CreateConnectionStringCommand, ProductConnectionStringDto>
    {
        private readonly IProductConnectionStringRepository _repository;
        private readonly IProductRepository _productRepository;
        private readonly IConnectionStringEncryptionService _encryptionService;
        private readonly IMapper _mapper;

        public CreateConnectionStringHandler(
            IProductConnectionStringRepository repository,
            IProductRepository productRepository,
            IConnectionStringEncryptionService encryptionService,
            IMapper mapper)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<ProductConnectionStringDto> Handle(CreateConnectionStringCommand request, CancellationToken cancellationToken)
        {
            // Validate product exists
            var product = await _productRepository.GetByIdAsync(request.ProductId);
            if (product == null)
            {
                throw new ArgumentException($"Product with ID {request.ProductId} not found");
            }

            // Validate connection type
            if (!ConnectionTypes.IsValid(request.ConnectionType))
            {
                throw new ArgumentException($"Invalid connection type: {request.ConnectionType}. Valid types are: {string.Join(", ", ConnectionTypes.All)}");
            }

            // Validate connection string format
            if (!_encryptionService.ValidateConnectionStringFormat(request.ConnectionString))
            {
                throw new ArgumentException("Invalid connection string format. Must contain Server and Database parameters.");
            }

            // Check if connection type already exists for this product
            var existingConnection = await _repository.GetByProductIdAndTypeAsync(request.ProductId, request.ConnectionType);
            if (existingConnection != null)
            {
                throw new InvalidOperationException($"Connection type '{request.ConnectionType}' already exists for product '{product.Name}'");
            }

            // Create new connection string entity
            var connectionString = new ProductConnectionString
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                ConnectionType = request.ConnectionType,
                ConnectionString = _encryptionService.EncryptConnectionString(request.ConnectionString),
                Description = request.Description,
                IsActive = request.IsActive,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Save to database
            var created = await _repository.CreateAsync(connectionString);

            // Map to DTO and mask connection string
            var dto = _mapper.Map<ProductConnectionStringDto>(created);
            dto.ConnectionString = _encryptionService.MaskConnectionString(request.ConnectionString);
            dto.ProductName = product.Name;

            return dto;
        }
    }
}