namespace LeadScoutCRM.Services;

public interface IAiService
{
    Task<string> GenerateOutreachMessageAsync(
        string businessName, string? niche, string? city,
        string? phoneNumber, string? website, string messageType);
}