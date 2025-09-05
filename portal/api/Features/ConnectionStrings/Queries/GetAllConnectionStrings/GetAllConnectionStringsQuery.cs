using MediatR;
using Contracts.DTOs;

namespace API.Features.ConnectionStrings.Queries.GetAllConnectionStrings
{
    public class GetAllConnectionStringsQuery : IRequest<IEnumerable<ProductConnectionStringDto>>
    {
        public Guid? ProductId { get; set; }

        public GetAllConnectionStringsQuery(Guid? productId = null)
        {
            ProductId = productId;
        }
    }
}