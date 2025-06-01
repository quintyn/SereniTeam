using Microsoft.AspNetCore.Mvc;
using SereniTeam.Server.Services;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Controllers;

/// <summary>
/// API controller for team management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;
    private readonly ILogger<TeamsController> _logger;

    public TeamsController(ITeamService teamService, ILogger<TeamsController> logger)
    {
        _teamService = teamService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active teams
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TeamDto>>> GetAllTeams()
    {
        try
        {
            var teams = await _teamService.GetAllTeamsAsync();
            return Ok(teams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving teams");
            return StatusCode(500, "An error occurred while retrieving teams");
        }
    }

    /// <summary>
    /// Gets a specific team by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TeamDto>> GetTeam(int id)
    {
        try
        {
            var team = await _teamService.GetTeamByIdAsync(id);

            if (team == null)
                return NotFound($"Team with ID {id} not found");

            return Ok(team);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team {TeamId}", id);
            return StatusCode(500, "An error occurred while retrieving the team");
        }
    }

    /// <summary>
    /// Gets team summary with analytics and trends
    /// </summary>
    [HttpGet("{id}/summary")]
    public async Task<ActionResult<TeamSummaryDto>> GetTeamSummary(int id, [FromQuery] int daysBack = 30)
    {
        try
        {
            if (daysBack < 1 || daysBack > 365)
                return BadRequest("Days back must be between 1 and 365");

            var summary = await _teamService.GetTeamSummaryAsync(id, daysBack);

            if (summary == null)
                return NotFound($"Team with ID {id} not found");

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team summary for {TeamId}", id);
            return StatusCode(500, "An error occurred while retrieving team summary");
        }
    }

    /// <summary>
    /// Creates a new team
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<int>> CreateTeam([FromBody] CreateTeamDto teamDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var teamId = await _teamService.CreateTeamAsync(teamDto);

            return CreatedAtAction(nameof(GetTeam), new { id = teamId }, teamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team {TeamName}", teamDto.Name);
            return StatusCode(500, "An error occurred while creating the team");
        }
    }

    /// <summary>
    /// Gets burnout alerts for all teams
    /// </summary>
    [HttpGet("alerts")]
    public async Task<ActionResult<List<BurnoutAlertDto>>> GetBurnoutAlerts()
    {
        try
        {
            var alerts = await _teamService.GetBurnoutAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving burnout alerts");
            return StatusCode(500, "An error occurred while retrieving burnout alerts");
        }
    }
}