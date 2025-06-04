using Microsoft.EntityFrameworkCore;
using SereniTeam.Server.Data;
using SereniTeam.Shared.Models;
using SereniTeam.Shared.DTOs;

namespace SereniTeam.Server.Services;

/// <summary>
/// Service for handling team operations and analytics
/// </summary>
public class TeamService : ITeamService
{
    private readonly SereniTeamContext _context;
    private readonly ILogger<TeamService> _logger;
    private readonly IConfiguration _configuration;

    public TeamService(
        SereniTeamContext context,
        ILogger<TeamService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets all active teams
    /// </summary>
    public async Task<List<TeamDto>> GetAllTeamsAsync()
    {
        return await _context.Teams
            .Where(t => t.IsActive)
            .Select(t => new TeamDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                CreatedAt = t.CreatedAt,
                IsActive = t.IsActive
            })
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a specific team by ID
    /// </summary>
    public async Task<TeamDto?> GetTeamByIdAsync(int id)
    {
        return await _context.Teams
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

    /// <summary>
    /// Gets comprehensive team summary with analytics and trends
    /// </summary>
    public async Task<TeamSummaryDto?> GetTeamSummaryAsync(int teamId, int daysBack = 30)
    {
        var team = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && t.IsActive);

        if (team == null)
            return null;

        var fromDate = DateTime.UtcNow.AddDays(-daysBack);

        var checkIns = await _context.CheckIns
            .Where(c => c.TeamId == teamId && c.SubmittedAt >= fromDate)
            .OrderBy(c => c.SubmittedAt)
            .ToListAsync();

        if (!checkIns.Any())
        {
            return new TeamSummaryDto
            {
                TeamId = teamId,
                TeamName = team.Name,
                AverageMood = 0,
                AverageStress = 0,
                TotalCheckIns = 0,
                LastCheckInDate = DateTime.MinValue,
                IsBurnoutRisk = false,
                RecentTrends = new List<DailyTrendDto>()
            };
        }

        // Calculate overall averages
        var avgMood = checkIns.Average(c => c.MoodRating);
        var avgStress = checkIns.Average(c => c.StressLevel);
        var lastCheckIn = checkIns.Max(c => c.SubmittedAt);

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
            .OrderBy(t => t.Date)
            .ToList();

        // Determine burnout risk (removed await since method is now synchronous)
        var isBurnoutRisk = CalculateBurnoutRisk(teamId, checkIns);

        return new TeamSummaryDto
        {
            TeamId = teamId,
            TeamName = team.Name,
            AverageMood = Math.Round(avgMood, 2),
            AverageStress = Math.Round(avgStress, 2),
            TotalCheckIns = checkIns.Count,
            LastCheckInDate = lastCheckIn,
            IsBurnoutRisk = isBurnoutRisk,
            RecentTrends = dailyTrends
        };
    }

    /// <summary>
    /// Creates a new team
    /// </summary>
    public async Task<int> CreateTeamAsync(CreateTeamDto teamDto)
    {
        var team = new Team
        {
            Name = teamDto.Name.Trim(),
            Description = teamDto.Description?.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new team: {TeamName} (ID: {TeamId})", team.Name, team.Id);

        return team.Id;
    }

    /// <summary>
    /// Gets burnout alerts for all teams
    /// </summary>
    public async Task<List<BurnoutAlertDto>> GetBurnoutAlertsAsync()
    {
        var alerts = new List<BurnoutAlertDto>();
        var teams = await _context.Teams.Where(t => t.IsActive).ToListAsync();

        foreach (var team in teams)
        {
            var recentCheckIns = await _context.CheckIns
                .Where(c => c.TeamId == team.Id && c.SubmittedAt >= DateTime.UtcNow.AddDays(-14))
                .ToListAsync();

            // Removed await since method is now synchronous
            if (CalculateBurnoutRisk(team.Id, recentCheckIns))
            {
                var severity = DetermineBurnoutSeverity(recentCheckIns);

                alerts.Add(new BurnoutAlertDto
                {
                    TeamId = team.Id,
                    TeamName = team.Name,
                    AlertMessage = GenerateBurnoutMessage(recentCheckIns, severity),
                    AlertDate = DateTime.UtcNow,
                    Severity = severity
                });
            }
        }

        return alerts;
    }

    /// <summary>
    /// Calculates if a team is at burnout risk based on configured thresholds
    /// FIXED: Removed async/await since this method doesn't perform any async operations
    /// </summary>
    private bool CalculateBurnoutRisk(int teamId, List<CheckIn> checkIns)
    {
        if (!checkIns.Any())
            return false;

        // Get configuration thresholds
        var lowMoodThreshold = _configuration.GetValue<double>("SereniTeam:BurnoutThresholds:LowMoodThreshold", 3.0);
        var highStressThreshold = _configuration.GetValue<double>("SereniTeam:BurnoutThresholds:HighStressThreshold", 7.0);
        var consecutiveDays = _configuration.GetValue<int>("SereniTeam:BurnoutThresholds:ConsecutiveDaysForAlert", 3);
        var minCheckIns = _configuration.GetValue<int>("SereniTeam:BurnoutThresholds:MinimumCheckInsForAnalysis", 5);

        if (checkIns.Count < minCheckIns)
            return false;

        // Group by date and check for consecutive days of concerning metrics
        var dailyAverages = checkIns
            .GroupBy(c => c.SubmittedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                AvgMood = g.Average(c => c.MoodRating),
                AvgStress = g.Average(c => c.StressLevel)
            })
            .OrderByDescending(x => x.Date)
            .Take(consecutiveDays * 2) // Look at more days to find patterns
            .ToList();

        // Check for consecutive days of low mood or high stress
        int consecutiveConcerningDays = 0;

        foreach (var day in dailyAverages.OrderBy(x => x.Date))
        {
            if (day.AvgMood <= lowMoodThreshold || day.AvgStress >= highStressThreshold)
            {
                consecutiveConcerningDays++;
                if (consecutiveConcerningDays >= consecutiveDays)
                    return true;
            }
            else
            {
                consecutiveConcerningDays = 0;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines the severity of burnout risk
    /// </summary>
    private string DetermineBurnoutSeverity(List<CheckIn> checkIns)
    {
        if (!checkIns.Any())
            return "Low";

        var avgMood = checkIns.Average(c => c.MoodRating);
        var avgStress = checkIns.Average(c => c.StressLevel);

        if (avgMood <= 2.0 || avgStress >= 8.5)
            return "High";
        else if (avgMood <= 3.0 || avgStress >= 7.0)
            return "Medium";
        else
            return "Low";
    }

    /// <summary>
    /// Generates a descriptive burnout alert message
    /// </summary>
    private string GenerateBurnoutMessage(List<CheckIn> checkIns, string severity)
    {
        var avgMood = checkIns.Average(c => c.MoodRating);
        var avgStress = checkIns.Average(c => c.StressLevel);

        return severity switch
        {
            "High" => $"Critical burnout risk detected. Team showing sustained low mood ({avgMood:F1}/10) and high stress ({avgStress:F1}/10).",
            "Medium" => $"Moderate burnout risk. Team mood ({avgMood:F1}/10) and stress levels ({avgStress:F1}/10) showing concerning trends.",
            _ => $"Team showing signs of potential burnout. Recent metrics: mood {avgMood:F1}/10, stress {avgStress:F1}/10."
        };
    }
}