using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsEndpoint : ToggleEndpoint<GetFlagsRequest, List<ProjectFlagDto>>
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
        this.RequirePermission(Auth.Models.Permissions.FlagsView);
    }

    public override async Task HandleAsync(GetFlagsRequest req, CancellationToken ct)
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

        var envRoles =
            projectMember.EnvironmentRoles
                .ToDictionary(x =>
                    x.EnvironmentId, x => x.Role);

        var rawFlagsQuery = _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.States)
            .ThenInclude(s => s.Rules)
            .Where(x => x.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(req.Search))
            rawFlagsQuery = rawFlagsQuery.Where(x =>
                EF.Functions.ILike(x.Key, $"%{req.Search}%") ||
                (x.Name != null && EF.Functions.ILike(x.Name, $"%{req.Search}%")) ||
                (x.Description != null && EF.Functions.ILike(x.Description, $"%{req.Search}%")) ||
                x.Tags.Any(t => EF.Functions.ILike(t, $"%{req.Search}%")));

        if (req.Tags.Length > 0)
            rawFlagsQuery = rawFlagsQuery.Where(x =>
                x.Tags.Any(t => req.Tags.Contains(t)));

        var rawFlags = await rawFlagsQuery.ToListAsync(ct);

        var flags = rawFlags.Select(x => new ProjectFlagDto(
                x.Id,
                x.Key,
                x.Name,
                x.Description,
                x.IsClientSideExposed,
                x.CreatedAt,
                x.UpdatedAt,
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
                )),
                x.Tags))
            .ToList();

        await Send.OkAsync(flags, ct);
    }
}