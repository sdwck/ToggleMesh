using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

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
        this.RequirePermission(Auth.Models.Permissions.ProjectsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var hooks = await _db.Webhooks
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId).ToListAsync(ct);
        await Send.OkAsync(hooks, ct);
    }
}