using MediatR;
using Contracts.DTOs;

namespace API.Features.Scripts.Commands.CreateScript
{
    /// <summary>
    /// Command to create a new pipeline stage script
    /// </summary>
    public class CreateScriptCommand : IRequest<PipelineStageScriptDto>
    {
        public Guid ProductId { get; set; }
        public Guid PipelineStageId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SqlScript { get; set; } = string.Empty;
        public int? ExecutionOrder { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int TimeoutMinutes { get; set; } = 30;
        public string OnFailureAction { get; set; } = "Stop";
        public int RetryAttempts { get; set; } = 0;
        public int RetryDelaySeconds { get; set; } = 30;
        public string ExpectedResultType { get; set; } = "None";
        public long? ExpectedMinRows { get; set; }
        public long? ExpectedMaxRows { get; set; }
        public string? Tags { get; set; }
        public string? CreatedBy { get; set; }
    }
}