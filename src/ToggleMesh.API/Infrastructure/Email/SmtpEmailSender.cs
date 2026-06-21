using System.Net;
using System.Net.Mail;

namespace ToggleMesh.API.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly SmtpClient _smtpClient;
    private readonly string _fromEmail;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var host = _configuration["Email:Smtp:Host"];
        var port = int.TryParse(_configuration["Email:Smtp:Port"], out var p) ? p : 587;
        var username = _configuration["Email:Smtp:Username"];
        var password = _configuration["Email:Smtp:Password"];
        var enableSsl = !bool.TryParse(_configuration["Email:Smtp:EnableSsl"], out var s) || s;
        _fromEmail = _configuration["Email:Smtp:FromEmail"] ?? "noreply@togglemesh.com";

        _smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password)
        };
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_configuration["Email:Smtp:Host"]))
        {
            _logger.LogWarning("SMTP Host is not configured. Email to {To} with subject '{Subject}' was not sent.", to, subject);
            _logger.LogInformation("Email Body: {Body}", htmlBody);
            return;
        }

        try
        {
            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            await _smtpClient.SendMailAsync(message, ct);
            _logger.LogInformation("Successfully sent email to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
        }
    }

    public void Dispose()
    {
        _smtpClient.Dispose();
    }
}
