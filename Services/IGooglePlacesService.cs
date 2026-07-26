using LeadScoutCRM.Models.ViewModels;

namespace LeadScoutCRM.Services;

public interface IGooglePlacesService
{
    // Recebe nicho + localização, devolve lista de resultados
    Task<List<GooglePlaceResult>> SearchPlacesAsync(string niche, string location);
}