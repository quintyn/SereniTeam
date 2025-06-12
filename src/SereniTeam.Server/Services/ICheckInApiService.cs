using SereniTeam.Shared.DTOs;

namespace SereniTeam.Client.Services;

/// <summary>
/// Interface for check-in API operations - implemented differently for WASM vs Server
/// </summary>
public interface ICheckInApiService
{
    Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkIn);
    Task<List<CheckInDto>> GetTeamCheckInsAsync(int teamId, int daysBack = 30);
}