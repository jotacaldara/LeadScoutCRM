using System.Text;
using System.Text.Json;
using LeadScoutCRM.Models.Entities;

namespace LeadScoutCRM.Services.Webhooks;

// Executa a entrega HTTP de UM evento a UMA subscrição, e devolve o registo
// do resultado (não grava na BD — quem chama decide quando gravar).
// Usado tanto pelo despachante em segundo plano (eventos reais) como pelo
// botão "Testar" nas Definições (entrega imediata).
public class WebhookDeliveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryService> _logger;

    public WebhookDeliveryService(IHttpClientFactory httpClientFactory, ILogger<WebhookDeliveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<WebhookDeliveryLog> DeliverAsync(
        WebhookSubscription subscription, string eventKey, object payload, CancellationToken ct = default)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            @event = eventKey,
            timestamp = DateTime.UtcNow.ToString("o"),
            data = payload
        });

        var log = new WebhookDeliveryLog
        {
            WebhookSubscriptionId = subscription.Id,
            EventType = eventKey,
            SentAt = DateTime.UtcNow
        };

        try
        {
            var signature = WebhookSigner.Sign(payloadJson, subscription.Secret);
            var client = _httpClientFactory.CreateClient("webhooks");

            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-LeadScout-Event", eventKey);
            request.Headers.Add("X-LeadScout-Signature", $"sha256={signature}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var response = await client.SendAsync(request, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            log.ResponseStatusCode = (int)response.StatusCode;
            log.Success = response.IsSuccessStatusCode;
            log.ResponseSnippet = Truncate(responseBody, 500);
        }
        catch (Exception ex)
        {
            log.Success = false;
            log.ErrorMessage = Truncate(ex.Message, 300);
            _logger.LogWarning(ex, "Falha ao entregar webhook {SubscriptionId}", subscription.Id);
        }

        return log;
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}