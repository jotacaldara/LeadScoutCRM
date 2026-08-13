using Microsoft.AspNetCore.Identity;
using Stripe;

namespace LeadScoutCRM.Models.Entities;

public class ApplicationUser : IdentityUser
{
    // Nome de apresentação
    public string DisplayName { get; set; } = string.Empty;

    // Plano actual do utilizador
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;

    // Status da subscrição Stripe
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;

    // IDs do Stripe (para gerir no portal)
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }

    // Datas
    public DateTime? LastReminderEmailSentAt { get; set; }
    public DateTime? SubscriptionStartedAt { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }   // null = activa indefinidamente

    // Acesso API (plano Business) — só o hash fica guardado, nunca a chave em claro
    public string? ApiKeyHash { get; set; }
    public DateTime? ApiKeyCreatedAt { get; set; }
    public DateTime? ApiKeyLastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}