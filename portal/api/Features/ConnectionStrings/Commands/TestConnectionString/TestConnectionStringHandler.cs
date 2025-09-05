using MediatR;
using Contracts.DTOs;
using API.Services;

namespace API.Features.ConnectionStrings.Commands.TestConnectionString
{
    /// <summary>
    /// Handler for testing connection strings
    /// </summary>
    public class TestConnectionStringHandler : IRequestHandler<TestConnectionStringCommand, ConnectionTestResultDto>
    {
        private readonly IConnectionTestingService _connectionTestingService;
        private readonly IConnectionStringEncryptionService _encryptionService;

        public TestConnectionStringHandler(
            IConnectionTestingService connectionTestingService,
            IConnectionStringEncryptionService encryptionService)
        {
            _connectionTestingService = connectionTestingService ?? throw new ArgumentNullException(nameof(connectionTestingService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        public async Task<ConnectionTestResultDto> Handle(TestConnectionStringCommand request, CancellationToken cancellationToken)
        {
            // Validate connection string format
            if (!_encryptionService.ValidateConnectionStringFormat(request.ConnectionString))
            {
                return new ConnectionTestResultDto
                {
                    IsSuccessful = false,
                    ErrorMessage = "Invalid connection string format. Must contain Server and Database parameters.",
                    ConnectionTimeMs = 0
                };
            }

            // Test the connection
            if (request.IncludeExtendedInfo)
            {
                return await _connectionTestingService.TestConnectionWithInfoAsync(request.ConnectionString);
            }
            else
            {
                return await _connectionTestingService.TestConnectionAsync(request.ConnectionString);
            }
        }
    }
}