using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Models.ViewModels;
using LeadScoutCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using LeadScoutCRM.Services.Webhooks;

namespace LeadScoutCRM.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Authorize]
public class LeadsApiController : ControllerBase
{
    private readonly Services.SubscriptionService _subscriptionService;
    private readonly AppDbContext _db;
    private readonly ILogger<LeadsApiController> _logger;
    private readonly IAiService _ai;
    private readonly WebhookDispatchQueue _webhookQueue;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public LeadsApiController(
        AppDbContext db,
        ILogger<LeadsApiController> logger,
        IAiService ai,
        Services.SubscriptionService subscriptionService,
        WebhookDispatchQueue webhookQueue)
    {
        _db = db;
        _logger = logger;
        _ai = ai;
        _subscriptionService = subscriptionService;
        _webhookQueue = webhookQueue;
    }

    // Guarda uma nova lead no CRM
    [HttpPost]
    public async Task<IActionResult> SaveLead([FromBody] SaveLeadRequest request)
    {
        var userId = CurrentUserId;

        if (!await _subscriptionService.CanAddLeadAsync(userId))
        {
            return StatusCode(402, new
            {
                message = "Limite de leads atingido. Faz upgrade do teu plano.",
                upgradeUrl = "/Pricing"
            });
        }

        // Verifica duplicado por utilizador
        if (!string.IsNullOrEmpty(request.PlaceId))
        {
            var exists = await _db.Leads
                .AnyAsync(l => l.GooglePlaceId == request.PlaceId && l.UserId == userId);

            if (exists)
                return Conflict(new { message = "Esta empresa já está no teu CRM." });
        }

        var lead = new Lead
        {
            UserId = userId,
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

        _logger.LogInformation("Lead guardada: {BusinessName} para utilizador {UserId}", lead.BusinessName, userId);

        await _webhookQueue.EnqueueAsync(new WebhookJob(
    userId, nameof(WebhookEventType.LeadCreated), LeadPublicDto.FromEntity(lead)));

        return CreatedAtAction(nameof(GetLead), new { id = lead.Id }, new
        {
            id = lead.Id,
            message = $"'{lead.BusinessName}' guardada com sucesso!"
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLead(int id)
    {
        // CORRIGIDO: verifica que a lead pertence ao utilizador
        var lead = await _db.Leads
            .Include(l => l.Notes)
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);

        return lead == null ? NotFound() : Ok(lead);
    }

    // Actualiza os dados da lead — verifica ownership (mesmo padrão dos outros endpoints)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLead(int id, [FromBody] UpdateLeadRequest request)
    {
        var lead = await _db.Leads
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);

        if (lead == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.BusinessName))
            return BadRequest(new { message = "O nome da empresa é obrigatório." });

        lead.BusinessName = request.BusinessName.Trim();
        lead.PhoneNumber = request.PhoneNumber;
        lead.Website = request.Website;
        lead.Address = request.Address;
        lead.Niche = request.Niche;
        lead.City = request.City;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Lead {Id} editada por {UserId}", id, CurrentUserId);

        return Ok(new { message = "Lead actualizada com sucesso.", businessName = lead.BusinessName });
    }

    // Actualiza o status — CORRIGIDO: verifica ownership
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var lead = await _db.Leads
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);

        if (lead == null)
            return NotFound();

        if (!Enum.TryParse<LeadStatus>(request.Status, out var newStatus))
            return BadRequest(new { message = "Status inválido." });

        lead.Status = newStatus;

        if (newStatus >= LeadStatus.MensagemEnviada)
            lead.LastContactedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _webhookQueue.EnqueueAsync(new WebhookJob(
    CurrentUserId, nameof(WebhookEventType.LeadStatusChanged), LeadPublicDto.FromEntity(lead)));

        return Ok(new { message = "Status actualizado.", newStatus = newStatus.ToString() });
    }

    // Adiciona uma nota — CORRIGIDO: verifica ownership
    [HttpPost("{id}/notes")]
    public async Task<IActionResult> AddNote(int id, [FromBody] AddNoteRequest request)
    {
        var lead = await _db.Leads
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);

        if (lead == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "A nota não pode estar vazia." });

        var note = new Note
        {
            LeadId = id,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = note.Id,
            content = note.Content,
            createdAt = note.CreatedAt.ToString("dd/MM/yyyy HH:mm")
        });
    }

    // Apaga uma lead — CORRIGIDO: verifica ownership
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLead(int id)
    {
        var lead = await _db.Leads
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);

        if (lead == null)
            return NotFound();

        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Gera mensagem via IA — CORRIGIDO: verifica ownership
    [HttpPost("{id}/generate-message")]
    public async Task<IActionResult> GenerateMessage(int id, [FromBody] GenerateMessageRequest request)
    {
        var lead = await _db.Leads
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);

        if (lead == null)
            return NotFound();

        try
        {
            var message = await _ai.GenerateOutreachMessageAsync(
                lead.BusinessName, lead.Niche, lead.City,
                lead.PhoneNumber, lead.Website, request.MessageType);

            return Ok(new { message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar mensagem para lead {Id}", id);
            return StatusCode(500, new { message = "Erro ao contactar IA." });
        }
    }
}