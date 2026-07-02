using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Analytics.GetProjectHistoricalExperiments;

public class GetProjectHistoricalExperimentsEndpoint : ToggleEndpointWithoutRequest<List<ProjectHistoricalExperimentDto>>
{
    private readonly AppDbContext _db;

    public GetProjectHistoricalExperimentsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId:guid}/experiments/historical");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var isOrgAdmin = await _db.OrganizationMembers
            .AnyAsync(om => om.OrganizationId == project.OrganizationId && om.UserId == UserId && om.Role == OrganizationRole.Admin, ct);

        var projectMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == UserId, ct);

        var baseRole = isOrgAdmin ? ProjectRole.Owner : projectMember?.Role ?? ProjectRole.None;

        var memberEnvRoles = await _db.MemberEnvironmentRoles
            .Where(r => r.ProjectMemberId == (projectMember != null ? projectMember.Id : Guid.Empty))
            .ToDictionaryAsync(r => r.EnvironmentId, r => r.Role, ct);

        var envs = await _db.Environments
            .Where(x => x.ProjectId == projectId)
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var envIds = envs.Keys.Where(envId => {
            var role = memberEnvRoles.TryGetValue(envId, out var specificRole) ? specificRole : baseRole;
            return role != ProjectRole.None;
        }).ToList();

        var iterations = await _db.ExperimentIterations
            .Where(x => envIds.Contains(x.EnvironmentId))
            .OrderByDescending(x => x.EndedAt)
            .Take(100)
            .ToListAsync(ct);

        var dtos = iterations.Select(x => new ProjectHistoricalExperimentDto
        {
            Id = x.Id,
            EnvironmentId = x.EnvironmentId,
            EnvironmentName = envs.TryGetValue(x.EnvironmentId, out var name) ? name : "Unknown",
            FlagKey = x.FlagKey,
            StartedAt = x.StartedAt,
            EndedAt = x.EndedAt,
            FinalMetricsSnapshot = x.FinalMetricsSnapshot,
            FlagConfigSnapshot = x.FlagConfigSnapshot
        }).ToList();

        await Send.OkAsync(dtos, ct);
    }
}
