using SereniTeam.Shared.DTOs;

namespace SereniTeam.Client.Services;

/// <summary>
/// Interface for real-time SignalR communication
/// </summary>
public interface ISignalRService
{
    /// <summary>
    /// Starts the SignalR connection
    /// </summary>
    Task StartConnectionAsync();

    /// <summary>
    /// Stops the SignalR connection
    /// </summary>
    Task StopConnectionAsync();

    /// <summary>
    /// Joins a team group to receive team-specific updates
    /// </summary>
    Task JoinTeamGroupAsync(int teamId);

    /// <summary>
    /// Leaves a team group
    /// </summary>
    Task LeaveTeamGroupAsync(int teamId);

    /// <summary>
    /// Event fired when a new check-in is received
    /// </summary>
    event Action<object>? OnNewCheckInReceived;

    /// <summary>
    /// Event fired when a burnout alert is received
    /// </summary>
    event Action<BurnoutAlertDto>? OnBurnoutAlertReceived;

    /// <summary>
    /// Gets whether the SignalR connection is active
    /// </summary>
    bool IsConnected { get; }
}