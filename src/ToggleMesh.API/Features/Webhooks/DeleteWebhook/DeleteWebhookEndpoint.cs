using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Webhooks.DeleteWebhook;

public class DeleteWebhookEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public DeleteWebhookEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId:guid}/webhooks/{id:guid}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var id = Route<Guid>("id");

        var hook = await _db.Webhooks
            .FirstOrDefaultAsync(w => 
                w.Id == id && 
                w.ProjectId == projectId, ct);
        if (hook != null)
        {
            _db.Webhooks.Remove(hook);
            await _db.SaveChangesAsync(ct);
        }
        await Send.NoContentAsync(ct);
    }
}