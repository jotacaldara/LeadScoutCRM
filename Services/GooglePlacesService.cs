using System.Text.Json;
using LeadScoutCRM.Models.ViewModels;
using LeadScoutCRM.Services.Models;
using Microsoft.Extensions.Options;

namespace LeadScoutCRM.Services;

public class GooglePlacesService : IGooglePlacesService
{
    private readonly HttpClient _httpClient;
    private readonly GooglePlacesOptions _options;
    private readonly ILogger<GooglePlacesService> _logger;

    public GooglePlacesService(
        HttpClient httpClient,
        IOptions<GooglePlacesOptions> options,
        ILogger<GooglePlacesService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<GooglePlaceResult>> SearchPlacesAsync(
        string niche, string location)
    {
        var results = new List<GooglePlaceResult>();

        try
        {
            var searchResults = await TextSearchAsync(niche, location);

            //limita a 10 resultados para não gastar tanto da API
            var tasks = searchResults
                .Take(10)
                .Select(place => EnrichWithDetailsAsync(place));

            var enrichedResults = await Task.WhenAll(tasks);
            results.AddRange(enrichedResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar no Google Places: {Niche} em {Location}",
                niche, location);
       
            throw;
        }

        return results;
    }

    private async Task<List<PlaceResult>> TextSearchAsync(
        string niche, string location)
    {
        var query = Uri.EscapeDataString($"{niche} em {location}");
        var url = $"{_options.BaseUrl}/textsearch/json" +
                  $"?query={query}" +
                  $"&language=pt" +
                  $"&key={_options.ApiKey}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode(); 

        var json = await response.Content.ReadAsStringAsync();

        var placesResponse = JsonSerializer.Deserialize<GooglePlacesResponse>(json);

        if (placesResponse?.Status != "OK" && placesResponse?.Status != "ZERO_RESULTS")
        {
            _logger.LogWarning("Google Places API devolveu status: {Status}",
                placesResponse?.Status);
        }

        return placesResponse?.Results ?? new List<PlaceResult>();
    }

    private async Task<GooglePlaceResult> EnrichWithDetailsAsync(PlaceResult place)
    {
        var result = new GooglePlaceResult
        {
            PlaceId = place.PlaceId,
            BusinessName = place.Name,
            Address = place.FormattedAddress,
            Rating = place.Rating
        };

        try
        {
           
            var url = $"{_options.BaseUrl}/details/json" +
                      $"?place_id={place.PlaceId}" +
                      $"&fields=formatted_phone_number,website,international_phone_number" +
                      $"&language=pt" +
                      $"&key={_options.ApiKey}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var detailsResponse = JsonSerializer.Deserialize<PlaceDetailsResponse>(json);

            if (detailsResponse?.Status == "OK" && detailsResponse.Result != null)
            {
                result.PhoneNumber = detailsResponse.Result.FormattedPhoneNumber
                                     ?? detailsResponse.Result.InternationalPhoneNumber;
                result.Website = detailsResponse.Result.Website;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Não foi possível obter detalhes para PlaceId: {PlaceId}", place.PlaceId);
        }

        return result;
    }
}