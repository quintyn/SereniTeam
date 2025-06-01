using System.ComponentModel.DataAnnotations;

namespace SereniTeam.Shared.Models;

/// <summary>
/// Represents a team within the organization
/// </summary>
public class Team
{
	public int Id { get; set; }

	[Required]
	[StringLength(100)]
	public string Name { get; set; } = string.Empty;

	[StringLength(500)]
	public string? Description { get; set; }

	public DateTime CreatedAt { get; set; }

	public bool IsActive { get; set; } = true;

	// Navigation properties
	public virtual ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
}