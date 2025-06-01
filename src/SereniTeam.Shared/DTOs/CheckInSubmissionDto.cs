using System.ComponentModel.DataAnnotations;

namespace SereniTeam.Shared.DTOs;

/// <summary>
/// DTO for submitting a new check-in
/// </summary>
public class CheckInSubmissionDto
{
    [Required]
    public int TeamId { get; set; }

    [Required]
    [Range(1, 10, ErrorMessage = "Mood rating must be between 1 and 10")]
    public int MoodRating { get; set; }

    [Required]
    [Range(1, 10, ErrorMessage = "Stress level must be between 1 and 10")]
    public int StressLevel { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }
}