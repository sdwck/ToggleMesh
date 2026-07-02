using ToggleMesh.API.Infrastructure.Email;

namespace ToggleMesh.IntegrationTests.Infrastructure;

public class FakeEmailSender : IEmailSender
{
    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
