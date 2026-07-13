using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.Common.Pagination;

namespace ToggleMesh.API.Features.Projects.GetProjects;

public class GetProjectsEndpoint : ToggleEndpoint<GetProjectsRequest, PagedResponse<ProjectListDto>>
{
    private readonly AppDbContext _db;

    public GetProjectsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects");
        Version(1);
    }

    public override async Task HandleAsync(GetProjectsRequest req, CancellationToken ct)
    {
        var query = _db.Projects.AsNoTracking();

        if (req.OrganizationId.HasValue)
        {
            query = query.Where(p => p.OrganizationId == req.OrganizationId.Value);

            var orgMember = await _db.OrganizationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(om => om.OrganizationId == req.OrganizationId.Value && om.UserId == UserId, ct);

            if (orgMember == null)
            {
                await Send.OkAsync(new PagedResponse<ProjectListDto>([], 0, req.Page, req.PageSize), ct);
                return;
            }

            if (orgMember.Role != OrganizationRole.Admin)
                query = query.Where(p => p.Members.Any(m => m.UserId == UserId));
        }
        else
            query = query.Where(p => 
                p.Members.Any(m => m.UserId == UserId) || 
                _db.OrganizationMembers.Any(om => om.OrganizationId == p.OrganizationId && om.UserId == UserId && om.Role == OrganizationRole.Admin)
            );

        var totalCount = await query.CountAsync(ct);

        var projects = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(p => new ProjectListDto
            {
                Id = p.Id,
                Name = p.Name,
                UserRole = p.Organization.Members.Any(om => om.UserId == UserId && om.Role == OrganizationRole.Admin)
                    ? ProjectRole.Owner
                    : p.Members.Where(m => m.UserId == UserId).Select(m => (ProjectRole?)m.Role).FirstOrDefault() ?? ProjectRole.None,
            })
            .ToListAsync(ct);

        var projectIds = projects.Select(p => p.Id).ToList();

        if (projectIds.Count != 0)
        {
            var memberEnvRoles = await _db.MemberEnvironmentRoles
                .Include(r => r.ProjectMember)
                .Where(r => projectIds.Contains(r.ProjectMember.ProjectId) && r.ProjectMember.UserId == UserId)
                .ToDictionaryAsync(r => r.EnvironmentId, r => r.Role, ct);

            var envs = await _db.Environments
                .Where(e => projectIds.Contains(e.ProjectId))
                .Select(e => new { e.Id, e.ProjectId })
                .ToListAsync(ct);

            var accessibleEnvIds = new HashSet<Guid>();
            foreach (var env in envs)
            {
                var p = projects.First(x => x.Id == env.ProjectId);
                var baseRole = p.UserRole;
                
                var role = memberEnvRoles.TryGetValue(env.Id, out var specificRole) ? specificRole : baseRole;
                if (role != ProjectRole.None)
                    accessibleEnvIds.Add(env.Id);
            }

            var flagStats = await _db.FlagEnvironmentStates
                .AsNoTracking()
                .Where(s => projectIds.Contains(s.FeatureFlag.ProjectId) && !s.FeatureFlag.IsDeleted && accessibleEnvIds.Contains(s.EnvironmentId))
                .GroupBy(s => s.FeatureFlag.ProjectId)
                .Select(g => new {
                    ProjectId = g.Key,
                    TotalFlags = g.Select(x => x.FeatureFlagId).Distinct().Count(),
                    ActiveFlags = g.Where(x => x.IsEnabled).Select(x => x.FeatureFlagId).Distinct().Count(),
                    RunningTests = g.Where(x => x.IsExperimentActive).Select(x => x.FeatureFlagId).Distinct().Count(),
                    MabActiveFlags = g.Where(x => x.IsMabEnabled).Select(x => x.FeatureFlagId).Distinct().Count()
                }).ToDictionaryAsync(x => x.ProjectId, ct);

            var topExperiments = await _db.FlagEnvironmentStates
                .AsNoTracking()
                .Where(s => projectIds.Contains(s.FeatureFlag.ProjectId) && s.IsExperimentActive && !s.FeatureFlag.IsDeleted && accessibleEnvIds.Contains(s.EnvironmentId))
                .Select(s => new { s.FeatureFlag.ProjectId, s.FeatureFlag.Key })
                .ToListAsync(ct);
            var topExpByProject = topExperiments
                .GroupBy(x => x.ProjectId)
                .ToDictionary(g => g.Key, g => g.First().Key);

            var traffic = await _db.FlagMetricBuckets
                .AsNoTracking()
                .Where(b => b.TimestampBucket >= DateTimeOffset.UtcNow.AddDays(-1) && projectIds.Contains(b.Environment.ProjectId) && accessibleEnvIds.Contains(b.EnvironmentId))
                .GroupBy(b => b.Environment.ProjectId)
                .Select(g => new {
                    ProjectId = g.Key,
                    TotalEvals = g.Sum(x => x.Count)
                }).ToDictionaryAsync(x => x.ProjectId, ct);
                
            var webhookStats = new Dictionary<Guid, int>();
            var adminProjectIds = projects.Where(p => p.UserRole == ProjectRole.Owner || p.UserRole == ProjectRole.Admin).Select(p => p.Id).ToList();
            if (adminProjectIds.Any())
            {
                webhookStats = await _db.Webhooks
                    .AsNoTracking()
                    .Where(w => adminProjectIds.Contains(w.ProjectId) && (w.Status == WebhookStatus.Failing || w.Status == WebhookStatus.DisabledBySystem))
                    .GroupBy(w => w.ProjectId)
                    .Select(g => new {
                        ProjectId = g.Key,
                        FailingWebhooks = g.Count()
                    }).ToDictionaryAsync(x => x.ProjectId, x => x.FailingWebhooks, ct);
            }

            foreach (var p in projects)
            {
                if (flagStats.TryGetValue(p.Id, out var stat))
                {
                    p.TotalFlags = stat.TotalFlags;
                    p.ActiveFlags = stat.ActiveFlags;
                    p.RunningExperiments = stat.RunningTests;
                    p.MabActiveFlagsCount = stat.MabActiveFlags;
                }
                
                if (topExpByProject.TryGetValue(p.Id, out var flagKey))
                    p.TopExperimentFlagKey = flagKey;
                
                if (traffic.TryGetValue(p.Id, out var t))
                    p.Evaluations24H = t.TotalEvals;
                
                if (webhookStats.TryGetValue(p.Id, out var whCount))
                    p.FailingWebhooksCount = whCount;
            }
        }

        await Send.OkAsync(new PagedResponse<ProjectListDto>(projects, totalCount, req.Page, req.PageSize), ct);
    }
}
