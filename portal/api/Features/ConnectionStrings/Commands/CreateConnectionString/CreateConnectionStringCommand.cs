using MediatR;
using Contracts.DTOs;

namespace API.Features.ConnectionStrings.Commands.CreateConnectionString
{
    /// <summary>
    /// Command to create a new product connection string
    /// </summary>
    public class CreateConnectionStringCommand : IRequest<ProductConnectionStringDto>
    {
        public Guid ProductId { get; set; }
        public string ConnectionType { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CreatedBy { get; set; }
    }
}