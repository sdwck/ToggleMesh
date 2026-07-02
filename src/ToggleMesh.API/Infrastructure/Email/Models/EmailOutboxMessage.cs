using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Infrastructure.Email.Models;

public class EmailOutboxMessage : Entity
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;

    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;
    public string? ErrorMessage { get; set; }

    public int AttemptCount { get; set; } = 0;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
