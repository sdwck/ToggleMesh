using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Flags.GetTags;

public class GetTagsEndpoint : ToggleEndpointWithoutRequest<List<string>>
{
    private readonly AppDbContext _db;

    public GetTagsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId:guid}/tags");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.FlagsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var tags = await _db.FeatureFlags
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .SelectMany(x => x.Tags)
            .Distinct()
            .ToListAsync(ct);

        await Send.OkAsync(tags, ct);
    }
}
