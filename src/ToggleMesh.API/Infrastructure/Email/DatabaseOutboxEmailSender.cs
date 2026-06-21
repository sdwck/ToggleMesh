using ToggleMesh.API.Infrastructure.Email.Models;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Infrastructure.Email;

public class DatabaseOutboxEmailSender : IEmailSender
{
    private readonly AppDbContext _db;

    public DatabaseOutboxEmailSender(AppDbContext db)
    {
        _db = db;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new EmailOutboxMessage
        {
            ToEmail = to,
            Subject = subject,
            HtmlBody = htmlBody,
            Status = EmailOutboxStatus.Pending,
            NextAttemptAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            AttemptCount = 0
        };

        _db.EmailOutboxMessages.Add(message);
        await _db.SaveChangesAsync(ct);
    }
}
