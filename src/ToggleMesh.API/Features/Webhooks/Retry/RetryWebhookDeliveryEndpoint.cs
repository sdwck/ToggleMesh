using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Webhooks.Retry;

public class RetryWebhookDeliveryEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public RetryWebhookDeliveryEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/webhooks/{webhookId:guid}/deliveries/{deliveryId:guid}/retry");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.WebhooksCreate);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var webhookId = Route<Guid>("webhookId");
        var deliveryId = Route<Guid>("deliveryId");

        var webhook = await _db.Webhooks
            .FirstOrDefaultAsync(w => 
                w.ProjectId == projectId && w.Id == webhookId, ct);

        if (webhook == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var delivery = await _db.WebhookDeliveries
            .FirstOrDefaultAsync(d => 
                d.WebhookId == webhookId && d.Id == deliveryId, ct);

        if (delivery == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        delivery.Status = WebhookDeliveryStatus.Pending;
        delivery.NextAttemptAt = DateTime.UtcNow;
        delivery.AttemptCount = 0;
        
        await _db.SaveChangesAsync(ct);
        await Send.OkAsync(cancellation: ct);
    }
}
