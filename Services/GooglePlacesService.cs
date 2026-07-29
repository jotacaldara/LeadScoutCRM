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

    public async Task<PlacesSearchResult> SearchPlacesAsync(
     string niche, string location, string? pageToken = null)
    {
        var result = new PlacesSearchResult();

        try
        {
            var placesResponse = await TextSearchAsync(niche, location, pageToken);

            var tasks = placesResponse.Results
                .Select(place => EnrichWithDetailsAsync(place));

            var enrichedResults = await Task.WhenAll(tasks);
            result.Results.AddRange(enrichedResults);
            result.NextPageToken = placesResponse.NextPageToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar no Google Places: {Niche} em {Location}",
                niche, location);

            throw;
        }

        return result;
    }


    private async Task<GooglePlacesResponse> TextSearchAsync(
    string niche, string location, string? pageToken)
    {
        string url;

        if (!string.IsNullOrEmpty(pageToken))
        {
            url = $"{_options.BaseUrl}/textsearch/json" +
                  $"?pagetoken={pageToken}" +
                  $"&key={_options.ApiKey}";
        }
        else
        {
            var query = Uri.EscapeDataString($"{niche} em {location}");
            url = $"{_options.BaseUrl}/textsearch/json" +
                  $"?query={query}" +
                  $"&language=pt" +
                  $"&key={_options.ApiKey}";
        }

        GooglePlacesResponse? placesResponse = null;

        // O pagetoken do Google só fica válido alguns segundos depois da pesquisa
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            placesResponse = JsonSerializer.Deserialize<GooglePlacesResponse>(json);

            var needsRetry = !string.IsNullOrEmpty(pageToken) &&
                              placesResponse?.Status == "INVALID_REQUEST";

            if (!needsRetry) break;

            _logger.LogInformation(
                "Pagetoken ainda não válido (tentativa {Attempt}/3), a aguardar...", attempt);
            await Task.Delay(2000 * attempt); // 2s, depois 4s
        }

        if (placesResponse?.Status != "OK" && placesResponse?.Status != "ZERO_RESULTS")
        {
            _logger.LogWarning("Google Places API devolveu status: {Status}", placesResponse?.Status);
        }

        return placesResponse ?? new GooglePlacesResponse();
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