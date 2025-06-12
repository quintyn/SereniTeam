namespace SereniTeam.Shared.DTOs;

/// <summary>
/// DTO for check-in data with timestamp
/// </summary>
public class CheckInDto
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int MoodRating { get; set; }
    public int StressLevel { get; set; }
    public string? Notes { get; set; }
    public DateTime SubmittedAt { get; set; }
}