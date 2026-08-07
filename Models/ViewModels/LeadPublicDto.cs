using LeadScoutCRM.Models.Entities;

namespace LeadScoutCRM.Models.ViewModels;

// Formato de resposta da API pública — nunca expõe a entidade Lead diretamente,
// para não vazar navegações internas (User, Notes) a integrações externas.
public class LeadPublicDto
{
    public int Id { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? Niche { get; set; }
    public string? City { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastContactedAt { get; set; }

    public static LeadPublicDto FromEntity(Lead lead) => new()
    {
        Id = lead.Id,
        BusinessName = lead.BusinessName,
        PhoneNumber = lead.PhoneNumber,
        Website = lead.Website,
        Address = lead.Address,
        Niche = lead.Niche,
        City = lead.City,
        Status = lead.Status.ToString(),
        CreatedAt = lead.CreatedAt,
        LastContactedAt = lead.LastContactedAt
    };
}