using AutoMapper;
using Contracts.DTOs;
using DAL.Repositories;
using MediatR;
using Microsoft.Data.SqlClient;
using API.Features.Products.RefreshProductSchema;

namespace API.Features.Products.GetProductMappings;

public record GetProductMappingsQuery(Guid ProductId) : IRequest<ProductMappingsDto?>;

public class GetProductMappingsQueryHandler : IRequestHandler<GetProductMappingsQuery, ProductMappingsDto?>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;
    private readonly IMediator _mediator;

    public GetProductMappingsQueryHandler(IProductRepository productRepository, IMapper mapper, IMediator mediator)
    {
        _productRepository = productRepository;
        _mapper = mapper;
        _mediator = mediator;
    }

    public async Task<ProductMappingsDto?> Handle(GetProductMappingsQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId);
        if (product == null)
            return null;

        // If our stored schema snapshot looks incomplete compared to the live DB, refresh it
        try
        {
            if ((product.DatabaseTechnology ?? string.Empty).Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
            {
                var csb = new SqlConnectionStringBuilder(product.ConnectionString)
                {
                    TrustServerCertificate = true,
                    ConnectTimeout = 15
                };

                var liveTableCount = 0;
                await using (var conn = new SqlConnection(csb.ToString()))
                {
                    await conn.OpenAsync(cancellationToken);
                    var cmd = new SqlCommand(@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", conn);
                    var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                    liveTableCount = Convert.ToInt32(obj);
                }

                var storedCount = product.DatabaseSchemas?.Count ?? 0;
                if (liveTableCount > storedCount)
                {
                    // Perform a schema refresh to capture all tables and columns
                    await _mediator.Send(new RefreshProductSchemaCommand(request.ProductId), cancellationToken);
                    // Re-load the product with the newly refreshed schema
                    product = await _productRepository.GetByIdAsync(request.ProductId);
                    if (product == null) return null;
                }
            }
        }
        catch
        {
            // Non-fatal: if live check fails, fall back to whatever we have stored
        }

        var result = _mapper.Map<ProductMappingsDto>(product);
        
        // Debug logging
        Console.WriteLine($"ProductConnectionStrings count: {product.ProductConnectionStrings?.Count ?? 0}");
        if (product.ProductConnectionStrings != null)
        {
            foreach (var pcs in product.ProductConnectionStrings)
            {
                Console.WriteLine($"Connection: Type={pcs.ConnectionType}, Active={pcs.IsActive}");
            }
        }
        
        // Manually set the SourceConnectionString since AutoMapper complex expression isn't working
        var sourceConnection = product.ProductConnectionStrings?
            .FirstOrDefault(pcs => pcs.ConnectionType == "Source" && pcs.IsActive);
        result.SourceConnectionString = sourceConnection?.ConnectionString ?? string.Empty;
        
        Console.WriteLine($"Final SourceConnectionString: {(!string.IsNullOrEmpty(result.SourceConnectionString) ? "SET" : "EMPTY")}");
        
        return result;
    }
}
