using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


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
        this.RequirePermission(AuthModels.Permissions.WebhooksCreate);
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

        if (!await SsrfValidator.IsSafeUrlAsync(req.Url, ct))
            ThrowError("The provided URL is invalid or points to a restricted internal network address.", 400);

        webhook.Name = req.Name;
        webhook.Url = req.Url;
        webhook.EnvironmentIds = req.EnvironmentIds;
        webhook.Events = req.Events;
        webhook.FlagTags = req.FlagTags;

        await _db.SaveChangesAsync(ct);

        var dto = new WebhookDto(
            webhook.Id, webhook.ProjectId, webhook.Name, webhook.Url, webhook.Status,
            webhook.EnvironmentIds, webhook.Events, webhook.FlagTags,
            webhook.ConsecutiveFailures, webhook.LastTriggeredAt);

        await Send.OkAsync(dto, ct);
    }
}
