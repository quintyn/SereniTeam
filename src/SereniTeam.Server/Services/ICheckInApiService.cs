using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Interface for check-in related API operations
/// </summary>
public interface ICheckInApiService
{
    /// <summary>
    /// Submits a new anonymous check-in
    /// </summary>
    Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkIn);
}