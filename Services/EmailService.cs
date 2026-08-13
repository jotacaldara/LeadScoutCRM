using System.Net;
using System.Net.Mail;

namespace LeadScoutCRM.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;


    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(
        string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var smtpHost = _config["Email:SmtpHost"];
            var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var username = _config["Email:Username"];
            var password = _config["Email:Password"];
            var fromName = _config["Email:FromName"] ?? "LeadScout CRM";
            var fromEmail = _config["Email:FromEmail"] ?? username;

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Email não configurado. Adiciona Email:SmtpHost e Email:Username ao appsettings.");
                return false;
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(username, password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(mail);
            _logger.LogInformation("Email enviado para {Email}: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email para {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendSubscriptionReminderAsync(
        string toEmail, string toName, string planName, DateTime? expiryDate = null)
    {
        var expiryInfo = expiryDate.HasValue
            ? $"A tua subscrição termina a <strong>{expiryDate.Value:dd/MM/yyyy}</strong>."
            : "A tua subscrição está prestes a terminar.";

        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:'Segoe UI',sans-serif;background:#f1f5f9;margin:0;padding:2rem;">
              <div style="max-width:520px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);">
                <div style="background:#6366f1;padding:2rem;text-align:center;">
                  <h1 style="color:#fff;margin:0;font-size:1.4rem;">⚡ LeadScout CRM</h1>
                </div>
                <div style="padding:2rem;">
                  <h2 style="color:#1e293b;margin-top:0;">Olá, {toName}!</h2>
                  <p style="color:#475569;line-height:1.6;">{expiryInfo}</p>
                  <p style="color:#475569;line-height:1.6;">
                    Renova o teu plano <strong>{planName}</strong> para continuares a ter acesso
                    a todas as funcionalidades do LeadScout CRM e não perderes os teus dados.
                  </p>
                  <div style="background:#f8fafc;border-radius:10px;padding:1.25rem;margin:1.5rem 0;">
                    <p style="margin:0;color:#64748b;font-size:.9rem;">
                      ✅ Leads ilimitadas &nbsp;·&nbsp; ✅ Pipeline Kanban &nbsp;·&nbsp; ✅ IA integrada
                    </p>
                  </div>
                  <div style="text-align:center;margin:2rem 0;">
                    <a href="https://leadscoutcrm.com/Account/Settings"
                       style="background:#6366f1;color:#fff;padding:.85rem 2.5rem;border-radius:10px;text-decoration:none;font-weight:600;font-size:1rem;">
                      Renovar Subscrição
                    </a>
                  </div>
                  <p style="color:#94a3b8;font-size:.8rem;text-align:center;margin-top:2rem;">
                    Se tiveres alguma questão, responde a este email ou contacta-nos.<br/>
                    LeadScout CRM · <a href="https://leadscoutcrm.com/Privacy" style="color:#6366f1;">Privacidade</a>
                  </p>
                </div>
              </div>
            </body>
            </html>
            """;

        return await SendEmailAsync(toEmail, toName,
            $"⚡ Renova o teu plano {planName} — LeadScout CRM", html);
    }

    public async Task<bool> SendUpgradeNudgeAsync(
        string toEmail, string toName, int leadCount, int leadLimit)
    {
        var pct = (int)Math.Round((double)leadCount / leadLimit * 100);

        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:'Segoe UI',sans-serif;background:#f1f5f9;margin:0;padding:2rem;">
              <div style="max-width:520px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);">
                <div style="background:#6366f1;padding:2rem;text-align:center;">
                  <h1 style="color:#fff;margin:0;font-size:1.4rem;">⚡ LeadScout CRM</h1>
                </div>
                <div style="padding:2rem;">
                  <h2 style="color:#1e293b;margin-top:0;">Olá, {toName}! Estás quase no limite 🔥</h2>
                  <p style="color:#475569;line-height:1.6;">
                    Já usaste <strong>{leadCount} de {leadLimit} leads</strong> disponíveis no teu plano Free
                    — estás a <strong>{pct}%</strong> do limite.
                  </p>
                  <p style="color:#475569;line-height:1.6;">
                    Faz upgrade para o plano <strong>Pro por apenas 19€/mês</strong> e desbloqueia leads ilimitadas,
                    exportação CSV e muito mais.
                  </p>
                  <div style="background:#f0fdf4;border-radius:10px;padding:1.25rem;margin:1.5rem 0;border-left:4px solid #22c55e;">
                    <p style="margin:0;color:#15803d;font-weight:600;">Plano Pro — 19€/mês</p>
                    <p style="margin:.5rem 0 0;color:#166534;font-size:.875rem;">
                      ✅ Leads ilimitadas &nbsp;·&nbsp; ✅ Export CSV &nbsp;·&nbsp; ✅ Kanban avançado
                    </p>
                  </div>
                  <div style="text-align:center;margin:2rem 0;">
                    <a href="https://leadscoutcrm.com/Pricing"
                       style="background:#6366f1;color:#fff;padding:.85rem 2.5rem;border-radius:10px;text-decoration:none;font-weight:600;font-size:1rem;">
                      Ver Planos e Fazer Upgrade
                    </a>
                  </div>
                  <p style="color:#94a3b8;font-size:.8rem;text-align:center;margin-top:2rem;">
                    LeadScout CRM · <a href="https://leadscoutcrm.com/Privacy" style="color:#6366f1;">Privacidade</a>
                  </p>
                </div>
              </div>
            </body>
            </html>
            """;

        return await SendEmailAsync(toEmail, toName,
            $"⚠️ Estás a {pct}% do limite de leads — Faz Upgrade", html);
    }

    public async Task<bool> SendWelcomeEmailAsync(string toEmail, string toName)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:'Segoe UI',sans-serif;background:#f1f5f9;margin:0;padding:2rem;">
              <div style="max-width:520px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);">
                <div style="background:#6366f1;padding:2rem;text-align:center;">
                  <h1 style="color:#fff;margin:0;font-size:1.4rem;">⚡ LeadScout CRM</h1>
                </div>
                <div style="padding:2rem;">
                  <h2 style="color:#1e293b;margin-top:0;">Bem-vindo, {toName}! 🎉</h2>
                  <p style="color:#475569;line-height:1.6;">
                    A tua conta no LeadScout CRM está pronta. Começa agora a encontrar leads
                    de qualidade com o Google Places integrado.
                  </p>
                  <div style="background:#f8fafc;border-radius:10px;padding:1.25rem;margin:1.5rem 0;">
                    <p style="margin:0 0 .5rem;color:#1e293b;font-weight:600;">Como começar:</p>
                    <p style="margin:.25rem 0;color:#64748b;font-size:.875rem;">1️⃣ Vai a <strong>Nova Pesquisa</strong> e pesquisa o teu nicho</p>
                    <p style="margin:.25rem 0;color:#64748b;font-size:.875rem;">2️⃣ Guarda os negócios que te interessam</p>
                    <p style="margin:.25rem 0;color:#64748b;font-size:.875rem;">3️⃣ Usa a IA para gerar mensagens de outreach</p>
                  </div>
                  <div style="text-align:center;margin:2rem 0;">
                    <a href="https://leadscoutcrm.com/Leads/Search"
                       style="background:#6366f1;color:#fff;padding:.85rem 2.5rem;border-radius:10px;text-decoration:none;font-weight:600;font-size:1rem;">
                      Começar a Pesquisar
                    </a>
                  </div>
                </div>
              </div>
            </body>
            </html>
            """;

        return await SendEmailAsync(toEmail, toName,
            "🚀 Bem-vindo ao LeadScout CRM!", html);
    }

    public async Task<bool> SendPaymentFailedEmailAsync(string toEmail, string toName, string planName)
    {
        var html = $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:'Segoe UI',sans-serif;background:#f1f5f9;margin:0;padding:2rem;">
          <div style="max-width:520px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);">
            <div style="background:#b91c1c;padding:2rem;text-align:center;">
              <h1 style="color:#fff;margin:0;font-size:1.4rem;">⚠️ LeadScout CRM</h1>
            </div>
            <div style="padding:2rem;">
              <h2 style="color:#1e293b;margin-top:0;">Olá, {toName}</h2>
              <p style="color:#475569;line-height:1.6;">
                Não conseguimos processar o pagamento da tua subscrição <strong>{planName}</strong>.
                Isto acontece normalmente por um cartão expirado, saldo insuficiente ou o banco
                ter bloqueado a transacção.
              </p>
              <p style="color:#475569;line-height:1.6;">
                Actualiza o teu método de pagamento para não perderes o acesso às funcionalidades premium.
              </p>
              <div style="text-align:center;margin:2rem 0;">
                <a href="https://leadscoutcrm.com/Account/Settings"
                   style="background:#b91c1c;color:#fff;padding:.85rem 2.5rem;border-radius:10px;text-decoration:none;font-weight:600;font-size:1rem;">
                  Actualizar Pagamento
                </a>
              </div>
              <p style="color:#94a3b8;font-size:.8rem;text-align:center;margin-top:2rem;">
                LeadScout CRM · <a href="https://leadscoutcrm.com/Privacy" style="color:#6366f1;">Privacidade</a>
              </p>
            </div>
          </div>
        </body>
        </html>
        """;

        return await SendEmailAsync(toEmail, toName,
            "⚠️ Falha no pagamento da tua subscrição — LeadScout CRM", html);
    }
}