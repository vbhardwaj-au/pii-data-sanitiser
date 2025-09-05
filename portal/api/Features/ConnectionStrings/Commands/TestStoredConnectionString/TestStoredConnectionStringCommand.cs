using MediatR;
using Contracts.DTOs;

namespace API.Features.ConnectionStrings.Commands.TestStoredConnectionString
{
    /// <summary>
    /// Command to test a stored connection string by ID
    /// </summary>
    public class TestStoredConnectionStringCommand : IRequest<ConnectionTestResultDto>
    {
        /// <summary>
        /// The ID of the stored connection string to test
        /// </summary>
        public Guid ConnectionStringId { get; set; }

        /// <summary>
        /// Whether to include extended database information
        /// </summary>
        public bool IncludeExtendedInfo { get; set; } = false;

        public TestStoredConnectionStringCommand(Guid connectionStringId, bool includeExtendedInfo = false)
        {
            ConnectionStringId = connectionStringId;
            IncludeExtendedInfo = includeExtendedInfo;
        }
    }
}