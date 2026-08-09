using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeadScoutCRM.Services.Webhooks;

// Corre continuamente em segundo plano enquanto a app está no ar. Consome
// a fila e entrega cada evento a todas as subscrições ativas do respetivo
// utilizador. Resolve o DbContext num scope próprio a cada job, porque o
// DbContext não é thread-safe nem partilhável entre pedidos.
public class WebhookDispatcherBackgroundService : BackgroundService
{
    private readonly WebhookDispatchQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookDispatcherBackgroundService> _logger;

    public WebhookDispatcherBackgroundService(
        WebhookDispatchQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookDispatcherBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar webhook para {UserId}", job.UserId);
            }
        }
    }

    private async Task ProcessJobAsync(WebhookJob job, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var delivery = scope.ServiceProvider.GetRequiredService<WebhookDeliveryService>();

        Enum.TryParse<WebhookEventType>(job.EventType, out var jobEventType);

        var subscriptions = await db.WebhookSubscriptions
            .Where(w => w.UserId == job.UserId && w.IsActive)
            .Where(w => w.EventType == WebhookEventType.All || w.EventType == jobEventType)
            .ToListAsync(ct);

        if (subscriptions.Count == 0) return;

        var eventKey = ToEventKey(job.EventType);

        foreach (var subscription in subscriptions)
        {
            var log = await delivery.DeliverAsync(subscription, eventKey, job.Payload, ct);
            db.WebhookDeliveryLogs.Add(log);
        }

        await db.SaveChangesAsync(ct);
    }

    private static string ToEventKey(string eventType) => eventType switch
    {
        nameof(WebhookEventType.LeadCreated) => "lead.created",
        nameof(WebhookEventType.LeadStatusChanged) => "lead.status_changed",
        _ => eventType
    };
}