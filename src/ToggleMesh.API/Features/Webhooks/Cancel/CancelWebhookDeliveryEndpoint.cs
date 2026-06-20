using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Extensions;

namespace ToggleMesh.API.Features.Webhooks.Cancel;

public class CancelWebhookDeliveryEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public CancelWebhookDeliveryEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/webhooks/{webhookId:guid}/deliveries/{deliveryId:guid}/cancel");
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

        delivery.Status = WebhookDeliveryStatus.Canceled;
        delivery.CompletedAt = DateTime.UtcNow;
        delivery.NextAttemptAt = null;
        
        await _db.SaveChangesAsync(ct);
        await Send.OkAsync(cancellation: ct);
    }
}
