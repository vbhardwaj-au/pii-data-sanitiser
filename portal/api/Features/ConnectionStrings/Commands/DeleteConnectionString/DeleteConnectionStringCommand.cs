using MediatR;

namespace API.Features.ConnectionStrings.Commands.DeleteConnectionString
{
    public class DeleteConnectionStringCommand : IRequest<bool>
    {
        public Guid Id { get; set; }

        public DeleteConnectionStringCommand(Guid id)
        {
            Id = id;
        }
    }
}