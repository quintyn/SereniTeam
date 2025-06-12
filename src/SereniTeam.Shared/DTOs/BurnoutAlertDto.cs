namespace SereniTeam.Shared.DTOs;

/// <summary>
/// DTO for burnout alert information
/// </summary>
public class BurnoutAlertDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string AlertLevel { get; set; } = string.Empty; // Low, Medium, High
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}