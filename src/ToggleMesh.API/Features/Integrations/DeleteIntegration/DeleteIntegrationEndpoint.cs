using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Extensions;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Integrations.DeleteIntegration;

public class DeleteIntegrationEndpoint : Endpoint<DeleteIntegrationRequest>
{
    private readonly AppDbContext _db;

    public DeleteIntegrationEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId}/integrations/{id}");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(DeleteIntegrationRequest req, CancellationToken ct)
    {
        var deleted = await _db.Integrations
            .Where(i => i.Id == req.Id && i.ProjectId == req.ProjectId)
            .ExecuteDeleteAsync(ct);

        if (deleted == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
