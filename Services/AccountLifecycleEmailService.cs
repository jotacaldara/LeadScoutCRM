using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LeadScoutCRM.Services;

// Corre em segundo plano durante toda a vida da aplicação e, periodicamente,
// verifica se há utilizadores a quem faz sentido enviar um lembrete automático:
//subscrições que terminam nos próximos 3 dias (cancelamento agendado no Stripe)
//utilizadores Free perto do limite de leads
// Segue o mesmo padrão do WebhookDispatcherBackgroundService: um scope novo
// por ciclo, porque o DbContext e o UserManager não são partilháveis entre pedidos.
public class AccountLifecycleEmailService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountLifecycleEmailService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
    private const int MinDaysBetweenReminders = 3;

    public AccountLifecycleEmailService(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountLifecycleEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Espera inicial para deixar a app arrancar por completo (seed, etc.)
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no ciclo de emails automáticos de conta.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var reminderCutoff = now.AddDays(-MinDaysBetweenReminders);

        // Subscrições prestes a terminar ──────────────────────────────────
        var endingSoon = await userManager.Users
            .Where(u => u.SubscriptionEndsAt != null
                     && u.SubscriptionEndsAt > now
                     && u.SubscriptionEndsAt <= now.AddDays(3)
                     && (u.LastReminderEmailSentAt == null || u.LastReminderEmailSentAt < reminderCutoff))
            .ToListAsync(ct);

        foreach (var user in endingSoon)
        {
            if (string.IsNullOrEmpty(user.Email)) continue;

            var sent = await emailService.SendSubscriptionReminderAsync(
                user.Email, user.DisplayName,
                PlanConfig.Plans[user.Plan].Name,
                user.SubscriptionEndsAt);

            if (sent)
            {
                user.LastReminderEmailSentAt = now;
                await userManager.UpdateAsync(user);
                _logger.LogInformation("Lembrete de renovação enviado automaticamente para {Email}", user.Email);
            }
        }

        // ── 2) Utilizadores Free perto do limite de leads ──────────────────────
        var freeUsers = await userManager.Users
            .Where(u => u.Plan == SubscriptionPlan.Free
                     && (u.LastReminderEmailSentAt == null || u.LastReminderEmailSentAt < now.AddDays(-7)))
            .ToListAsync(ct);

        if (freeUsers.Count == 0) return;

        var freeUserIds = freeUsers.Select(u => u.Id).ToList();
        var leadCounts = await db.Leads
            .Where(l => freeUserIds.Contains(l.UserId))
            .GroupBy(l => l.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        foreach (var user in freeUsers)
        {
            if (string.IsNullOrEmpty(user.Email)) continue;

            var count = leadCounts.TryGetValue(user.Id, out var c) ? c : 0;
            var pct = (double)count / PlanConfig.FreeLeadLimit;
            if (pct < 0.8) continue; // só avisa a partir de 80% do limite

            var sent = await emailService.SendUpgradeNudgeAsync(
                user.Email, user.DisplayName, count, PlanConfig.FreeLeadLimit);

            if (sent)
            {
                user.LastReminderEmailSentAt = now;
                await userManager.UpdateAsync(user);
                _logger.LogInformation("Nudge de upgrade enviado automaticamente para {Email}", user.Email);
            }
        }
    }
}