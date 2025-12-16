using Microsoft.EntityFrameworkCore;
using SereniTeam.Server.Data;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Implementation of ITeamService for team management operations
/// </summary>
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
        using var context = _contextFactory.CreateDbContext();
        return await context.Teams
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
    }

    public async Task<TeamDto?> GetTeamByIdAsync(int id)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.Teams
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
    }

    public async Task<TeamSummaryDto?> GetTeamSummaryAsync(int id, int daysBack = 30)
    {
        using var context = _contextFactory.CreateDbContext();

        var team = await context.Teams.FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
        if (team == null) return null;

        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
        var checkIns = await context.CheckIns
            .Where(c => c.TeamId == id && c.SubmittedAt >= cutoffDate)
            .ToListAsync();

        var dailyTrends = checkIns
            .GroupBy(c => c.SubmittedAt.Date)
            .Select(g => new DailyTrendDto
            {
                Date = g.Key,
                AverageMood = g.Average(c => c.MoodRating),
                AverageStress = g.Average(c => c.StressLevel),
                CheckInCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new TeamSummaryDto
        {
            TeamId = team.Id,
            TeamName = team.Name,
            AverageMood = checkIns.Any() ? checkIns.Average(c => c.MoodRating) : 0,
            AverageStress = checkIns.Any() ? checkIns.Average(c => c.StressLevel) : 0,
            TotalCheckIns = checkIns.Count,
            LastCheckInDate = checkIns.Any() ? checkIns.Max(c => c.SubmittedAt) : null,
            IsBurnoutRisk = CalculateBurnoutRisk(checkIns),
            RecentTrends = dailyTrends
        };
    }

    public async Task<int> CreateTeamAsync(CreateTeamDto teamDto)
    {
        using var context = _contextFactory.CreateDbContext();

        var team = new SereniTeam.Shared.Models.Team
        {
            Name = teamDto.Name,
            Description = teamDto.Description,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.Teams.Add(team);
        await context.SaveChangesAsync();
        return team.Id;
    }

    public async Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync()
    {
        using var context = _contextFactory.CreateDbContext();

        var alerts = new List<BurnoutAlertDto>();
        var cutoffDate = DateTime.UtcNow.AddDays(-7);

        var teams = await context.Teams.Where(t => t.IsActive).ToListAsync();

        foreach (var team in teams)
        {
            var recentCheckIns = await context.CheckIns
                .Where(c => c.TeamId == team.Id && c.SubmittedAt >= cutoffDate)
                .ToListAsync();

            if (recentCheckIns.Any() && CalculateBurnoutRisk(recentCheckIns))
            {
                var avgMood = recentCheckIns.Average(c => c.MoodRating);
                var avgStress = recentCheckIns.Average(c => c.StressLevel);

                alerts.Add(new BurnoutAlertDto
                {
                    TeamId = team.Id,
                    TeamName = team.Name,
                    AlertLevel = avgStress >= 8 ? "High" : (avgMood <= 3 ? "Medium" : "Low"),
                    Message = $"Team showing signs of burnout: Avg Mood {avgMood:F1}, Avg Stress {avgStress:F1}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return alerts;
    }

    private bool CalculateBurnoutRisk(List<SereniTeam.Shared.Models.CheckIn> checkIns)
    {
        if (!checkIns.Any()) return false;
        var avgMood = checkIns.Average(c => c.MoodRating);
        var avgStress = checkIns.Average(c => c.StressLevel);
        return avgMood <= 3.0 || avgStress >= 8.0;
    }
}