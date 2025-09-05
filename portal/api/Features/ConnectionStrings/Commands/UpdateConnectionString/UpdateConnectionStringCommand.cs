using MediatR;
using Contracts.DTOs;

namespace API.Features.ConnectionStrings.Commands.UpdateConnectionString
{
    public class UpdateConnectionStringCommand : IRequest<ProductConnectionStringDto>
    {
        public Guid Id { get; set; }
        public string ConnectionType { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public string? UpdatedBy { get; set; }
    }
}