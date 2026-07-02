using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Email.Models;

namespace ToggleMesh.API.Infrastructure.Email;

public class DatabaseOutboxEmailSender : IEmailSender
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public DatabaseOutboxEmailSender(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var message = new EmailOutboxMessage
            {
                ToEmail = to,
                Subject = subject,
                HtmlBody = htmlBody,
                Status = EmailOutboxStatus.Pending,
                NextAttemptAt = now,
                CreatedAt = now,
                AttemptCount = 0
            };

        _db.EmailOutboxMessages.Add(message);
        await _db.SaveChangesAsync(ct);
    }
}
