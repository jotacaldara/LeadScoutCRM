using LeadScoutCRM.Models.ViewModels;

namespace LeadScoutCRM.Services;

public interface IGooglePlacesService
{
    Task<PlacesSearchResult> SearchPlacesAsync(string niche, string location, string? pageToken = null);
}