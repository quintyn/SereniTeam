using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SereniTeam.Server.Data;
using SereniTeam.Server.Hubs;
using SereniTeam.Shared.Models;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Service for handling check-in operations
/// </summary>
public class CheckInService : ICheckInService
{
    private readonly SereniTeamContext _context;
    private readonly IHubContext<TeamUpdatesHub> _hubContext;
    private readonly ILogger<CheckInService> _logger;

    public CheckInService(
        SereniTeamContext context,
        IHubContext<TeamUpdatesHub> hubContext,
        ILogger<CheckInService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Submits a new anonymous check-in and broadcasts real-time update
    /// </summary>
    public async Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkInDto)
    {
        try
        {
            // Validate team exists
            var teamExists = await _context.Teams
                .AnyAsync(t => t.Id == checkInDto.TeamId && t.IsActive);

            if (!teamExists)
            {
                _logger.LogWarning("Attempted to submit check-in for non-existent or inactive team {TeamId}", checkInDto.TeamId);
                return false;
            }

            // Create check-in entity
            var checkIn = new CheckIn
            {
                TeamId = checkInDto.TeamId,
                MoodRating = checkInDto.MoodRating,
                StressLevel = checkInDto.StressLevel,
                Notes = checkInDto.Notes?.Trim(),
                SubmittedAt = DateTime.UtcNow
            };

            _context.CheckIns.Add(checkIn);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Check-in submitted for team {TeamId} - Mood: {Mood}, Stress: {Stress}",
                checkInDto.TeamId, checkInDto.MoodRating, checkInDto.StressLevel);

            // Broadcast real-time update to team dashboard
            await _hubContext.Clients.Group($"Team_{checkInDto.TeamId}")
                .SendAsync("NewCheckInReceived", new
                {
                    TeamId = checkInDto.TeamId,
                    MoodRating = checkInDto.MoodRating,
                    StressLevel = checkInDto.StressLevel,
                    SubmittedAt = checkIn.SubmittedAt
                });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting check-in for team {TeamId}", checkInDto.TeamId);
            return false;
        }
    }

    /// <summary>
    /// Retrieves check-ins for a specific team
    /// </summary>
    public async Task<List<CheckIn>> GetTeamCheckInsAsync(int teamId, DateTime? fromDate = null)
    {
        var query = _context.CheckIns.Where(c => c.TeamId == teamId);

        if (fromDate.HasValue)
        {
            query = query.Where(c => c.SubmittedAt >= fromDate.Value);
        }

        return await query
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync();
    }
}