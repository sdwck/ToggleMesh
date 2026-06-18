using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Projects.DeleteProject;

public class DeleteProjectEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly ICacheInvalidator _cacheInvalidator;

    public DeleteProjectEndpoint(AppDbContext db, ICacheInvalidator cacheInvalidator)
    {
        _db = db;
        _cacheInvalidator = cacheInvalidator;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId:guid}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.ProjectsDelete);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var project = await _db.Projects
            .Include(p => p.Environments)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        var environmentIds = project.Environments.Select(e => e.Id).ToList();

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);

        foreach (var envId in environmentIds)
            await _cacheInvalidator.InvalidateEnvironmentCacheAsync(envId);

        await Send.NoContentAsync(ct);
    }
}