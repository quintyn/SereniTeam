using Microsoft.AspNetCore.SignalR.Client;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Client.Services;

/// <summary>
/// Service for real-time SignalR communication
/// </summary>
public class SignalRService : ISignalRService, IAsyncDisposable
{
    private readonly HubConnection _hubConnection;

    public event Action<object>? OnNewCheckInReceived;
    public event Action<BurnoutAlertDto>? OnBurnoutAlertReceived;

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    public SignalRService(IConfiguration configuration)
    {
        var baseUrl = configuration["BaseUrl"] ?? "https://localhost:7000";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/teamupdates")
            .WithAutomaticReconnect()
            .Build();

        // Register event handlers
        _hubConnection.On<object>("NewCheckInReceived", (data) =>
        {
            OnNewCheckInReceived?.Invoke(data);
        });

        _hubConnection.On<BurnoutAlertDto>("BurnoutAlertTriggered", (alert) =>
        {
            OnBurnoutAlertReceived?.Invoke(alert);
        });
    }

    public async Task StartConnectionAsync()
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            await _hubConnection.StartAsync();
        }
    }

    public async Task StopConnectionAsync()
    {
        if (_hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.StopAsync();
        }
    }

    public async Task JoinTeamGroupAsync(int teamId)
    {
        if (IsConnected)
        {
            await _hubConnection.InvokeAsync("JoinTeamGroup", teamId);
        }
    }

    public async Task LeaveTeamGroupAsync(int teamId)
    {
        if (IsConnected)
        {
            await _hubConnection.InvokeAsync("LeaveTeamGroup", teamId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}