using System.ComponentModel.DataAnnotations;

namespace LeadScoutCRM.Models.ViewModels;

// Corpo do POST /api/leads
public class SaveLeadRequest
{
    [Required]
    public string BusinessName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? Niche { get; set; }
    public string? City { get; set; }
    public string? PlaceId { get; set; }
}

// Corpo do PATCH /api/leads/{id}/status
public class UpdateStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

// Corpo do POST /api/leads/{id}/notes
public class AddNoteRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}

public class GenerateMessageRequest
{
    [Required]
    public string MessageType { get; set; } = "whatsapp"; // whatsapp | email | linkedin
}

public class UpdateLeadRequest
{
    [Required]
    public string BusinessName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? Niche { get; set; }
    public string? City { get; set; }
}