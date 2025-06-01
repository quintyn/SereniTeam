using System.ComponentModel.DataAnnotations;

namespace SereniTeam.Shared.Models;

/// <summary>
/// Represents an anonymous wellness check-in submission
/// </summary>
public class CheckIn
{
	public int Id { get; set; }

	public int TeamId { get; set; }

	[Range(1, 10)]
	public int MoodRating { get; set; }

	[Range(1, 10)]
	public int StressLevel { get; set; }

	[StringLength(500)]
	public string? Notes { get; set; }

	public DateTime SubmittedAt { get; set; }

	// Navigation properties
	public virtual Team Team { get; set; } = null!;
}