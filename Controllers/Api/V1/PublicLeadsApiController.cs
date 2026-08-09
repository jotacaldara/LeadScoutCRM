using LeadScoutCRM.Auth;
using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Models.ViewModels;
using LeadScoutCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LeadScoutCRM.Services.Webhooks;

namespace LeadScoutCRM.Controllers.Api.V1;

// API pública para integrações externas — plano Business apenas.
// Autenticação: cabeçalho X-Api-Key (não usa cookies).
// Distinta do LeadsApiController (uso interno da aplicação, cookie-based) por desenho:
// separar o contrato público do contrato interno evita que uma mudança na SPA
// interna quebre integrações de terceiros, e vice-versa.

[ApiController]
[Route("api/v1/leads")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName, Policy = "ApiAccess")]
[Produces("application/json")]
public class PublicLeadsApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SubscriptionService _subscriptionService;
    private readonly ILogger<PublicLeadsApiController> _logger;
    private readonly WebhookDispatchQueue _webhookQueue;

    public PublicLeadsApiController(
        AppDbContext db,
        SubscriptionService subscriptionService,
        ILogger<PublicLeadsApiController> logger,
        WebhookDispatchQueue webhookQueue)
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _logger = logger;
        _webhookQueue = webhookQueue;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public PublicLeadsApiController(
        AppDbContext db,
        SubscriptionService subscriptionService,
        ILogger<PublicLeadsApiController> logger)
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    //Lista as leads do utilizador, com paginação e filtros opcionais
    [HttpGet]
    public async Task<IActionResult> GetLeads(
        [FromQuery] string? status,
        [FromQuery] string? niche,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Leads.Where(l => l.UserId == CurrentUserId).AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<LeadStatus>(status, true, out var statusEnum))
            query = query.Where(l => l.Status == statusEnum);

        if (!string.IsNullOrEmpty(niche))
            query = query.Where(l => l.Niche == niche);

        var total = await query.CountAsync();

        var leads = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => LeadPublicDto.FromEntity(l))
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            data = leads
        });
    }

    //Obtém uma lead específica
    [HttpGet("{id}")]
    public async Task<IActionResult> GetLead(int id)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);
        return lead == null
            ? NotFound(new { message = "Lead não encontrada." })
            : Ok(LeadPublicDto.FromEntity(lead));
    }

    //Cria uma nova lead
    [HttpPost]
    public async Task<IActionResult> CreateLead([FromBody] SaveLeadRequest request)
    {
        if (!await _subscriptionService.CanAddLeadAsync(CurrentUserId))
            return StatusCode(402, new { message = "Limite de leads atingido para o teu plano." });

        if (!string.IsNullOrEmpty(request.PlaceId))
        {
            var exists = await _db.Leads
                .AnyAsync(l => l.GooglePlaceId == request.PlaceId && l.UserId == CurrentUserId);
            if (exists)
                return Conflict(new { message = "Já existe uma lead com este PlaceId." });
        }

        var lead = new Lead
        {
            UserId = CurrentUserId,
            BusinessName = request.BusinessName,
            PhoneNumber = request.PhoneNumber,
            Website = request.Website,
            Address = request.Address,
            Niche = request.Niche,
            City = request.City,
            GooglePlaceId = request.PlaceId,
            Status = LeadStatus.Novo,
            CreatedAt = DateTime.UtcNow
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Lead criada via API pública: {Name} (utilizador {UserId})",
            lead.BusinessName, CurrentUserId);

        await _webhookQueue.EnqueueAsync(new WebhookJob(
    CurrentUserId, nameof(WebhookEventType.LeadCreated), LeadPublicDto.FromEntity(lead)));

        return CreatedAtAction(nameof(GetLead), new { id = lead.Id }, LeadPublicDto.FromEntity(lead));
    }

    //Atualiza o status de uma lead
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);
        if (lead == null) return NotFound(new { message = "Lead não encontrada." });

        if (!Enum.TryParse<LeadStatus>(request.Status, true, out var newStatus))
            return BadRequest(new { message = "Status inválido." });

        lead.Status = newStatus;
        if (newStatus >= LeadStatus.MensagemEnviada)
            lead.LastContactedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _webhookQueue.EnqueueAsync(new WebhookJob(
    CurrentUserId, nameof(WebhookEventType.LeadStatusChanged), LeadPublicDto.FromEntity(lead)));

        return Ok(LeadPublicDto.FromEntity(lead));
    }

    //Apaga uma lead
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLead(int id)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);
        if (lead == null) return NotFound(new { message = "Lead não encontrada." });

        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}