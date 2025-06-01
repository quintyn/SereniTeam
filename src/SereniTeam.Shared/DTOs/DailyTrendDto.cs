namespace SereniTeam.Shared.DTOs;

/// <summary>
/// DTO for daily trend data points
/// </summary>
public class DailyTrendDto
{
    public DateTime Date { get; set; }
    public double AverageMood { get; set; }
    public double AverageStress { get; set; }
    public int CheckInCount { get; set; }
}