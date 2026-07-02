using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Webhooks.Domain;

public class WebhookDelivery : Entity
{
    public Guid WebhookId { get; set; }
    public Webhook Webhook { get; set; } = null!;
    
    public string EventName { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;
    public int? StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    
    public int AttemptCount { get; set; } = 0;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
