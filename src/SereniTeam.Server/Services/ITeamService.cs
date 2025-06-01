using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Interface for team management and analytics operations
/// </summary>
public interface ITeamService
{
    /// <summary>
    /// Gets all active teams
    /// </summary>
    /// <returns>List of active teams</returns>
    Task<List<TeamDto>> GetAllTeamsAsync();

    /// <summary>
    /// Gets a specific team by ID
    /// </summary>
    /// <param name="id">Team identifier</param>
    /// <returns>Team data or null if not found</returns>
    Task<TeamDto?> GetTeamByIdAsync(int id);

    /// <summary>
    /// Gets comprehensive team summary with analytics and trends
    /// </summary>
    /// <param name="teamId">Team identifier</param>
    /// <param name="daysBack">Number of days to include in analysis</param>
    /// <returns>Team summary with analytics or null if team not found</returns>
    Task<TeamSummaryDto?> GetTeamSummaryAsync(int teamId, int daysBack = 30);

    /// <summary>
    /// Creates a new team
    /// </summary>
    /// <param name="teamDto">Team creation data</param>
    /// <returns>ID of the created team</returns>
    Task<int> CreateTeamAsync(CreateTeamDto teamDto);

    /// <summary>
    /// Gets burnout alerts for all teams
    /// </summary>
    /// <returns>List of teams with burnout risk alerts</returns>
    Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync();
}