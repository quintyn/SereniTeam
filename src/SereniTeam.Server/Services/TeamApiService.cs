using SereniTeam.Shared.DTOs;
using SereniTeam.Client.Services;

namespace SereniTeam.Server.Services;

/// <summary>
/// Service for team-related operations (Blazor Server version)
/// Calls the actual services directly instead of making HTTP requests
/// </summary>
public class TeamApiService : SereniTeam.Client.Services.ITeamApiService
{
    private readonly ITeamService _teamService;
    private readonly ILogger<TeamApiService> _logger;

    public TeamApiService(ITeamService teamService, ILogger<TeamApiService> logger)
    {
        _teamService = teamService;
        _logger = logger;
    }

    public async Task<List<TeamDto>> GetAllTeamsAsync()
    {
        try
        {
            _logger.LogInformation("Getting all teams via direct service call");
            return await _teamService.GetAllTeamsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all teams");
            return new List<TeamDto>();
        }
    }

    public async Task<TeamDto?> GetTeamByIdAsync(int id)
    {
        try
        {
            _logger.LogInformation("Getting team {TeamId} via direct service call", id);
            return await _teamService.GetTeamByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team {TeamId}", id);
            return null;
        }
    }

    public async Task<TeamSummaryDto?> GetTeamSummaryAsync(int id, int daysBack = 30)
    {
        try
        {
            _logger.LogInformation("Getting team summary for {TeamId} ({DaysBack} days) via direct service call", id, daysBack);
            return await _teamService.GetTeamSummaryAsync(id, daysBack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team summary for {TeamId}", id);
            return null;
        }
    }

    public async Task<int> CreateTeamAsync(CreateTeamDto team)
    {
        try
        {
            _logger.LogInformation("Creating team {TeamName} via direct service call", team.Name);
            return await _teamService.CreateTeamAsync(team);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team {TeamName}", team.Name);
            return 0;
        }
    }

    public async Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync()
    {
        try
        {
            _logger.LogInformation("Getting burnout alerts via direct service call");
            return await _teamService.GetBurnoutAlertsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting burnout alerts");
            return new List<BurnoutAlertDto>();
        }
    }
}