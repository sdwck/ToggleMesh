namespace ToggleMesh.API.Infrastructure.Email;

public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
