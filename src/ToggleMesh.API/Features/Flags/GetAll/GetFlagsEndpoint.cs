using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsEndpoint : ToggleEndpointWithoutRequest<List<ProjectFlagDto>>
{
    private readonly AppDbContext _db;

    public GetFlagsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/flags");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.FlagsView}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var projectMember = await _db.ProjectMembers
            .AsNoTracking()
            .Include(pm => pm.EnvironmentRoles)
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == UserId, ct);

        if (projectMember == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var envRoles = projectMember.EnvironmentRoles.ToDictionary(x => x.EnvironmentId, x => x.Role);

        var rawFlags = await _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.States)
                .ThenInclude(s => s.Rules)
            .Where(x => x.ProjectId == projectId)
            .ToListAsync(ct);

        var flags = rawFlags.Select(x => new ProjectFlagDto(
            x.Id,
            x.Key,
            x.Name,
            x.Description,
            x.IsClientSideExposed,
            x.CreatedAt,
            x.States.Where(s => 
            {
                var effectiveRole = projectMember.Role;
                if (envRoles.TryGetValue(s.EnvironmentId, out var overrideRole))
                    effectiveRole = overrideRole;
                return effectiveRole != Projects.ProjectRole.None;
            }).Select(s => new FlagEnvironmentStateDto(
                s.EnvironmentId,
                s.IsEnabled,
                s.RolloutPercentage,
                s.TrueCount,
                s.FalseCount,
                s.Rules.Count
            ))
        )).ToList();

        await Send.OkAsync(flags, ct);
    }
}