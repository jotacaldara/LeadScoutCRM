using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LeadScoutCRM.Controllers;

[Authorize]
public class AnalyticsController : Controller
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // CORRIGIDO: filtrar apenas leads do utilizador autenticado
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var leads = await _db.Leads
            .Where(l => l.UserId == userId)
            .ToListAsync();

        var now = DateTime.UtcNow;

        // Leads por status
        var byStatus = leads
            .GroupBy(l => l.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Leads por nicho (top 8)
        var byNiche = leads
            .Where(l => !string.IsNullOrEmpty(l.Niche))
            .GroupBy(l => l.Niche!)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .ToDictionary(g => g.Key, g => g.Count());

        // Leads por dia (últimos 30 dias)
        var last30 = Enumerable.Range(0, 30)
            .Select(i => now.Date.AddDays(-29 + i))
            .ToDictionary(
                d => d.ToString("dd/MM"),
                d => leads.Count(l => l.CreatedAt.Date == d));

        // Nicho com melhor conversão
        var bestNiche = leads
            .Where(l => !string.IsNullOrEmpty(l.Niche))
            .GroupBy(l => l.Niche!)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g =>
                (double)g.Count(l => l.Status == LeadStatus.ClienteFechado) / g.Count())
            .FirstOrDefault()?.Key ?? "—";

        var model = new AnalyticsViewModel
        {
            TotalLeads = leads.Count,
            LeadsThisWeek = leads.Count(l => l.CreatedAt >= now.AddDays(-7)),
            ConversionRate = leads.Count == 0 ? 0
                : Math.Round((double)leads.Count(l => l.Status == LeadStatus.ClienteFechado)
                    / leads.Count * 100, 1),
            ContactRate = leads.Count == 0 ? 0
                : Math.Round((double)leads.Count(l => l.LastContactedAt.HasValue)
                    / leads.Count * 100, 1),
            TopNiche = byNiche.FirstOrDefault().Key ?? "—",
            BestConversionNiche = bestNiche,
            LeadsByStatus = byStatus,
            LeadsByNiche = byNiche,
            LeadsPerDay = last30
        };

        return View(model);
    }
}