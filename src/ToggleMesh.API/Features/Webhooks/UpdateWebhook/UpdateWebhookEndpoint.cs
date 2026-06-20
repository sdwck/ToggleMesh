using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Webhooks.UpdateWebhook;

public class UpdateWebhookEndpoint : ToggleEndpoint<UpdateWebhookRequest>
{
    private readonly AppDbContext _db;

    public UpdateWebhookEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/projects/{projectId:guid}/webhooks/{webhookId:guid}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.WebhooksCreate);
    }

    public override async Task HandleAsync(UpdateWebhookRequest req, CancellationToken ct)
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

        webhook.Name = req.Name;
        webhook.Url = req.Url;
        webhook.EnvironmentIds = req.EnvironmentIds;
        webhook.Events = req.Events;

        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(webhook, ct);
    }
}
