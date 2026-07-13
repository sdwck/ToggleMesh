using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.Common.Pagination;
using ToggleMesh.API.Features.Flags.Domain;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsEndpoint : ToggleEndpoint<GetFlagsRequest, PagedResponse<ProjectFlagDto>>
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
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(GetFlagsRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

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

        var query = _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.Variations)
            .Include(x => x.States.Where(s => !s.Environment.IsDeleted))
                .ThenInclude(s => s.Rules)
            .AsSplitQuery()
            .Where(x => x.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(req.Search))
            query = query.Where(x =>
                EF.Functions.ILike(x.Key, $"%{req.Search}%") ||
                (x.Name != null && EF.Functions.ILike(x.Name, $"%{req.Search}%")) ||
                (x.Description != null && EF.Functions.ILike(x.Description, $"%{req.Search}%")) ||
                x.Tags.Any(t => EF.Functions.ILike(t, $"%{req.Search}%")));

        if (req.Tags.Length > 0)
            query = query.Where(x =>
                x.Tags.Any(t => req.Tags.Contains(t)));

        var totalCount = await query.CountAsync(ct);

        var rawFlags = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync(ct);

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
                    return effectiveRole != ProjectRole.None;
                }).Select(s => new FlagEnvironmentStateDto(
                    s.EnvironmentId,
                    s.IsEnabled,
                    s.FallthroughRollout.Count > 0,
                    0L,
                    0L,
                    s.Rules.Count,
                    s.IsMabEnabled,
                    s.MabGoalEvent,
                    s.IsExperimentActive
                )),
                x.Tags,
                (int)x.Type,
                x.Variations.OrderBy(v => v.Sequence).Select(v => new VariationDto(v.Id, v.Value))
            ))
            .ToList();

        await Send.OkAsync(new PagedResponse<ProjectFlagDto>(
            flags, totalCount, req.Page, req.PageSize), ct);
    }
}
