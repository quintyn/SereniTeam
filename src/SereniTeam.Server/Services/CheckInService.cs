using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SereniTeam.Server.Data;
using SereniTeam.Server.Hubs;
using SereniTeam.Shared.DTOs;
using SereniTeam.Shared.Models;

namespace SereniTeam.Server.Services;

public class CheckInService : ICheckInService
{
    private readonly IDbContextFactory<SereniTeamContext> _contextFactory;
    private readonly ILogger<CheckInService> _logger;
    private readonly IHubContext<TeamUpdatesHub> _hubContext;

    public CheckInService(
        IDbContextFactory<SereniTeamContext> contextFactory,
        ILogger<CheckInService> logger,
        IHubContext<TeamUpdatesHub> hubContext)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkInDto)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();

            // Validate team exists
            var teamExists = await context.Teams
                .AnyAsync(t => t.Id == checkInDto.TeamId && t.IsActive);

            if (!teamExists)
            {
                _logger.LogWarning("Attempted to submit check-in for non-existent team {TeamId}", checkInDto.TeamId);
                return false;
            }

            var checkIn = new CheckIn
            {
                TeamId = checkInDto.TeamId,
                MoodRating = checkInDto.MoodRating,
                StressLevel = checkInDto.StressLevel,
                Notes = checkInDto.Notes,
                SubmittedAt = DateTime.UtcNow
            };

            context.CheckIns.Add(checkIn);
            await context.SaveChangesAsync();

            // Trigger SignalR notification
            await _hubContext.Clients.Group($"Team_{checkInDto.TeamId}")
                .SendAsync("NewCheckInReceived", new CheckInDto
                {
                    Id = checkIn.Id,
                    TeamId = checkIn.TeamId,
                    MoodRating = checkIn.MoodRating,
                    StressLevel = checkIn.StressLevel,
                    Notes = checkIn.Notes,
                    SubmittedAt = checkIn.SubmittedAt
                });

            _logger.LogInformation("Check-in submitted successfully for team {TeamId}", checkInDto.TeamId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting check-in for team {TeamId}", checkInDto.TeamId);
            return false;
        }
    }

    public async Task<List<CheckInDto>> GetTeamCheckInsAsync(int teamId, int daysBack = 30)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();

            var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
            var checkIns = await context.CheckIns
                .Where(c => c.TeamId == teamId && c.SubmittedAt >= cutoffDate)
                .OrderByDescending(c => c.SubmittedAt)
                .Select(c => new CheckInDto
                {
                    Id = c.Id,
                    TeamId = c.TeamId,
                    MoodRating = c.MoodRating,
                    StressLevel = c.StressLevel,
                    Notes = c.Notes,
                    SubmittedAt = c.SubmittedAt
                })
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} check-ins for team {TeamId}", checkIns.Count, teamId);
            return checkIns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins for team {TeamId}", teamId);
            throw;
        }
    }
}