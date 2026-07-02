using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Webhooks.Cancel;

public class CancelWebhookDeliveryEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public CancelWebhookDeliveryEndpoint(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/webhooks/{webhookId:guid}/deliveries/{deliveryId:guid}/cancel");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.WebhooksCreate);
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
        delivery.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        delivery.NextAttemptAt = null;
        
        await _db.SaveChangesAsync(ct);
        await Send.OkAsync(cancellation: ct);
    }
}
