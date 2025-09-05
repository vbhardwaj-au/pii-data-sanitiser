using MediatR;
using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;

namespace API.Features.Products.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto?>
{
    private readonly IProductRepository _productRepository;

    public CreateProductCommandHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductDto?> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        
        // Create new product entity
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            ConnectionString = dto.ConnectionString,
            DatabaseTechnology = dto.DatabaseTechnology,
            IsActive = dto.IsActive,
            GlobalSeed = dto.GlobalSeed,
            BatchSize = dto.BatchSize,
            SqlBatchSize = dto.SqlBatchSize,
            ParallelThreads = dto.ParallelThreads,
            MaxCacheSize = dto.MaxCacheSize,
            CommandTimeoutSeconds = dto.CommandTimeoutSeconds,
            MappingCacheDirectory = dto.MappingCacheDirectory ?? "mappings/",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "system",
            UpdatedBy = "system"
        };
        
        // Add to repository
        product = await _productRepository.CreateAsync(product);
        
        // Return DTO
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ConnectionString = product.ConnectionString,
            DatabaseTechnology = product.DatabaseTechnology,
            IsActive = product.IsActive,
            GlobalSeed = product.GlobalSeed,
            BatchSize = product.BatchSize,
            SqlBatchSize = product.SqlBatchSize,
            ParallelThreads = product.ParallelThreads,
            MaxCacheSize = product.MaxCacheSize,
            CommandTimeoutSeconds = product.CommandTimeoutSeconds,
            MappingCacheDirectory = product.MappingCacheDirectory,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            CreatedBy = product.CreatedBy,
            UpdatedBy = product.UpdatedBy
        };
    }
}