using Microsoft.AspNetCore.SignalR;
using API.Authorization;

namespace API.Hubs;

[RequireRole(Roles.Admin, Roles.ProductAdmin, Roles.Analyst)]
public class MonitoringHub : Hub
{
    private readonly ILogger<MonitoringHub> _logger;

    public MonitoringHub(ILogger<MonitoringHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "MonitoringUsers");
        _logger.LogInformation("User {ConnectionId} connected to monitoring hub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "MonitoringUsers");
        _logger.LogInformation("User {ConnectionId} disconnected from monitoring hub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinAgentMonitoring()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "AgentMonitoring");
        _logger.LogDebug("User {ConnectionId} joined agent monitoring", Context.ConnectionId);
    }

    public async Task LeaveAgentMonitoring()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AgentMonitoring");
        _logger.LogDebug("User {ConnectionId} left agent monitoring", Context.ConnectionId);
    }

    public async Task JoinJobMonitoring()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "JobMonitoring");
        _logger.LogDebug("User {ConnectionId} joined job monitoring", Context.ConnectionId);
    }

    public async Task LeaveJobMonitoring()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "JobMonitoring");
        _logger.LogDebug("User {ConnectionId} left job monitoring", Context.ConnectionId);
    }
}