using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

/// <summary>
/// Отправка писем:
/// - если настроен SMTP в appsettings.json (секция Smtp) — отправляем реально;
/// - иначе пишем в лог/консоль (удобно для разработки).
/// </summary>
public sealed class ConfigurableEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<ConfigurableEmailSender> _logger;

    public ConfigurableEmailSender(IConfiguration cfg, ILogger<ConfigurableEmailSender> logger)
    {
        _cfg = cfg;
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        => SendAsync(email, "OpinionHub: подтверждение почты", confirmationLink);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        => SendAsync(email, "OpinionHub: сброс пароля", resetLink);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
        => SendAsync(email, "OpinionHub: код сброса пароля", resetCode);

    private async Task SendAsync(string to, string subject, string body)
    {
        var host = _cfg["Smtp:Host"];
        var fromEmail = _cfg["Smtp:FromEmail"];
        var fromName = _cfg["Smtp:FromName"] ?? "OpinionHub";
        var user = _cfg["Smtp:User"];
        var pass = _cfg["Smtp:Password"];

        // Если SMTP не настроен — просто печатаем в лог.
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("[DEV EMAIL]\nTo: {To}\nSubject: {Subject}\n{Body}", to, subject, body);
            return;
        }

        var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
        var enableSsl = bool.TryParse(_cfg["Smtp:EnableSsl"], out var ssl) ? ssl : true;

        if (string.IsNullOrWhiteSpace(fromEmail))
            fromEmail = user ?? "no-reply@localhost";

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, pass);

        await client.SendMailAsync(message);
    }
}
