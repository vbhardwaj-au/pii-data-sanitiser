using MediatR;
using AutoMapper;
using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;
using API.Services;

namespace API.Features.ConnectionStrings.Commands.UpdateConnectionString
{
    public class UpdateConnectionStringHandler : IRequestHandler<UpdateConnectionStringCommand, ProductConnectionStringDto>
    {
        private readonly IProductConnectionStringRepository _repository;
        private readonly IConnectionStringEncryptionService _encryptionService;
        private readonly IMapper _mapper;

        public UpdateConnectionStringHandler(
            IProductConnectionStringRepository repository,
            IConnectionStringEncryptionService encryptionService,
            IMapper mapper)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<ProductConnectionStringDto> Handle(UpdateConnectionStringCommand request, CancellationToken cancellationToken)
        {
            // Get existing connection string
            var existing = await _repository.GetByIdAsync(request.Id);
            if (existing == null)
            {
                throw new ArgumentException($"Connection string with ID {request.Id} not found");
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

            // Check if connection type already exists for another connection of the same product
            if (await _repository.ExistsAsync(existing.ProductId, request.ConnectionType, request.Id))
            {
                throw new InvalidOperationException($"Connection type '{request.ConnectionType}' already exists for this product");
            }

            // Update the entity
            var updatedEntity = new ProductConnectionString
            {
                Id = request.Id,
                ProductId = existing.ProductId,
                ConnectionType = request.ConnectionType,
                ConnectionString = _encryptionService.EncryptConnectionString(request.ConnectionString),
                Description = request.Description,
                IsActive = request.IsActive,
                UpdatedBy = request.UpdatedBy,
                CreatedBy = existing.CreatedBy,
                CreatedAt = existing.CreatedAt
            };

            // Save to database
            var updated = await _repository.UpdateAsync(updatedEntity);

            // Map to DTO and mask connection string
            var dto = _mapper.Map<ProductConnectionStringDto>(updated);
            dto.ConnectionString = _encryptionService.MaskConnectionString(request.ConnectionString);
            dto.ProductName = updated.Product?.Name;

            return dto;
        }
    }
}