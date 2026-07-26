using System.ComponentModel.DataAnnotations;

namespace LeadScoutCRM.Models.Entities;

public class Note
{
    public int Id { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int LeadId { get; set; }         
    public Lead Lead { get; set; } = null!; 
}