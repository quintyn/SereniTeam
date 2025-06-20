using Microsoft.AspNetCore.Mvc;
using SereniTeam.Server.Services;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Controllers;

/// <summary>
/// API controller for check-in operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CheckInsController : ControllerBase
{
    private readonly ICheckInService _checkInService;
    private readonly ILogger<CheckInsController> _logger;

    public CheckInsController(ICheckInService checkInService, ILogger<CheckInsController> logger)
    {
        _checkInService = checkInService;
        _logger = logger;
    }

    /// <summary>
    /// Submits a new anonymous wellness check-in
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> SubmitCheckIn([FromBody] CheckInSubmissionDto checkInDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _checkInService.SubmitCheckInAsync(checkInDto);
            if (!success)
                return BadRequest("Unable to submit check-in. Please verify team ID and try again.");

            return Ok(new { message = "Check-in submitted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting check-in for team {TeamId}", checkInDto.TeamId);
            return StatusCode(500, "An error occurred while submitting the check-in");
        }
    }

    /// <summary>
    /// Gets check-ins for a specific team (for admin/debugging purposes)
    /// </summary>
    [HttpGet("team/{teamId}")]
    public async Task<ActionResult> GetTeamCheckIns(int teamId, [FromQuery] int daysBack = 30)
    {
        try
        {
            var checkIns = await _checkInService.GetTeamCheckInsAsync(teamId, daysBack);

            // Return anonymized data only (no personal identifiers)
            var anonymizedData = checkIns.Select(c => new
            {
                MoodRating = c.MoodRating,
                StressLevel = c.StressLevel,
                SubmittedAt = c.SubmittedAt,
                HasNotes = !string.IsNullOrEmpty(c.Notes)
            }).ToList();

            return Ok(anonymizedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins for team {TeamId}", teamId);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }
}