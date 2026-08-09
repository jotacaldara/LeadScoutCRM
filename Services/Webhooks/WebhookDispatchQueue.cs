using System.Threading.Channels;

namespace LeadScoutCRM.Services.Webhooks;

// Um evento à espera de ser entregue: quem gerou (UserId), o quê (EventType)
// e os dados (Payload). O Payload é object porque reaproveita o LeadPublicDto
// já existente sem criar acoplamento entre a fila e o modelo de leads.
public record WebhookJob(string UserId, string EventType, object Payload);

// Fila em memória entre quem gera o evento (controllers) e quem o entrega
// (BackgroundService). Isto evita que criar uma lead fique à espera de um
// endpoint externo lento — o pedido ao utilizador responde de imediato,
// a entrega acontece em segundo plano.
public class WebhookDispatchQueue
{
    private readonly Channel<WebhookJob> _channel = Channel.CreateUnbounded<WebhookJob>();

    public ChannelReader<WebhookJob> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(WebhookJob job) => _channel.Writer.WriteAsync(job);
}