using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SereniTeam.Server.Data;
using SereniTeam.Server.Hubs;
using SereniTeam.Shared.DTOs;
using SereniTeam.Shared.Models;

namespace SereniTeam.Server.Services;

/// <summary>
/// Server-side implementation of ITeamApiService for Blazor Server mode
/// This replaces HTTP calls with direct service calls - UPDATED for DbContextFactory
/// </summary>
public class ServerSideTeamApiService : ITeamApiService
{
    private readonly IDbContextFactory<SereniTeamContext> _contextFactory;
    private readonly ILogger<ServerSideTeamApiService> _logger;
    private readonly IHubContext<TeamUpdatesHub> _hubContext;

    public ServerSideTeamApiService(
        IDbContextFactory<SereniTeamContext> contextFactory,
        ILogger<ServerSideTeamApiService> logger,
        IHubContext<TeamUpdatesHub> hubContext)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<List<TeamDto>> GetAllTeamsAsync()
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Getting all teams");

            using var context = _contextFactory.CreateDbContext();

            var teams = await context.Teams
                .Where(t => t.IsActive)
                .Select(t => new TeamDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    IsActive = t.IsActive
                })
                .ToListAsync();

            _logger.LogDebug("ServerSideTeamApiService: Retrieved {Count} teams", teams.Count);
            return teams;
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

            using var context = _contextFactory.CreateDbContext();

            var team = await context.Teams
                .Where(t => t.Id == id && t.IsActive)
                .Select(t => new TeamDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    IsActive = t.IsActive
                })
                .FirstOrDefaultAsync();

            _logger.LogDebug("ServerSideTeamApiService: Retrieved team {TeamId}: {Found}", id, team != null);
            return team;
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

            using var context = _contextFactory.CreateDbContext();

            var team = await context.Teams
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            if (team == null) return null;

            var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
            var checkIns = await context.CheckIns
                .Where(c => c.TeamId == id && c.SubmittedAt >= cutoffDate)
                .ToListAsync();

            // Calculate daily trends
            var dailyTrends = checkIns
                .GroupBy(c => c.SubmittedAt.Date)
                .Select(g => new DailyTrendDto
                {
                    Date = g.Key,
                    AverageMood = g.Average(c => c.MoodRating),
                    AverageStress = g.Average(c => c.StressLevel),
                    CheckInCount = g.Count()
                })
                .OrderByDescending(t => t.Date)
                .Take(30)
                .ToList();

            var summary = new TeamSummaryDto
            {
                TeamId = team.Id,
                TeamName = team.Name,
                Description = team.Description,
                AverageMood = checkIns.Any() ? checkIns.Average(c => c.MoodRating) : 0,
                AverageStress = checkIns.Any() ? checkIns.Average(c => c.StressLevel) : 0,
                TotalCheckIns = checkIns.Count,
                LastCheckInDate = checkIns.Any() ? checkIns.Max(c => c.SubmittedAt) : null,
                IsBurnoutRisk = CalculateBurnoutRisk(checkIns),
                RecentTrends = dailyTrends
            };

            _logger.LogDebug("ServerSideTeamApiService: Retrieved team summary for {TeamId}: {Found}", id, summary != null);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error getting team summary for {TeamId}", id);
            throw;
        }
    }

    public async Task<int> CreateTeamAsync(CreateTeamDto teamDto)
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Creating team {TeamName}", teamDto.Name);

            using var context = _contextFactory.CreateDbContext();

            var team = new Team
            {
                Name = teamDto.Name,
                Description = teamDto.Description,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            context.Teams.Add(team);
            await context.SaveChangesAsync();

            _logger.LogDebug("ServerSideTeamApiService: Created team {TeamName} with ID {TeamId}", teamDto.Name, team.Id);
            return team.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error creating team {TeamName}", teamDto.Name);
            throw;
        }
    }

    public async Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync()
    {
        try
        {
            _logger.LogDebug("ServerSideTeamApiService: Getting burnout alerts");

            using var context = _contextFactory.CreateDbContext();

            var alerts = new List<BurnoutAlertDto>();
            var cutoffDate = DateTime.UtcNow.AddDays(-7); // Check last 7 days

            var teams = await context.Teams
                .Where(t => t.IsActive)
                .ToListAsync();

            foreach (var team in teams)
            {
                var recentCheckIns = await context.CheckIns
                    .Where(c => c.TeamId == team.Id && c.SubmittedAt >= cutoffDate)
                    .ToListAsync();

                if (recentCheckIns.Any())
                {
                    var avgMood = recentCheckIns.Average(c => c.MoodRating);
                    var avgStress = recentCheckIns.Average(c => c.StressLevel);

                    // Simple burnout detection logic
                    if (avgMood <= 3.0 || avgStress >= 8.0)
                    {
                        var severity = (avgMood <= 2.0 || avgStress >= 9.0) ? "High" : "Medium";

                        alerts.Add(new BurnoutAlertDto
                        {
                            TeamId = team.Id,
                            TeamName = team.Name,
                            AlertLevel = severity,
                            Message = $"Team showing signs of burnout - Avg Mood: {avgMood:F1}, Avg Stress: {avgStress:F1}",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            _logger.LogDebug("ServerSideTeamApiService: Retrieved {Count} burnout alerts", alerts.Count);
            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerSideTeamApiService: Error getting burnout alerts");
            throw;
        }
    }

    private bool CalculateBurnoutRisk(List<CheckIn> checkIns)
    {
        if (!checkIns.Any()) return false;

        // Simple burnout risk calculation
        var avgMood = checkIns.Average(c => c.MoodRating);
        var avgStress = checkIns.Average(c => c.StressLevel);

        // Risk if mood is low (≤3) or stress is high (≥8)
        return avgMood <= 3.0 || avgStress >= 8.0;
    }
}