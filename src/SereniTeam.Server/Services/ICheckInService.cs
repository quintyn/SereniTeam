using SereniTeam.Shared.Models;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Interface for check-in related operations
/// </summary>
public interface ICheckInService
{
    /// <summary>
    /// Submits a new anonymous check-in and broadcasts real-time update
    /// </summary>
    /// <param name="checkInDto">Check-in data to submit</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkInDto);

    /// <summary>
    /// Retrieves check-ins for a specific team
    /// </summary>
    /// <param name="teamId">Team identifier</param>
    /// <param name="fromDate">Optional start date filter</param>
    /// <returns>List of check-ins for the team</returns>
    Task<List<CheckIn>> GetTeamCheckInsAsync(int teamId, DateTime? fromDate = null);
}