using System.ComponentModel.DataAnnotations;

namespace LeadScoutCRM.Models.Entities;

public enum WebhookEventType
{
    LeadCreated = 0,
    LeadStatusChanged = 1,
    All = 2
}

// Subscrição de um utilizador Business a eventos do LeadScout — permite que
// ferramentas externas (Zapier, Make, scripts próprios) sejam avisadas
// automaticamente, em vez de terem de perguntar periodicamente à API.
public class WebhookSubscription
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string TargetUrl { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Description { get; set; }

    public WebhookEventType EventType { get; set; } = WebhookEventType.All;

    // Guardado em claro de propósito — é o próprio servidor que precisa
    // deste valor para assinar cada entrega (mesmo modelo do Stripe/GitHub).
    [Required]
    public string Secret { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WebhookDeliveryLog> DeliveryLogs { get; set; } = new List<WebhookDeliveryLog>();
}