using System.Security.Cryptography;
using System.Text;

namespace LeadScoutCRM.Services.Webhooks;

public static class WebhookSigner
{
    public static string Sign(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = new HMACSHA256(keyBytes).ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}