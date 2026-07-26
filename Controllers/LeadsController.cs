using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Models.ViewModels;
using LeadScoutCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace LeadScoutCRM.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private readonly AppDbContext _db;
    private readonly IGooglePlacesService _googlePlaces;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(
        AppDbContext db,
        IGooglePlacesService googlePlaces,
        ILogger<LeadsController> logger)
    {
        _db = db;
        _googlePlaces = googlePlaces;
        _logger = logger;
    }

    // Lista todas as leads do utilizador autenticado
    public async Task<IActionResult> Index(
        string? statusFilter,
        string? searchTerm,
        string? niche)
    {
        var query = _db.Leads
            .Where(l => l.UserId == CurrentUserId)
            .Include(l => l.Notes)
            .AsQueryable();

        if (!string.IsNullOrEmpty(statusFilter) &&
            Enum.TryParse<LeadStatus>(statusFilter, out var status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(l =>
                l.BusinessName.Contains(searchTerm) ||
                (l.City != null && l.City.Contains(searchTerm)));
        }

        if (!string.IsNullOrEmpty(niche))
        {
            query = query.Where(l => l.Niche == niche);
        }

        var leads = await query
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        // CORRIGIDO: todos os ViewBag filtrados por utilizador
        ViewBag.StatusFilter = statusFilter;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.NicheFilter = niche;

        ViewBag.TotalLeads = await _db.Leads
            .CountAsync(l => l.UserId == CurrentUserId);

        ViewBag.LeadsFechadas = await _db.Leads
            .CountAsync(l => l.UserId == CurrentUserId && l.Status == LeadStatus.ClienteFechado);

        // Nichos distintos apenas do utilizador actual
        ViewBag.Niches = await _db.Leads
            .Where(l => l.UserId == CurrentUserId && l.Niche != null)
            .Select(l => l.Niche!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        return View(leads);
    }

    // Formulário de pesquisa (GET mostra o form vazio)
    public IActionResult Search()
    {
        return View(new SearchViewModel());
    }

    // Processa o formulário e chama o Google Places
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(SearchViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            model.Results = await _googlePlaces
                .SearchPlacesAsync(model.Niche, model.Location);

            if (!model.Results.Any())
                TempData["Info"] = "Nenhum resultado encontrado. Tenta outro nicho ou localização.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na pesquisa Google Places");
            ModelState.AddModelError("", "Erro ao contactar o Google Places. Verifica a tua chave de API.");
        }

        return View(model);
    }

    // Página de detalhe de uma lead (com notas)
    public async Task<IActionResult> Detail(int id)
    {
        var lead = await _db.Leads
            .Where(l => l.UserId == CurrentUserId && l.Id == id)
            .Include(l => l.Notes.OrderByDescending(n => n.CreatedAt))
            .FirstOrDefaultAsync();

        if (lead == null)
            return NotFound();

        return View(lead);
    }

    // Kanban board — CORRIGIDO: filtrado por utilizador
    public async Task<IActionResult> Kanban()
    {
        var leads = await _db.Leads
            .Where(l => l.UserId == CurrentUserId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        var model = new KanbanViewModel
        {
            TotalLeads = leads.Count,
            Columns = Enum.GetValues<LeadStatus>()
                .ToDictionary(
                    s => s,
                    s => leads.Where(l => l.Status == s).ToList()
                )
        };

        return View(model);
    }
}