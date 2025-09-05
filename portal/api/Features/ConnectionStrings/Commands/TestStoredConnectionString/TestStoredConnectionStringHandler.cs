using MediatR;
using Contracts.DTOs;
using API.Services;
using DAL.Repositories;

namespace API.Features.ConnectionStrings.Commands.TestStoredConnectionString
{
    /// <summary>
    /// Handler for testing stored connection strings
    /// </summary>
    public class TestStoredConnectionStringHandler : IRequestHandler<TestStoredConnectionStringCommand, ConnectionTestResultDto>
    {
        private readonly IProductConnectionStringRepository _repository;
        private readonly IConnectionTestingService _connectionTestingService;
        private readonly IConnectionStringEncryptionService _encryptionService;

        public TestStoredConnectionStringHandler(
            IProductConnectionStringRepository repository,
            IConnectionTestingService connectionTestingService,
            IConnectionStringEncryptionService encryptionService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _connectionTestingService = connectionTestingService ?? throw new ArgumentNullException(nameof(connectionTestingService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        public async Task<ConnectionTestResultDto> Handle(TestStoredConnectionStringCommand request, CancellationToken cancellationToken)
        {
            // Get the stored connection string
            var storedConnection = await _repository.GetByIdAsync(request.ConnectionStringId);
            if (storedConnection == null)
            {
                return new ConnectionTestResultDto
                {
                    IsSuccessful = false,
                    ErrorMessage = $"Connection string with ID {request.ConnectionStringId} not found.",
                    ConnectionTimeMs = 0
                };
            }

            // Check if connection string is active
            if (!storedConnection.IsActive)
            {
                return new ConnectionTestResultDto
                {
                    IsSuccessful = false,
                    ErrorMessage = "Connection string is not active and cannot be tested.",
                    ConnectionTimeMs = 0
                };
            }

            // Decrypt the connection string
            string decryptedConnectionString;
            try
            {
                decryptedConnectionString = _encryptionService.DecryptConnectionString(storedConnection.ConnectionString);
            }
            catch (Exception ex)
            {
                return new ConnectionTestResultDto
                {
                    IsSuccessful = false,
                    ErrorMessage = $"Failed to decrypt connection string: {ex.Message}",
                    ConnectionTimeMs = 0
                };
            }

            // Test the connection
            ConnectionTestResultDto result;
            if (request.IncludeExtendedInfo)
            {
                result = await _connectionTestingService.TestConnectionWithInfoAsync(decryptedConnectionString);
            }
            else
            {
                result = await _connectionTestingService.TestConnectionAsync(decryptedConnectionString);
            }

            // Add metadata about the tested connection string
            result.AdditionalInfo["ConnectionType"] = storedConnection.ConnectionType;
            result.AdditionalInfo["ProductName"] = storedConnection.Product?.Name ?? "Unknown";
            if (!string.IsNullOrEmpty(storedConnection.Description))
            {
                result.AdditionalInfo["Description"] = storedConnection.Description;
            }

            return result;
        }
    }
}