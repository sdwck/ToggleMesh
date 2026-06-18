using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Projects.GetKeys;

public class GetKeysEndpoint : ToggleEndpointWithoutRequest<List<GetKeysResponse>>
{
    private readonly AppDbContext _db;

    public GetKeysEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId:guid}/environments/{environmentId:guid}/keys");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.EnvironmentsKeysRotate);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");

        var env = await _db.Environments
            .FirstOrDefaultAsync(e => e.Id == environmentId && e.ProjectId == projectId, ct);

        if (env == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var keys = await _db.EnvironmentKeys
            .AsNoTracking()
            .Where(k => k.EnvironmentId == environmentId)
            .OrderByDescending(k => k.CreatedOn)
            .Select(k => new GetKeysResponse
            {
                Id = k.Id,
                Name = k.Name,
                KeyType = k.KeyType,
                KeyPreview = k.KeyPreview,
                CreatedOn = k.CreatedOn,
                ExpireOn = k.ExpireOn,
                LastUsedAt = k.LastUsedAt
            })
            .ToListAsync(ct);

        await Send.OkAsync(keys, ct);
    }
}
