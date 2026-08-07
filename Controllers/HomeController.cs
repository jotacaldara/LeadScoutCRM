using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View("Landing");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        ViewBag.TotalLeads = await _db.Leads
            .CountAsync(l => l.UserId == userId);
        ViewBag.LeadsFechadas = await _db.Leads
            .CountAsync(l => l.UserId == userId && l.Status == LeadStatus.ClienteFechado);
        ViewBag.EmNegociacao = await _db.Leads
            .CountAsync(l => l.UserId == userId && l.Status == LeadStatus.EmNegociacao);
        ViewBag.LeadsHoje = await _db.Leads
            .CountAsync(l => l.UserId == userId && l.CreatedAt.Date == DateTime.UtcNow.Date);
        ViewBag.RecentLeads = await _db.Leads
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(5)
            .ToListAsync();

        return View();
    }
}