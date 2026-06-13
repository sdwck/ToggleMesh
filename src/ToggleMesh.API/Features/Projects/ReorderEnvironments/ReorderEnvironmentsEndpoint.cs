using Microsoft.EntityFrameworkCore;
using FastEndpoints;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Projects.ReorderEnvironments;

public class ReorderEnvironmentsEndpoint : ToggleEndpoint<ReorderEnvironmentsRequest>
{
    private readonly AppDbContext _db;
    public ReorderEnvironmentsEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/environments/reorder");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.EnvironmentsEdit);
    }

    public override async Task HandleAsync(ReorderEnvironmentsRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var environments = await _db.Environments
            .Where(e => e.ProjectId == projectId)
            .ToListAsync(ct);

        for (var i = 0; i < req.EnvironmentIds.Count; i++)
        {
            var envId = req.EnvironmentIds[i];
            var env = environments.FirstOrDefault(e => e.Id == envId);
            env?.SortOrder = i;
        }

        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}