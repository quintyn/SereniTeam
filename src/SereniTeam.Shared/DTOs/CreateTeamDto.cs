using System.ComponentModel.DataAnnotations;

namespace SereniTeam.Shared.DTOs;

/// <summary>
/// DTO for creating a new team
/// </summary>
public class CreateTeamDto
{
    [Required]
    [StringLength(100, ErrorMessage = "Team name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }
}