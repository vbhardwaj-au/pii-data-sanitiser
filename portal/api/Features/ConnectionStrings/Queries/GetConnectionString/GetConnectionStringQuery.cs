using MediatR;
using Contracts.DTOs;

namespace API.Features.ConnectionStrings.Queries.GetConnectionString
{
    public class GetConnectionStringQuery : IRequest<ProductConnectionStringDto?>
    {
        public Guid Id { get; set; }

        public GetConnectionStringQuery(Guid id)
        {
            Id = id;
        }
    }
}