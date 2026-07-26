using System.ComponentModel.DataAnnotations;

namespace LeadScoutCRM.Models.Entities;

public class Lead
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string BusinessName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(300)]
    public string? Website { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? Niche { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? GooglePlaceId { get; set; }

    public LeadStatus Status { get; set; } = LeadStatus.Novo;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastContactedAt { get; set; }

    public ICollection<Note> Notes { get; set; } = new List<Note>();
}