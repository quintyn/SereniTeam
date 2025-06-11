using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Server-side implementation of ITeamApiService for Blazor Server mode
/// This replaces HTTP calls with direct service calls
/// </summary>
public class ServerSideTeamApiService : SereniTeam.Client.Services.ITeamApiService
{
	private readonly ITeamService _teamService;
	private readonly ILogger<ServerSideTeamApiService> _logger;

	public ServerSideTeamApiService(ITeamService teamService, ILogger<ServerSideTeamApiService> logger)
	{
		_teamService = teamService;
		_logger = logger;
	}

	public async Task<List<TeamDto>> GetAllTeamsAsync()
	{
		try
		{
			_logger.LogDebug("ServerSideTeamApiService: Getting all teams");
			var result = await _teamService.GetAllTeamsAsync();
			_logger.LogDebug("ServerSideTeamApiService: Retrieved {Count} teams", result.Count);
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "ServerSideTeamApiService: Error getting all teams");
			throw;
		}
	}

	public async Task<TeamDto?> GetTeamByIdAsync(int id)
	{
		try
		{
			_logger.LogDebug("ServerSideTeamApiService: Getting team {TeamId}", id);
			var result = await _teamService.GetTeamByIdAsync(id);
			_logger.LogDebug("ServerSideTeamApiService: Retrieved team {TeamId}: {Found}", id, result != null);
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "ServerSideTeamApiService: Error getting team {TeamId}", id);
			throw;
		}
	}

	public async Task<TeamSummaryDto?> GetTeamSummaryAsync(int id, int daysBack = 30)
	{
		try
		{
			_logger.LogDebug("ServerSideTeamApiService: Getting team summary for {TeamId}, {DaysBack} days", id, daysBack);
			var result = await _teamService.GetTeamSummaryAsync(id, daysBack);
			_logger.LogDebug("ServerSideTeamApiService: Retrieved team summary for {TeamId}: {Found}", id, result != null);
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "ServerSideTeamApiService: Error getting team summary for {TeamId}", id);
			throw;
		}
	}

	public async Task<int> CreateTeamAsync(CreateTeamDto team)
	{
		try
		{
			_logger.LogDebug("ServerSideTeamApiService: Creating team {TeamName}", team.Name);
			var result = await _teamService.CreateTeamAsync(team);
			_logger.LogDebug("ServerSideTeamApiService: Created team {TeamName} with ID {TeamId}", team.Name, result);
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "ServerSideTeamApiService: Error creating team {TeamName}", team.Name);
			throw;
		}
	}

	public async Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync()
	{
		try
		{
			_logger.LogDebug("ServerSideTeamApiService: Getting burnout alerts");
			var result = await _teamService.GetBurnoutAlertsAsync();
			_logger.LogDebug("ServerSideTeamApiService: Retrieved {Count} burnout alerts", result.Count);
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "ServerSideTeamApiService: Error getting burnout alerts");
			throw;
		}
	}
}