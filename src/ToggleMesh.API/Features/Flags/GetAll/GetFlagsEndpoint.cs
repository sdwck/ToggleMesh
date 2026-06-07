using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsEndpoint : ToggleEndpointWithoutRequest<List<GetFlagResponse>>
{
    private readonly AppDbContext _db;

    public GetFlagsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/environments/{environmentId}/flags");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.FlagsView}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");
        var flags = await _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == environmentId)
            .Select(x => new GetFlagResponse(
                x.Key, 
                x.IsEnabled, 
                x.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
                x.RolloutPercentage,
                x.TrueCount,
                x.FalseCount))
            .ToListAsync(ct);
        
        await Send.OkAsync(flags, ct);
    }
}