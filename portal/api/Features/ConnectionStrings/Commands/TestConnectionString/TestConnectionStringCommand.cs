using MediatR;
using Contracts.DTOs;

namespace API.Features.ConnectionStrings.Commands.TestConnectionString
{
    /// <summary>
    /// Command to test a connection string
    /// </summary>
    public class TestConnectionStringCommand : IRequest<ConnectionTestResultDto>
    {
        /// <summary>
        /// The connection string to test
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Whether to include extended database information
        /// </summary>
        public bool IncludeExtendedInfo { get; set; } = false;
    }
}