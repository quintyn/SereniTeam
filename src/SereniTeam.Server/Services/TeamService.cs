using Microsoft.EntityFrameworkCore;
using SereniTeam.Server.Data;
using SereniTeam.Shared.DTOs;
using SereniTeam.Shared.Models;

namespace SereniTeam.Server.Services;

public class TeamService : ITeamService
{
    private readonly IDbContextFactory<SereniTeamContext> _contextFactory;
    private readonly ILogger<TeamService> _logger;

    public TeamService(IDbContextFactory<SereniTeamContext> contextFactory, ILogger<TeamService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<List<TeamDto>> GetAllTeamsAsync()
    {
        try
        {
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

            _logger.LogDebug("Retrieved {Count} teams", teams.Count);
            return teams;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all teams");
            throw;
        }
    }

    public async Task<TeamDto?> GetTeamByIdAsync(int id)
    {
        try
        {
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

            _logger.LogDebug("Retrieved team {TeamId}: {Found}", id, team != null);
            return team;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team {TeamId}", id);
            throw;
        }
    }

    public async Task<TeamSummaryDto?> GetTeamSummaryAsync(int id, int daysBack = 30)
    {
        try
        {
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
                IsBurnoutRisk = await CalculateBurnoutRisk(checkIns),
                RecentTrends = dailyTrends
            };

            _logger.LogDebug("Retrieved team summary for {TeamId}", id);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team summary for {TeamId}", id);
            throw;
        }
    }

    public async Task<int> CreateTeamAsync(CreateTeamDto teamDto)
    {
        try
        {
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

            _logger.LogInformation("Created team {TeamName} with ID {TeamId}", teamDto.Name, team.Id);
            return team.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team {TeamName}", teamDto.Name);
            throw;
        }
    }

    public async Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync()
    {
        try
        {
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

            _logger.LogDebug("Found {Count} burnout alerts", alerts.Count);
            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving burnout alerts");
            throw;
        }
    }

    private async Task<bool> CalculateBurnoutRisk(List<CheckIn> checkIns)
    {
        if (!checkIns.Any()) return false;

        // Simple burnout risk calculation
        var avgMood = checkIns.Average(c => c.MoodRating);
        var avgStress = checkIns.Average(c => c.StressLevel);

        // Risk if mood is low (≤3) or stress is high (≥8)
        return avgMood <= 3.0 || avgStress >= 8.0;
    }
}