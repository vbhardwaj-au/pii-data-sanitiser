using MediatR;
using DAL.Repositories;

namespace API.Features.ConnectionStrings.Commands.DeleteConnectionString
{
    public class DeleteConnectionStringHandler : IRequestHandler<DeleteConnectionStringCommand, bool>
    {
        private readonly IProductConnectionStringRepository _repository;

        public DeleteConnectionStringHandler(IProductConnectionStringRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<bool> Handle(DeleteConnectionStringCommand request, CancellationToken cancellationToken)
        {
            return await _repository.DeleteAsync(request.Id);
        }
    }
}