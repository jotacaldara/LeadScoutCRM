using System.Security.Cryptography;
using System.Text;

namespace LeadScoutCRM.Services;

// Gera e verifica chaves API para o acesso externo (plano Business).
// só o hash SHA-256 fica guardado na base de dados. Mesmo princípio que
// o Identity usa para passwords, aplicado aqui a chaves API.
public static class ApiKeyHasher
{
    private const string Prefix = "lsk_"; // LeadScout Key

    public static string GenerateKey()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");

        return Prefix + token;
    }

    public static string Hash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes);
    }
}