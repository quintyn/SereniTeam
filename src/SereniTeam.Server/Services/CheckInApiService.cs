using SereniTeam.Shared.DTOs;
using SereniTeam.Client.Services;

namespace SereniTeam.Server.Services;

/// <summary>
/// Service for check-in related operations (Blazor Server version)
/// Calls the actual services directly instead of making HTTP requests
/// </summary>
public class CheckInApiService : SereniTeam.Client.Services.ICheckInApiService
{
    private readonly ICheckInService _checkInService;
    private readonly ILogger<CheckInApiService> _logger;

    public CheckInApiService(ICheckInService checkInService, ILogger<CheckInApiService> logger)
    {
        _checkInService = checkInService;
        _logger = logger;
    }

    public async Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkIn)
    {
        try
        {
            _logger.LogInformation("Submitting check-in for team {TeamId} via direct service call", checkIn.TeamId);
            return await _checkInService.SubmitCheckInAsync(checkIn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting check-in for team {TeamId}", checkIn.TeamId);
            return false;
        }
    }
}