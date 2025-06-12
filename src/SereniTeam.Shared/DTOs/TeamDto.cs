namespace SereniTeam.Shared.DTOs;

/// <summary>
/// DTO for basic team information
/// </summary>
public class TeamDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}