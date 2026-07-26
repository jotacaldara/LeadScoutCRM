using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace LeadScoutCRM.Services;

public class SubscriptionService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        ILogger<SubscriptionService> logger)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
        _logger = logger;
    }

    // Conta as leads do utilizador
    public async Task<int> GetLeadCountAsync(string userId)
        => await _db.Leads.CountAsync(l => l.UserId == userId);

    // Verifica se o utilizador pode adicionar mais leads
    public async Task<bool> CanAddLeadAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        var plan = PlanConfig.Plans[user.Plan];
        if (plan.LeadLimit == int.MaxValue) return true;

        var count = await GetLeadCountAsync(userId);
        return count < plan.LeadLimit;
    }

    // Cria uma sessão de Checkout do Stripe
    public async Task<string> CreateCheckoutSessionAsync(
        string userId, SubscriptionPlan targetPlan, string baseUrl)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");

        var planInfo = PlanConfig.Plans[targetPlan];
        if (planInfo.StripePriceId == null)
            throw new InvalidOperationException("Plano não tem preço Stripe.");

        var customerService = new CustomerService();

        if (!string.IsNullOrEmpty(user.StripeCustomerId))
        {
            try
            {
                await customerService.GetAsync(user.StripeCustomerId);
            }
            catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
            {
                _logger.LogWarning("Customer {Id} não existe. A criar novo.", user.StripeCustomerId);
                user.StripeCustomerId = null;
            }
        }

        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = user.Email,
                Name = user.DisplayName,
                Metadata = new Dictionary<string, string> { ["userId"] = userId }
            });
            user.StripeCustomerId = customer.Id;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Stripe customer criado: {CustomerId} para {Email}", customer.Id, user.Email);
        }

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Customer = user.StripeCustomerId,
            Mode = "subscription",
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = planInfo.StripePriceId,
                    Quantity = 1
                }
            },
            SuccessUrl = $"{baseUrl}/Account/SubscriptionSuccess?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{baseUrl}/Pricing",
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["plan"] = targetPlan.ToString()
            }
        });

        return session.Url;
    }

    // Cria portal do cliente Stripe
    public async Task<string> CreateCustomerPortalAsync(string userId, string baseUrl)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");

        if (string.IsNullOrEmpty(user.StripeCustomerId))
            throw new InvalidOperationException("Sem customer no Stripe.");

        var portalService = new Stripe.BillingPortal.SessionService();
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = user.StripeCustomerId,
            ReturnUrl = $"{baseUrl}/Account/Settings"
        });

        return session.Url;
    }

    // MÉTODO PRINCIPAL: Activar plano usando userId (vem do metadata da sessão Checkout)
    // Este método é mais fiável porque usa o userId directamente do metadata
    public async Task ActivatePlanByUserIdAsync(
        string userId, string stripeSubscriptionId, SubscriptionPlan plan, string? stripeCustomerId = null)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("ActivatePlanByUserId: utilizador {UserId} não encontrado.", userId);
            return;
        }

        user.Plan = plan;
        user.StripeSubscriptionId = stripeSubscriptionId;
        user.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionStartedAt = DateTime.UtcNow;
        user.SubscriptionEndsAt = null;

        // Garante que o StripeCustomerId fica guardado
        if (!string.IsNullOrEmpty(stripeCustomerId) && string.IsNullOrEmpty(user.StripeCustomerId))
            user.StripeCustomerId = stripeCustomerId;

        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Plano {Plan} activado via userId para {Email}", plan, user.Email);
    }

    // Activar plano após pagamento — busca por StripeCustomerId (fallback)
    public async Task ActivatePlanAsync(
        string stripeCustomerId, string stripeSubscriptionId, SubscriptionPlan plan)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.StripeCustomerId == stripeCustomerId);

        if (user == null)
        {
            _logger.LogWarning("Webhook: customer {Id} não encontrado.", stripeCustomerId);
            return;
        }

        user.Plan = plan;
        user.StripeSubscriptionId = stripeSubscriptionId;
        user.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionStartedAt = DateTime.UtcNow;
        user.SubscriptionEndsAt = null;

        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Plano {Plan} activado para {Email}", plan, user.Email);
    }

    // Cancelar plano (chamado pelo webhook)
    public async Task CancelPlanAsync(string stripeCustomerId)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.StripeCustomerId == stripeCustomerId);

        if (user == null) return;

        user.Plan = SubscriptionPlan.Free;
        user.SubscriptionStatus = SubscriptionStatus.Cancelled;
        user.SubscriptionEndsAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Plano cancelado para {Email}", user.Email);
    }
}