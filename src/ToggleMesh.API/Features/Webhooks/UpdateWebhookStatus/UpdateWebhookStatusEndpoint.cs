using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Webhooks.UpdateWebhookStatus;

public class UpdateWebhookStatusEndpoint : ToggleEndpoint<UpdateWebhookStatusRequest>
{
    private readonly AppDbContext _db;

    public UpdateWebhookStatusEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/projects/{projectId:guid}/webhooks/{webhookId:guid}/status");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.WebhooksCreate);
    }

    public override async Task HandleAsync(UpdateWebhookStatusRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var webhookId = Route<Guid>("webhookId");

        var webhook = await _db.Webhooks
            .FirstOrDefaultAsync(w => 
                w.ProjectId == projectId && w.Id == webhookId, ct);

        if (webhook == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        webhook.Status = req.Status;
        if (req.Status == WebhookStatus.Active)
            webhook.ConsecutiveFailures = 0;

        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(webhook, ct);
    }
}
