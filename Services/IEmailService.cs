namespace LeadScoutCRM.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);

    Task<bool> SendSubscriptionReminderAsync(
        string toEmail, string toName,
        string planName, DateTime? expiryDate = null);

    Task<bool> SendUpgradeNudgeAsync(
        string toEmail, string toName,
        int leadCount, int leadLimit);

    Task<bool> SendWelcomeEmailAsync(string toEmail, string toName);

    Task<bool> SendPaymentFailedEmailAsync(string toEmail, string toName, string planName);
}