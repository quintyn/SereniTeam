using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Interface for team-related API operations
/// </summary>
public interface ITeamApiService
{
	/// <summary>
	/// Gets all active teams
	/// </summary>
	Task<List<TeamDto>> GetAllTeamsAsync();

	/// <summary>
	/// Gets a specific team by ID
	/// </summary>
	Task<TeamDto?> GetTeamByIdAsync(int id);

	/// <summary>
	/// Gets team summary with analytics and trends
	/// </summary>
	Task<TeamSummaryDto?> GetTeamSummaryAsync(int id, int daysBack = 30);

	/// <summary>
	/// Creates a new team
	/// </summary>
	Task<int> CreateTeamAsync(CreateTeamDto team);

	/// <summary>
	/// Gets burnout alerts for all teams
	/// </summary>
	Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync();
}