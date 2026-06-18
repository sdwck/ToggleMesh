using Microsoft.EntityFrameworkCore;
using FastEndpoints;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Projects.UpdateProject;

public class UpdateProjectEndpoint : ToggleEndpoint<UpdateProjectRequest>
{
    private readonly AppDbContext _db;

    public UpdateProjectEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/projects/{projectId:guid}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(UpdateProjectRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        project.Name = req.Name.Trim();
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}