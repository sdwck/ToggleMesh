using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.GetStats;

public class GetFlagStatsEndpoint : ToggleEndpointWithoutRequest<List<FlagEnvironmentStatsDto>>
{
    private readonly AppDbContext _db;

    public GetFlagStatsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/flags/{flagKey}/stats");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var flagKey = Route<string>("flagKey");

        var (role, envRoles) = await new GetProjectRoleCommand 
        { 
            ProjectId = projectId, 
            UserId = UserId 
        }.ExecuteAsync(ct);
        if (role == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var projectEnvIds = await _db.Environments
            .Where(e => e.ProjectId == projectId && !e.IsDeleted)
            .Select(e => e.Id)
            .ToListAsync(ct);

        var rawBuckets = await _db.FlagMetricBuckets
            .AsNoTracking()
            .Where(x => x.FlagKey == flagKey && projectEnvIds.Contains(x.EnvironmentId))
            .ToListAsync(ct);

        var buckets = rawBuckets
            .GroupBy(x => x.EnvironmentId)
            .Select(g => new FlagEnvironmentStatsDto(
                g.Key,
                g.GroupBy(b => b.VariationId).ToDictionary(v => v.Key, v => v.Sum(b => b.Count))
            ))
            .ToList();

        await Send.OkAsync(buckets, ct);
    }
}
