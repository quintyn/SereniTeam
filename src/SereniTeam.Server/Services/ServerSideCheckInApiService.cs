using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Server-side implementation of ICheckInApiService for Blazor Server mode
/// This replaces HTTP calls with direct service calls
/// </summary>
public class ServerSideCheckInApiService : SereniTeam.Client.Services.ICheckInApiService
{
    private readonly ICheckInService _checkInService;
    private readonly ILogger<ServerSideCheckInApiService> _logger;

    public ServerSideCheckInApiService(ICheckInService checkInService, ILogger<ServerSideCheckInApiService> logger)
    {
        _checkInService = checkInService;
        _logger = logger;
    }

    public async Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkIn)
    {
        try
        {
            _logger.LogDebug("ServerSideCheckInApiService: Submitting check-in for team {TeamId}", checkIn.TeamId);
            var result = await _checkInService.SubmitCheckInAsync(checkIn);
            _logger.LogDebug("ServerSideCheckInApiService: Check-in submission result: {Success}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideCheckInApiService: Error submitting check-in for team {TeamId}", checkIn.TeamId);
            return false;
        }
    }
}