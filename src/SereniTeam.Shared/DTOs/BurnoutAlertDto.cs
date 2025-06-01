namespace SereniTeam.Shared.DTOs;

/// <summary>
/// DTO for burnout alert information
/// </summary>
public class BurnoutAlertDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string AlertMessage { get; set; } = string.Empty;
    public DateTime AlertDate { get; set; }
    public string Severity { get; set; } = string.Empty; // Low, Medium, High
}