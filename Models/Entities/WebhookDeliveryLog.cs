namespace LeadScoutCRM.Models.Entities;

// Regista cada tentativa de entrega — essencial para depuração e para
// o utilizador confirmar, na própria aplicação, que a integração funciona.
public class WebhookDeliveryLog
{
    public int Id { get; set; }

    public int WebhookSubscriptionId { get; set; }
    public WebhookSubscription WebhookSubscription { get; set; } = null!;

    public string EventType { get; set; } = string.Empty;
    public int? ResponseStatusCode { get; set; }
    public bool Success { get; set; }
    public string? ResponseSnippet { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}