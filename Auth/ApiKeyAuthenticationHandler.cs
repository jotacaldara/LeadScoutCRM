using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace LeadScoutCRM.Auth;

// Autentica pedidos externos através do cabeçalho X-Api-Key.
// É um esquema de autenticação ADICIONAL ao cookie do Identity — não o substitui,
// só se aplica onde é explicitamente pedido via [Authorize(AuthenticationSchemes = "ApiKey")].
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-Api-Key";

    private readonly AppDbContext _db;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
            return AuthenticateResult.NoResult();

        var providedKey = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
            return AuthenticateResult.NoResult();

        var providedHash = ApiKeyHasher.Hash(providedKey);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ApiKeyHash == providedHash);
        if (user == null)
            return AuthenticateResult.Fail("Chave API inválida.");

        var planInfo = PlanConfig.Plans[user.Plan];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName),
            new("plan", user.Plan.ToString()),
            new("has_api_access", planInfo.HasApiAccess ? "true" : "false")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        // Regista a última utilização — visível em Definições > Acesso API
        user.ApiKeyLastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return AuthenticateResult.Success(ticket);
    }

    // 401 — não sabemos quem é (chave em falta ou inválida)
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsync("""{"message":"Chave API em falta ou inválida. Envia o cabeçalho X-Api-Key."}""");
    }

    // 403 — sabemos quem é, mas o plano não permite
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";
        return Response.WriteAsync("""{"message":"O teu plano não inclui acesso à API. Faz upgrade para Business."}""");
    }
}