namespace LeadScoutCRM.Models.Entities;

public enum SubscriptionPlan
{
    Free = 0,       // 10 leads, sem Kanban avançado
    Pro = 1,        // leads ilimitadas, todas as features
    Business = 2    // tudo + export + API key
}

public enum SubscriptionStatus
{
    None = 0,          // Free (nunca pagou)
    Active = 1,        // Subscrição activa
    PastDue = 2,       // Pagamento falhado (grace period)
    Cancelled = 3,     // Cancelada pelo utilizador
    Trialing = 4       // Em trial (se implementares)
}

// Configuração estática dos planos (preços, limites, features)
public static class PlanConfig
{
    public const int FreeLeadLimit = 10;

    public static readonly Dictionary<SubscriptionPlan, PlanInfo> Plans = new()
    {
        [SubscriptionPlan.Free] = new PlanInfo
        {
            Name = "Free",
            PricePerMonth = 0,
            LeadLimit = FreeLeadLimit,
            HasKanban = true,
            HasExport = false,
            HasApiAccess = false,
            StripePriceId = null
        },
        [SubscriptionPlan.Pro] = new PlanInfo
        {
            Name = "Pro",
            PricePerMonth = 19,
            LeadLimit = int.MaxValue, // ilimitado
            HasKanban = true,
            HasExport = true,
            HasApiAccess = false,
            StripePriceId = "price_1TaFlNFM92YHjK3gqnoq0kzv"
        },
        [SubscriptionPlan.Business] = new PlanInfo
        {
            Name = "Business",
            PricePerMonth = 49,
            LeadLimit = int.MaxValue,
            HasKanban = true,
            HasExport = true,
            HasApiAccess = true,
            StripePriceId = "price_1TaFlaFM92YHjK3gX87RCdUv"
        }
    };
}

public class PlanInfo
{
    public string Name { get; set; } = string.Empty;
    public decimal PricePerMonth { get; set; }
    public int LeadLimit { get; set; }
    public bool HasKanban { get; set; }
    public bool HasExport { get; set; }
    public bool HasApiAccess { get; set; }
    public string? StripePriceId { get; set; }
}