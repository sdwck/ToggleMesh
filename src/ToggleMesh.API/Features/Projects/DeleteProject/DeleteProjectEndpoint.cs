using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


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
        this.RequirePermission(AuthModels.Permissions.ProjectsDelete);
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

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try 
        {
            var keysToDelete = await _db.EnvironmentKeys
                .Where(k => environmentIds.Contains(k.EnvironmentId))
                .ToListAsync(ct);
            _db.EnvironmentKeys.RemoveRange(keysToDelete);

            await _db.Webhooks
                .Where(w => w.ProjectId == projectId)
                .ExecuteDeleteAsync(ct);

            _db.Projects.Remove(project);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        } 
        catch 
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        foreach (var envId in environmentIds)
            await _cacheInvalidator.InvalidateEnvironmentCacheAsync(envId);

        await Send.NoContentAsync(ct);
    }
}