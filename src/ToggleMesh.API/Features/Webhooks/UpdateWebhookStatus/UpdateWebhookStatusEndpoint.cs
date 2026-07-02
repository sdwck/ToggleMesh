using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


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
        this.RequirePermission(AuthModels.Permissions.WebhooksCreate);
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

        var dto = new WebhookDto(
            webhook.Id, 
            webhook.ProjectId, 
            webhook.Name, 
            webhook.Url, 
            webhook.Status,
            webhook.EnvironmentIds, 
            webhook.Events, 
            webhook.FlagTags,
            webhook.ConsecutiveFailures, 
            webhook.LastTriggeredAt);

        await Send.OkAsync(dto, ct);
    }
}
