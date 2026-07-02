using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Webhooks.GetWebhooks;

public class GetWebhooksEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public GetWebhooksEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId:guid}/webhooks");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var hooks = await _db.Webhooks
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .Select(w => new WebhookDto(
                w.Id, 
                w.ProjectId, 
                w.Name, 
                w.Url, 
                w.Status,
                w.EnvironmentIds, 
                w.Events, 
                w.FlagTags,
                w.ConsecutiveFailures, 
                w.LastTriggeredAt))
            .ToListAsync(ct);
        await Send.OkAsync(hooks, ct);
    }
}