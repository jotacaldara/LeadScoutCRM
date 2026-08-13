using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace LeadScoutCRM.Controllers;

[ApiController]
[Route("api/stripe/webhook")]
public class StripeWebhookController : ControllerBase
{
    private readonly Services.SubscriptionService _subscriptionService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        Services.SubscriptionService subscriptionService,
        IEmailService emailService,
        IConfiguration config,
        ILogger<StripeWebhookController> logger)
    {
        _subscriptionService = subscriptionService;
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var webhookSecret = _config["Stripe:WebhookSecret"]!;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret,
                throwOnApiVersionMismatch: false
            );
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Webhook inválido: {Message} | Type: {Type}",
       ex.Message, ex.StripeError?.Type);
            return BadRequest();
        }

        _logger.LogInformation("Stripe webhook recebido: {EventType}", stripeEvent.Type);

        // ── MÉTODO PRINCIPAL: checkout.session.completed ──────────────────────────
        if (stripeEvent.Type == Events.CheckoutSessionCompleted)
        {
            var session = (Session)stripeEvent.Data.Object;

            if (session.Mode == "subscription")
            {
                var userId = session.Metadata?.GetValueOrDefault("userId");
                var planStr = session.Metadata?.GetValueOrDefault("plan");

                _logger.LogInformation(
                    "Checkout completado — userId: {UserId}, plan: {Plan}, subscriptionId: {SubId}",
                    userId, planStr, session.SubscriptionId);

                if (!string.IsNullOrEmpty(userId) &&
                    !string.IsNullOrEmpty(planStr) &&
                    Enum.TryParse<SubscriptionPlan>(planStr, out var plan))
                {
                    await _subscriptionService.ActivatePlanByUserIdAsync(
                        userId,
                        session.SubscriptionId ?? "",
                        plan,
                        session.CustomerId
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "checkout.session.completed sem metadata válido. userId={UserId} plan={Plan}",
                        userId, planStr);
                }
            }
        }

        // ── FALLBACK: customer.subscription.created/updated ──────────────────────
        else if (stripeEvent.Type == Events.CustomerSubscriptionCreated ||
                 stripeEvent.Type == Events.CustomerSubscriptionUpdated)
        {
            var subscription = (Subscription)stripeEvent.Data.Object;

            if (subscription.Status == "active" || subscription.Status == "trialing")
            {
                var priceId = subscription.Items.Data.FirstOrDefault()?.Price.Id;
                var plan = GetPlanByPriceId(priceId);

                _logger.LogInformation(
                    "Subscrição {Event} — customerId: {CustomerId}, plan: {Plan}",
                    stripeEvent.Type, subscription.CustomerId, plan);

                await _subscriptionService.ActivatePlanAsync(
                    subscription.CustomerId,
                    subscription.Id,
                    plan
                );

                // Cancelou mas mantém acesso até final do período pago:
                // guarda a data para o lembrete automático o avisar antes de expirar.
                if (subscription.CancelAtPeriodEnd)
                {
                    await _subscriptionService.ScheduleCancellationAsync(
                        subscription.CustomerId, subscription.CurrentPeriodEnd);
                }
            }
        }

        // ── Subscrição cancelada ou expirada ──────────────────────────────────────
        else if (stripeEvent.Type == Events.CustomerSubscriptionDeleted)
        {
            var subscription = (Subscription)stripeEvent.Data.Object;

            _logger.LogInformation(
                "Subscrição cancelada — customerId: {CustomerId}", subscription.CustomerId);

            await _subscriptionService.CancelPlanAsync(subscription.CustomerId);
        }

        // ── Pagamento falhado ─────────────────────────────────────────────────────
        else if (stripeEvent.Type == Events.InvoicePaymentFailed)
        {
            var invoice = (Invoice)stripeEvent.Data.Object;
            _logger.LogWarning("Pagamento falhado para customer {CustomerId}", invoice.CustomerId);

            var user = await _subscriptionService.MarkPastDueAsync(invoice.CustomerId);

            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                await _emailService.SendPaymentFailedEmailAsync(
                    user.Email, user.DisplayName, PlanConfig.Plans[user.Plan].Name);
            }
        }

        return Ok();
    }

    private static SubscriptionPlan GetPlanByPriceId(string? priceId)
    {
        if (priceId == null) return SubscriptionPlan.Free;

        foreach (var (plan, info) in PlanConfig.Plans)
        {
            if (info.StripePriceId == priceId)
                return plan;
        }

        return SubscriptionPlan.Free;
    }
}