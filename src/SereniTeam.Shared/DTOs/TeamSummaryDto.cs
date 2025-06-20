namespace SereniTeam.Shared.DTOs;

/// <summary>
/// DTO for team summary with wellness metrics
/// </summary>
public class TeamSummaryDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double AverageMood { get; set; }
    public double AverageStress { get; set; }
    public int TotalCheckIns { get; set; }
    public DateTime? LastCheckInDate { get; set; }
    public bool IsBurnoutRisk { get; set; }
    public List<DailyTrendDto> RecentTrends { get; set; } = new();
}