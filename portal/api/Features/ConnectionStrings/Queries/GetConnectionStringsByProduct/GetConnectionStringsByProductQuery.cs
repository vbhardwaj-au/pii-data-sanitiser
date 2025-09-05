using MediatR;
using Contracts.DTOs;

namespace API.Features.ConnectionStrings.Queries.GetConnectionStringsByProduct
{
    public class GetConnectionStringsByProductQuery : IRequest<IEnumerable<ProductConnectionStringDto>>
    {
        public Guid ProductId { get; set; }

        public GetConnectionStringsByProductQuery(Guid productId)
        {
            ProductId = productId;
        }
    }
}