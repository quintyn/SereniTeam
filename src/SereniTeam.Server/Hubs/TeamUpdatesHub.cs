using Microsoft.AspNetCore.SignalR;

namespace SereniTeam.Server.Hubs;

/// <summary>
/// SignalR hub for real-time team updates and notifications
/// </summary>
public class TeamUpdatesHub : Hub
{
    private readonly ILogger<TeamUpdatesHub> _logger;

    public TeamUpdatesHub(ILogger<TeamUpdatesHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Joins a team group to receive updates for that specific team
    /// </summary>
    public async Task JoinTeamGroup(int teamId)
    {
        var groupName = $"Team_{teamId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation("Connection {ConnectionId} joined team group {GroupName}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leaves a team group
    /// </summary>
    public async Task LeaveTeamGroup(int teamId)
    {
        var groupName = $"Team_{teamId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation("Connection {ConnectionId} left team group {GroupName}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}