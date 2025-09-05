using Contracts.DTOs;

namespace API.Services;

public interface IAgentManagementService
{
    Task<AgentRegistrationResponse> RegisterAgentAsync(AgentRegistrationRequest request);
    Task<AgentRegistrationResponse> UpdateAgentRegistrationAsync(AgentRegistrationRequest request);
    Task<AgentHeartbeatResponse> ProcessHeartbeatAsync(Guid agentId, AgentHeartbeatRequest request);
    Task<string> GenerateApiKeyForAgentAsync(Guid agentId);
    Task<(bool IsValid, Guid AgentId)> ValidateApiKeyAsync(string apiKey);
    Task MarkOfflineAgentsAsync();
}