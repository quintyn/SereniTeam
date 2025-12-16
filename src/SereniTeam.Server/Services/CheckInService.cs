using Microsoft.EntityFrameworkCore;
using SereniTeam.Server.Data;
using SereniTeam.Shared.DTOs;
using SereniTeam.Shared.Models;

namespace SereniTeam.Server.Services;

/// <summary>
/// Implementation of ICheckInService for check-in operations
/// </summary>
public class CheckInService : ICheckInService
{
    private readonly IDbContextFactory<SereniTeamContext> _contextFactory;
    private readonly ILogger<CheckInService> _logger;

    public CheckInService(IDbContextFactory<SereniTeamContext> contextFactory, ILogger<CheckInService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkInDto)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();

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

            _logger.LogInformation("Check-in submitted for team {TeamId}", checkInDto.TeamId);
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

            return await context.CheckIns
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting check-ins for team {TeamId}", teamId);
            return new List<CheckInDto>();
        }
    }
}