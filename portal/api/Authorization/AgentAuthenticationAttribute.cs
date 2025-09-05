using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using API.Services;

namespace API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AgentAuthenticationAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Get the API key from Authorization header
        var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Unauthorized",
                message = "Agent API key is required",
                timestamp = DateTime.UtcNow
            });
            return;
        }

        var apiKey = authHeader.Substring("Bearer ".Length);
        
        // Get the agent management service from DI
        var agentManagementService = context.HttpContext.RequestServices
            .GetRequiredService<IAgentManagementService>();

        // Validate the API key
        var (isValid, agentId) = await agentManagementService.ValidateApiKeyAsync(apiKey);
        
        if (!isValid)
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                error = "Unauthorized",
                message = "Invalid agent API key",
                timestamp = DateTime.UtcNow
            });
            return;
        }

        // Store agent ID in HttpContext for use in controllers
        context.HttpContext.Items["AgentId"] = agentId;
        context.HttpContext.Items["ApiKey"] = apiKey;
    }
}