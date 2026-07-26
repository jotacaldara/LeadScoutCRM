using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace LeadScoutCRM.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly SubscriptionService _subscriptionService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IEmailService emailService,
        SubscriptionService subscriptionService,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _db = db;
        _emailService = emailService;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        var now = DateTime.UtcNow;

        var proActive = users.Count(u => u.Plan == SubscriptionPlan.Pro && u.SubscriptionStatus == SubscriptionStatus.Active);
        var bizActive = users.Count(u => u.Plan == SubscriptionPlan.Business && u.SubscriptionStatus == SubscriptionStatus.Active);
        var freeUsers = users.Count(u => u.Plan == SubscriptionPlan.Free);
        var mrr = (proActive * 19m) + (bizActive * 49m);
        var newThisWeek = users.Count(u => u.CreatedAt >= now.AddDays(-7));
        var newThisMonth = users.Count(u => u.CreatedAt >= now.AddDays(-30));

        // Alertas
        var nearLimit = new List<(ApplicationUser User, int Count, int Limit)>();
        var freeUsersNearLimit = users.Where(u => u.Plan == SubscriptionPlan.Free).ToList();
        foreach (var u in freeUsersNearLimit)
        {
            var count = await _db.Leads.CountAsync(l => l.UserId == u.Id);
            if (count >= 8)
                nearLimit.Add((u, count, PlanConfig.FreeLeadLimit));
        }

        var pastDue = users.Where(u => u.SubscriptionStatus == SubscriptionStatus.PastDue).ToList();

        // Leads por dia (últimos 14 dias)
        var allLeads = await _db.Leads
            .Where(l => l.CreatedAt >= now.AddDays(-14))
            .ToListAsync();
        var leadsPerDay = Enumerable.Range(0, 14)
            .Select(i => now.Date.AddDays(-13 + i))
            .ToDictionary(
                d => d.ToString("dd/MM"),
                d => allLeads.Count(l => l.CreatedAt.Date == d));

        // Novos utilizadores por dia (últimos 14 dias)
        var usersPerDay = Enumerable.Range(0, 14)
            .Select(i => now.Date.AddDays(-13 + i))
            .ToDictionary(
                d => d.ToString("dd/MM"),
                d => users.Count(u => u.CreatedAt.Date == d));

        ViewBag.TotalUsers = users.Count;
        ViewBag.ProUsers = proActive;
        ViewBag.BusinessUsers = bizActive;
        ViewBag.FreeUsers = freeUsers;
        ViewBag.MRR = mrr;
        ViewBag.ARR = mrr * 12;
        ViewBag.NewThisWeek = newThisWeek;
        ViewBag.NewThisMonth = newThisMonth;
        ViewBag.TotalLeads = await _db.Leads.CountAsync();
        ViewBag.NearLimit = nearLimit;
        ViewBag.PastDue = pastDue;
        ViewBag.RecentUsers = users.OrderByDescending(u => u.CreatedAt).Take(8).ToList();
        ViewBag.LeadsPerDay = System.Text.Json.JsonSerializer.Serialize(leadsPerDay.Values);
        ViewBag.LeadsDayLabels = System.Text.Json.JsonSerializer.Serialize(leadsPerDay.Keys);
        ViewBag.UsersPerDay = System.Text.Json.JsonSerializer.Serialize(usersPerDay.Values);
        ViewBag.ConversionRate = users.Count == 0 ? 0
            : Math.Round((double)(proActive + bizActive) / users.Count * 100, 1);

        return View();
    }

    // ── Lista de utilizadores ─────────────────────────────────────────────────

    [HttpGet("Users")]
    public async Task<IActionResult> Users(string? search, string? plan, string? status)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(u =>
                u.Email!.Contains(search) || u.DisplayName.Contains(search));

        if (!string.IsNullOrEmpty(plan) && Enum.TryParse<SubscriptionPlan>(plan, out var planEnum))
            query = query.Where(u => u.Plan == planEnum);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubscriptionStatus>(status, out var statusEnum))
            query = query.Where(u => u.SubscriptionStatus == statusEnum);

        var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

        ViewBag.LeadCounts = await _db.Leads
            .GroupBy(l => l.UserId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        ViewBag.Search = search;
        ViewBag.PlanFilter = plan;
        ViewBag.StatusFilter = status;

        return View(users);
    }

    // ── Detalhe de utilizador ─────────────────────────────────────────────────

    [HttpGet("Users/{id}")]
    public async Task<IActionResult> UserDetail(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var leads = await _db.Leads
            .Where(l => l.UserId == id)
            .OrderByDescending(l => l.CreatedAt)
            .Take(20)
            .ToListAsync();

        var roles = await _userManager.GetRolesAsync(user);

        var leadsByStatus = leads
            .GroupBy(l => l.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        ViewBag.Leads = leads;
        ViewBag.Roles = roles;
        ViewBag.LeadCount = await _db.Leads.CountAsync(l => l.UserId == id);
        ViewBag.NoteCount = await _db.Notes.CountAsync(n => n.Lead.UserId == id);
        ViewBag.LeadsByStatus = leadsByStatus;

        return View(user);
    }

    // ── Alterar plano ─────────────────────────────────────────────────────────

    [HttpPost("Users/{id}/SetPlan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPlan(string id, string plan)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (Enum.TryParse<SubscriptionPlan>(plan, out var newPlan))
        {
            user.Plan = newPlan;
            user.SubscriptionStatus = newPlan == SubscriptionPlan.Free
                ? SubscriptionStatus.None
                : SubscriptionStatus.Active;
            if (newPlan != SubscriptionPlan.Free)
                user.SubscriptionStartedAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);
            TempData["Success"] = $"Plano de {user.Email} alterado para {newPlan}.";
            _logger.LogInformation("Admin alterou plano de {Email} para {Plan}", user.Email, newPlan);
        }

        return RedirectToAction(nameof(UserDetail), new { id });
    }

    // ── Enviar email de reminder ──────────────────────────────────────────────

    [HttpPost("Users/{id}/SendReminder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendReminder(string id, string reminderType = "renewal")
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        bool sent = reminderType switch
        {
            "upgrade" => await _emailService.SendUpgradeNudgeAsync(
                user.Email!, user.DisplayName,
                await _db.Leads.CountAsync(l => l.UserId == id),
                PlanConfig.FreeLeadLimit),

            "welcome" => await _emailService.SendWelcomeEmailAsync(
                user.Email!, user.DisplayName),

            _ => await _emailService.SendSubscriptionReminderAsync(
                user.Email!, user.DisplayName,
                PlanConfig.Plans[user.Plan].Name,
                user.SubscriptionEndsAt)
        };

        TempData[sent ? "Success" : "Error"] = sent
            ? $"Email de {reminderType} enviado para {user.Email}."
            : "Erro ao enviar email. Verifica a configuração SMTP em appsettings.json.";

        return RedirectToAction(nameof(UserDetail), new { id });
    }

    // ── Email personalizado ───────────────────────────────────────────────────

    [HttpPost("Users/{id}/SendCustomEmail")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCustomEmail(string id, string subject, string body)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            TempData["Error"] = "Assunto e corpo são obrigatórios.";
            return RedirectToAction(nameof(UserDetail), new { id });
        }

        var html = $"""
            <!DOCTYPE html><html>
            <body style="font-family:'Segoe UI',sans-serif;background:#f1f5f9;margin:0;padding:2rem;">
              <div style="max-width:520px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);">
                <div style="background:#6366f1;padding:2rem;text-align:center;">
                  <h1 style="color:#fff;margin:0;font-size:1.4rem;">⚡ LeadScout CRM</h1>
                </div>
                <div style="padding:2rem;">
                  <p style="color:#475569;line-height:1.7;white-space:pre-wrap;">{body}</p>
                  <hr style="border:none;border-top:1px solid #e2e8f0;margin:1.5rem 0;">
                  <p style="color:#94a3b8;font-size:.8rem;text-align:center;">
                    LeadScout CRM · <a href="https://leadscoutcrm.com" style="color:#6366f1;">leadscoutcrm.com</a>
                  </p>
                </div>
              </div>
            </body></html>
            """;

        var sent = await _emailService.SendEmailAsync(user.Email!, user.DisplayName, subject, html);

        TempData[sent ? "Success" : "Error"] = sent
            ? $"Email enviado para {user.Email}."
            : "Erro ao enviar email. Verifica a configuração SMTP.";

        return RedirectToAction(nameof(UserDetail), new { id });
    }

    // ── Eliminar utilizador ───────────────────────────────────────────────────

    [HttpPost("Users/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var currentAdmin = await _userManager.GetUserAsync(User);
        if (currentAdmin?.Id == id)
        {
            TempData["Error"] = "Não podes eliminar a tua própria conta.";
            return RedirectToAction(nameof(UserDetail), new { id });
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var email = user.Email;

        // Leads e notas eliminadas por CASCADE no DB
        var result = await _userManager.DeleteAsync(user);

        if (result.Succeeded)
        {
            _logger.LogWarning("Admin eliminou utilizador: {Email}", email);
            TempData["Success"] = $"Utilizador {email} eliminado com sucesso.";
            return RedirectToAction(nameof(Users));
        }

        TempData["Error"] = "Erro ao eliminar utilizador.";
        return RedirectToAction(nameof(UserDetail), new { id });
    }

    // ── Exportar utilizadores CSV ─────────────────────────────────────────────

    [HttpGet("ExportUsers")]
    public async Task<IActionResult> ExportUsers()
    {
        var users = await _userManager.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var leadCounts = await _db.Leads
            .GroupBy(l => l.UserId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        var sb = new StringBuilder();
        sb.AppendLine("Email,Nome,Plano,Status,Leads,Registo,StripeCustomerId,SubscritionID");

        foreach (var u in users)
        {
            var lc = leadCounts.TryGetValue(u.Id, out var c) ? c : 0;
            sb.AppendLine(
                $"\"{u.Email}\",\"{u.DisplayName}\",{u.Plan},{u.SubscriptionStatus}," +
                $"{lc},{u.CreatedAt:yyyy-MM-dd}," +
                $"\"{u.StripeCustomerId ?? ""}\",\"{u.StripeSubscriptionId ?? ""}\"");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"leadscout-users-{DateTime.UtcNow:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    // ── Revenue Analytics ─────────────────────────────────────────────────────

    [HttpGet("Revenue")]
    public async Task<IActionResult> Revenue()
    {
        var users = await _userManager.Users.ToListAsync();
        var now = DateTime.UtcNow;

        var proActive = users.Where(u => u.Plan == SubscriptionPlan.Pro && u.SubscriptionStatus == SubscriptionStatus.Active).ToList();
        var bizActive = users.Where(u => u.Plan == SubscriptionPlan.Business && u.SubscriptionStatus == SubscriptionStatus.Active).ToList();

        var mrr = (proActive.Count * 19m) + (bizActive.Count * 49m);

        // Novos pagantes por mês (últimos 6 meses)
        var revenuePerMonth = Enumerable.Range(0, 6)
            .Select(i => now.Date.AddMonths(-5 + i))
            .ToDictionary(
                d => d.ToString("MMM yy"),
                d => {
                    var proM = users.Count(u =>
                        u.Plan == SubscriptionPlan.Pro &&
                        u.SubscriptionStatus == SubscriptionStatus.Active &&
                        u.SubscriptionStartedAt.HasValue &&
                        u.SubscriptionStartedAt.Value.Year == d.Year &&
                        u.SubscriptionStartedAt.Value.Month == d.Month);
                    var bizM = users.Count(u =>
                        u.Plan == SubscriptionPlan.Business &&
                        u.SubscriptionStatus == SubscriptionStatus.Active &&
                        u.SubscriptionStartedAt.HasValue &&
                        u.SubscriptionStartedAt.Value.Year == d.Year &&
                        u.SubscriptionStartedAt.Value.Month == d.Month);
                    return (proM * 19) + (bizM * 49);
                });

        ViewBag.MRR = mrr;
        ViewBag.ARR = mrr * 12;
        ViewBag.ProCount = proActive.Count;
        ViewBag.BizCount = bizActive.Count;
        ViewBag.FreeCount = users.Count(u => u.Plan == SubscriptionPlan.Free);
        ViewBag.TotalUsers = users.Count;
        ViewBag.ProRevenue = proActive.Count * 19m;
        ViewBag.BizRevenue = bizActive.Count * 49m;
        ViewBag.AvgRevPerUser = users.Count > 0 ? mrr / users.Count : 0;
        ViewBag.ConversionRate = users.Count > 0
            ? Math.Round((double)(proActive.Count + bizActive.Count) / users.Count * 100, 1)
            : 0;

        ViewBag.RevenueLabels = System.Text.Json.JsonSerializer.Serialize(revenuePerMonth.Keys);
        ViewBag.RevenueData = System.Text.Json.JsonSerializer.Serialize(revenuePerMonth.Values);

        return View();
    }

    // ── Subscrições activas ───────────────────────────────────────────────────

    [HttpGet("Subscriptions")]
    public async Task<IActionResult> Subscriptions(string? statusFilter)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrEmpty(statusFilter) &&
            Enum.TryParse<SubscriptionStatus>(statusFilter, out var statusEnum))
        {
            query = query.Where(u => u.SubscriptionStatus == statusEnum);
        }
        else
        {
            // Por defeito mostra todos os pagantes (não Free/None)
            query = query.Where(u =>
                u.SubscriptionStatus != SubscriptionStatus.None ||
                u.Plan != SubscriptionPlan.Free);
        }

        var users = await query
            .OrderByDescending(u => u.SubscriptionStartedAt)
            .ToListAsync();

        ViewBag.StatusFilter = statusFilter;
        return View(users);
    }

    // ── Centro de Email (bulk) ────────────────────────────────────────────────

    [HttpGet("EmailCenter")]
    public IActionResult EmailCenter() => View();

    [HttpPost("EmailCenter/Send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendBulkEmail(
        string targetGroup, string emailType, string? customSubject, string? customBody)
    {
        var users = await _userManager.Users.ToListAsync();

        List<ApplicationUser> targets = targetGroup switch
        {
            "free" => users.Where(u => u.Plan == SubscriptionPlan.Free).ToList(),
            "pro" => users.Where(u => u.Plan == SubscriptionPlan.Pro).ToList(),
            "business" => users.Where(u => u.Plan == SubscriptionPlan.Business).ToList(),
            "paid" => users.Where(u => u.Plan != SubscriptionPlan.Free).ToList(),
            "pastdue" => users.Where(u => u.SubscriptionStatus == SubscriptionStatus.PastDue).ToList(),
            _ => users // all
        };

        var sent = 0;
        var failed = 0;

        foreach (var user in targets)
        {
            if (string.IsNullOrEmpty(user.Email)) continue;

            bool ok = emailType switch
            {
                "upgrade" => await _emailService.SendUpgradeNudgeAsync(
                    user.Email, user.DisplayName,
                    await _db.Leads.CountAsync(l => l.UserId == user.Id),
                    PlanConfig.FreeLeadLimit),
                "renewal" => await _emailService.SendSubscriptionReminderAsync(
                    user.Email, user.DisplayName,
                    PlanConfig.Plans[user.Plan].Name,
                    user.SubscriptionEndsAt),
                "welcome" => await _emailService.SendWelcomeEmailAsync(
                    user.Email, user.DisplayName),
                "custom" => !string.IsNullOrWhiteSpace(customSubject) && !string.IsNullOrWhiteSpace(customBody)
                    ? await _emailService.SendEmailAsync(user.Email, user.DisplayName,
                        customSubject,
                        $"<div style='font-family:sans-serif;padding:2rem;'><p style='white-space:pre-wrap;'>{customBody}</p></div>")
                    : false,
                _ => false
            };

            if (ok) sent++; else failed++;
        }

        _logger.LogInformation("Bulk email: {Sent} enviados, {Failed} falhados — grupo: {Group}", sent, failed, targetGroup);

        TempData["Success"] = $"Emails enviados: {sent} com sucesso" +
            (failed > 0 ? $", {failed} falharam (verifica configuração SMTP)." : ".");

        return RedirectToAction(nameof(EmailCenter));
    }

    // ── Configurações do Admin ────────────────────────────────────────────────

    [HttpGet("Settings")]
    public IActionResult Settings()
    {
        ViewBag.SmtpHost = _emailService.GetType().Name; // just shows service type
        return View();
    }
}