using System.Security.Cryptography;

namespace LeadScoutCRM.Services.Webhooks;

public static class WebhookSecretGenerator
{
    private const string Prefix = "lswh_"; // LeadScout Webhook

    public static string Generate()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(randomBytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");

        return Prefix + token;
    }
}