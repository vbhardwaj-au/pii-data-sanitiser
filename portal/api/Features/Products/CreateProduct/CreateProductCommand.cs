using MediatR;
using Contracts.DTOs;

namespace API.Features.Products.CreateProduct;

public record CreateProductCommand(CreateProductDto Dto) : IRequest<ProductDto?>;