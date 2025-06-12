using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SereniTeam.Server.Data;
using SereniTeam.Server.Hubs;
using SereniTeam.Shared.DTOs;
using SereniTeam.Shared.Models;

namespace SereniTeam.Server.Services;

/// <summary>
/// Server-side implementation of ICheckInApiService for Blazor Server mode
/// This replaces HTTP calls with direct service calls - UPDATED for DbContextFactory
/// </summary>
public class ServerSideCheckInApiService : SereniTeam.Client.Services.ICheckInApiService
{
    private readonly IDbContextFactory<SereniTeamContext> _contextFactory;
    private readonly ILogger<ServerSideCheckInApiService> _logger;
    private readonly IHubContext<TeamUpdatesHub> _hubContext;

    public ServerSideCheckInApiService(
        IDbContextFactory<SereniTeamContext> contextFactory,
        ILogger<ServerSideCheckInApiService> logger,
        IHubContext<TeamUpdatesHub> hubContext)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<bool> SubmitCheckInAsync(CheckInSubmissionDto checkIn)
    {
        try
        {
            _logger.LogDebug("ServerSideCheckInApiService: Submitting check-in for team {TeamId}", checkIn.TeamId);

            using var context = _contextFactory.CreateDbContext();

            // Validate team exists
            var teamExists = await context.Teams
                .AnyAsync(t => t.Id == checkIn.TeamId && t.IsActive);

            if (!teamExists)
            {
                _logger.LogWarning("Attempted to submit check-in for non-existent team {TeamId}", checkIn.TeamId);
                return false;
            }

            var checkInEntity = new CheckIn
            {
                TeamId = checkIn.TeamId,
                MoodRating = checkIn.MoodRating,
                StressLevel = checkIn.StressLevel,
                Notes = checkIn.Notes,
                SubmittedAt = DateTime.UtcNow
            };

            context.CheckIns.Add(checkInEntity);
            await context.SaveChangesAsync();

            // Trigger SignalR notification
            await _hubContext.Clients.Group($"Team_{checkIn.TeamId}")
                .SendAsync("NewCheckInReceived", new CheckInDto
                {
                    Id = checkInEntity.Id,
                    TeamId = checkInEntity.TeamId,
                    MoodRating = checkInEntity.MoodRating,
                    StressLevel = checkInEntity.StressLevel,
                    Notes = checkInEntity.Notes,
                    SubmittedAt = checkInEntity.SubmittedAt
                });

            _logger.LogDebug("ServerSideCheckInApiService: Check-in submission successful for team {TeamId}", checkIn.TeamId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideCheckInApiService: Error submitting check-in for team {TeamId}", checkIn.TeamId);
            return false;
        }
    }

    public async Task<List<CheckInDto>> GetTeamCheckInsAsync(int teamId, int daysBack = 30)
    {
        try
        {
            _logger.LogDebug("ServerSideCheckInApiService: Getting check-ins for team {TeamId}", teamId);

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

            _logger.LogDebug("ServerSideCheckInApiService: Retrieved {Count} check-ins for team {TeamId}", checkIns.Count, teamId);
            return checkIns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideCheckInApiService: Error getting check-ins for team {TeamId}", teamId);
            throw;
        }
    }
}