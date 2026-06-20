using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

using ToggleMesh.Common.Pagination;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsEndpoint : ToggleEndpoint<GetFlagsRequest, CursorPagedResponse<ProjectFlagDto>>
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

        var (role, envRoles) = await _db.GetProjectRoleAndEnvOverridesAsync(projectId, UserId, ct);

        if (role == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

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

        var query = rawFlagsQuery;

        if (req.Cursor.HasValue)
            query = query.Where(x => x.Id < req.Cursor.Value);

        var totalCount = await rawFlagsQuery.CountAsync(ct);

        var rawFlags = await query
            .OrderByDescending(x => x.Id)
            .Take(req.PageSize + 1)
            .ToListAsync(ct);

        var hasNextPage = rawFlags.Count > req.PageSize;
        if (hasNextPage)
            rawFlags.RemoveAt(rawFlags.Count - 1);

        var nextCursor = rawFlags.Count > 0 
            ? rawFlags.Last().Id 
            : (Guid?)null;

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
                    var effectiveRole = role.Value;
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

        await Send.OkAsync(new CursorPagedResponse<ProjectFlagDto>(flags, totalCount, nextCursor, hasNextPage), ct);
    }
}