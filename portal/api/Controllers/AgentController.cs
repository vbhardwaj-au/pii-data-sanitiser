using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using API.Authorization;
using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;
using API.Services;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Distributed Agent Management")]
public class AgentController : ControllerBase
{
    private readonly IObfuscationAgentRepository _agentRepository;
    private readonly IObfuscationJobRepository _jobRepository;
    private readonly IAgentManagementService _agentManagementService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IObfuscationAgentRepository agentRepository,
        IObfuscationJobRepository jobRepository,
        IAgentManagementService agentManagementService,
        ILogger<AgentController> logger)
    {
        _agentRepository = agentRepository;
        _jobRepository = jobRepository;
        _agentManagementService = agentManagementService;
        _logger = logger;
    }

    /// <summary>
    /// Pre-register an agent to get its ID before container deployment
    /// </summary>
    [HttpPost("pre-register")]
    [SwaggerOperation(Summary = "Pre-Register Agent", Description = "Pre-register an agent to get its ID for container deployment")]
    [SwaggerResponse(200, "Agent pre-registered successfully", typeof(AgentRegistrationResponse))]
    public async Task<ActionResult<AgentRegistrationResponse>> PreRegisterAgent([FromBody] AgentRegistrationRequest request)
    {
        try
        {
            // Generate a new agent ID for pre-registration
            var agentId = Guid.NewGuid();
            request.AgentId = agentId;
            
            // Register the agent
            var response = await _agentManagementService.RegisterAgentAsync(request);
            
            _logger.LogInformation("Pre-registered agent {AgentName} with ID {AgentId}", 
                request.Name, agentId);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pre-register agent {AgentName}", request.Name);
            return StatusCode(500, new AgentRegistrationResponse
            {
                Success = false,
                ErrorMessage = "Failed to pre-register agent"
            });
        }
    }

    /// <summary>
    /// Register a new obfuscation agent
    /// </summary>
    [HttpPost("register")]
    [SwaggerOperation(Summary = "Register Agent", Description = "Register a new obfuscation agent with the portal")]
    [SwaggerResponse(200, "Agent registered successfully", typeof(AgentRegistrationResponse))]
    [SwaggerResponse(400, "Invalid registration request")]
    [SwaggerResponse(409, "Agent already exists")]
    public async Task<ActionResult<AgentRegistrationResponse>> RegisterAgent([FromBody] AgentRegistrationRequest request)
    {
        try
        {
            // If AgentId is provided, try to re-register existing agent
            if (request.AgentId.HasValue && request.AgentId.Value != Guid.Empty)
            {
                var existingById = await _agentRepository.GetByIdAsync(request.AgentId.Value);
                if (existingById != null)
                {
                    // Update existing agent and return success
                    var updateResponse = await _agentManagementService.UpdateAgentRegistrationAsync(request);
                    _logger.LogInformation("Re-registered existing agent {AgentName} with ID {AgentId}", 
                        request.Name, request.AgentId);
                    return Ok(updateResponse);
                }
                // If agent ID doesn't exist, continue with new registration using that ID
            }

            // Check if agent already exists by hostname and port (for backward compatibility)
            var existingAgent = await _agentRepository.GetByHostAndPortAsync(request.HostName, request.Port);
            if (existingAgent != null)
            {
                // Instead of returning conflict, update the existing agent
                request.AgentId = existingAgent.Id;
                var updateResponse = await _agentManagementService.UpdateAgentRegistrationAsync(request);
                _logger.LogInformation("Updated existing agent {AgentName} at {HostName}:{Port}", 
                    request.Name, request.HostName, request.Port);
                return Ok(updateResponse);
            }

            // Check for name conflicts only if it's a new agent
            var existingByName = await _agentRepository.GetByNameAsync(request.Name);
            if (existingByName != null && (!request.AgentId.HasValue || request.AgentId.Value != existingByName.Id))
            {
                // Update the name conflict to use the existing agent
                request.AgentId = existingByName.Id;
                var updateResponse = await _agentManagementService.UpdateAgentRegistrationAsync(request);
                _logger.LogInformation("Updated existing agent by name {AgentName}", request.Name);
                return Ok(updateResponse);
            }

            var response = await _agentManagementService.RegisterAgentAsync(request);
            
            _logger.LogInformation("Successfully registered new agent {AgentName} at {HostName}:{Port}", 
                request.Name, request.HostName, request.Port);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent {AgentName}", request.Name);
            return StatusCode(500, new AgentRegistrationResponse
            {
                Success = false,
                ErrorMessage = "Internal server error during agent registration"
            });
        }
    }

    /// <summary>
    /// Agent heartbeat to maintain connection and receive jobs
    /// </summary>
    [HttpPost("{agentId}/heartbeat")]
    [SwaggerOperation(Summary = "Agent Heartbeat", Description = "Agent heartbeat to maintain connection and receive job assignments")]
    [SwaggerResponse(200, "Heartbeat processed successfully", typeof(AgentHeartbeatResponse))]
    [SwaggerResponse(404, "Agent not found")]
    public async Task<ActionResult<AgentHeartbeatResponse>> Heartbeat(
        Guid agentId, 
        [FromBody] AgentHeartbeatRequest request)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
            {
                return NotFound(new AgentHeartbeatResponse
                {
                    Success = false,
                    Message = "Agent not found"
                });
            }

            var response = await _agentManagementService.ProcessHeartbeatAsync(agentId, request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process heartbeat for agent {AgentId}", agentId);
            return StatusCode(500, new AgentHeartbeatResponse
            {
                Success = false,
                Message = "Internal server error during heartbeat processing"
            });
        }
    }

    /// <summary>
    /// Get list of all registered agents
    /// </summary>
    [HttpGet]
    [RequireRole(Roles.Admin, Roles.ProductAdmin, Roles.Analyst)]
    [SwaggerOperation(Summary = "List Agents", Description = "Get list of all registered agents")]
    [SwaggerResponse(200, "Agents retrieved successfully", typeof(IEnumerable<ObfuscationAgent>))]
    public async Task<ActionResult<IEnumerable<ObfuscationAgent>>> GetAgents()
    {
        try
        {
            var agents = await _agentRepository.GetAllAsync();
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve agents list");
            return StatusCode(500, new { error = "Failed to retrieve agents" });
        }
    }

    /// <summary>
    /// Get agent details by ID
    /// </summary>
    [HttpGet("{agentId}")]
    [RequireRole(Roles.Admin, Roles.ProductAdmin, Roles.Analyst)]
    [SwaggerOperation(Summary = "Get Agent", Description = "Get agent details by ID")]
    [SwaggerResponse(200, "Agent retrieved successfully")]
    [SwaggerResponse(404, "Agent not found")]
    public async Task<ActionResult<ObfuscationAgent>> GetAgent(Guid agentId)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve agent {AgentId}", agentId);
            return StatusCode(500, new { error = "Failed to retrieve agent" });
        }
    }

    /// <summary>
    /// Update agent configuration
    /// </summary>
    [HttpPut("{agentId}")]
    [RequireRole(Roles.Admin, Roles.ProductAdmin)]
    [SwaggerOperation(Summary = "Update Agent", Description = "Update agent configuration")]
    [SwaggerResponse(200, "Agent updated successfully")]
    [SwaggerResponse(404, "Agent not found")]
    public async Task<ActionResult> UpdateAgent(Guid agentId, [FromBody] AgentUpdateRequest request)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            // Update agent properties
            if (request.IsEnabled.HasValue)
                agent.IsEnabled = request.IsEnabled.Value;
            
            if (request.MaxConcurrentJobs.HasValue)
                agent.MaxConcurrentJobs = request.MaxConcurrentJobs.Value;
            
            if (request.Priority.HasValue)
                agent.Priority = request.Priority.Value;
            
            if (!string.IsNullOrEmpty(request.Notes))
                agent.Notes = request.Notes;

            await _agentRepository.UpdateAsync(agent);
            
            _logger.LogInformation("Updated agent {AgentName} configuration", agent.Name);
            return Ok(new { message = "Agent updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update agent {AgentId}", agentId);
            return StatusCode(500, new { error = "Failed to update agent" });
        }
    }

    /// <summary>
    /// Delete/unregister an agent
    /// </summary>
    [HttpDelete("{agentId}")]
    [RequireRole(Roles.Admin)]
    [SwaggerOperation(Summary = "Delete Agent", Description = "Delete/unregister an agent")]
    [SwaggerResponse(200, "Agent deleted successfully")]
    [SwaggerResponse(404, "Agent not found")]
    [SwaggerResponse(400, "Agent has active jobs")]
    public async Task<ActionResult> DeleteAgent(Guid agentId)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            // Check for active jobs
            if (agent.ActiveJobs > 0)
            {
                return BadRequest(new { error = "Cannot delete agent with active jobs" });
            }

            var success = await _agentRepository.DeleteAsync(agentId);
            if (success)
            {
                _logger.LogInformation("Deleted agent {AgentName}", agent.Name);
                return Ok(new { message = "Agent deleted successfully" });
            }
            else
            {
                return NotFound(new { error = "Agent not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete agent {AgentId}", agentId);
            return StatusCode(500, new { error = "Failed to delete agent" });
        }
    }

    /// <summary>
    /// Get jobs for a specific agent
    /// </summary>
    [HttpGet("{agentId}/jobs")]
    [RequireRole(Roles.Admin, Roles.ProductAdmin, Roles.Analyst)]
    [SwaggerOperation(Summary = "Get Agent Jobs", Description = "Get jobs assigned to a specific agent")]
    [SwaggerResponse(200, "Jobs retrieved successfully")]
    public async Task<ActionResult> GetAgentJobs(Guid agentId)
    {
        try
        {
            var jobs = await _jobRepository.GetByAgentIdAsync(agentId);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve jobs for agent {AgentId}", agentId);
            return StatusCode(500, new { error = "Failed to retrieve agent jobs" });
        }
    }
}

public class AgentUpdateRequest
{
    public bool? IsEnabled { get; set; }
    public int? MaxConcurrentJobs { get; set; }
    public int? Priority { get; set; }
    public string? Notes { get; set; }
}