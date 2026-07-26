using System.Text.Json.Serialization;

namespace LeadScoutCRM.Services.Models;

// Representa a resposta raiz do endpoint /textsearch
public class GooglePlacesResponse
{
    [JsonPropertyName("results")]
    public List<PlaceResult> Results { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    // Token para buscar a próxima página de resultados (se existir)
    [JsonPropertyName("next_page_token")]
    public string? NextPageToken { get; set; }
}

public class PlaceResult
{
    [JsonPropertyName("place_id")]
    public string PlaceId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("formatted_address")]
    public string? FormattedAddress { get; set; }

    [JsonPropertyName("formatted_phone_number")]
    public string? FormattedPhoneNumber { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("rating")]
    public double? Rating { get; set; }

    [JsonPropertyName("user_ratings_total")]
    public int? UserRatingsTotal { get; set; }

    
    [JsonPropertyName("opening_hours")]
    public OpeningHours? OpeningHours { get; set; }
}

public class OpeningHours
{
    [JsonPropertyName("open_now")]
    public bool? OpenNow { get; set; }
}

// Resposta do endpoint /details (para obter telefone e website)
public class PlaceDetailsResponse
{
    [JsonPropertyName("result")]
    public PlaceDetails? Result { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class PlaceDetails
{
    [JsonPropertyName("formatted_phone_number")]
    public string? FormattedPhoneNumber { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("international_phone_number")]
    public string? InternationalPhoneNumber { get; set; }
}