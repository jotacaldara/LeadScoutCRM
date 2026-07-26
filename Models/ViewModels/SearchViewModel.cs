using System.ComponentModel.DataAnnotations;
using LeadScoutCRM.Models.Entities;

namespace LeadScoutCRM.Models.ViewModels;

public class SearchViewModel
{

    [Required(ErrorMessage = "O nicho é obrigatório")]
    [Display(Name = "Nicho de Negócio")]
    public string Niche { get; set; } = string.Empty;  

    [Required(ErrorMessage = "A localização é obrigatória")]
    [Display(Name = "Cidade / Localização")]
    public string Location { get; set; } = string.Empty; 

    public List<GooglePlaceResult> Results { get; set; } = new();
}

// Representa UM resultado do Google Places
// Não é uma Entity da BD — é só um contentor de dados temporário
public class GooglePlaceResult
{
    public string PlaceId { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public double? Rating { get; set; }
}