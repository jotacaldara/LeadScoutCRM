namespace LeadScoutCRM.Services.Models;

// Esta classe mapeia a secção "GooglePlaces" do appsettings.json
// Options Pattern 
public class GooglePlacesOptions
{
    public const string SectionName = "GooglePlaces";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}