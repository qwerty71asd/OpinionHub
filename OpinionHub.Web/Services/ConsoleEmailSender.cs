using Microsoft.AspNetCore.Identity;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public class ConsoleEmailSender : IEmailSender<ApplicationUser>
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        _logger.LogInformation("EMAIL CONFIRMATION for {Email}: {Link}", email, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        _logger.LogInformation("PASSWORD RESET CODE for {Email}: {Code}", email, resetCode);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        _logger.LogInformation("PASSWORD RESET LINK for {Email}: {Link}", email, resetLink);
        return Task.CompletedTask;
    }
}
