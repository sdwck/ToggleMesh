using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Projects.GetDashboard;

public class GetProjectDashboardEndpoint : ToggleEndpointWithoutRequest<ProjectDashboardDto>
{
    private readonly AppDbContext _db;
    private readonly BayesianMathService _math;

    public GetProjectDashboardEndpoint(AppDbContext db, BayesianMathService math)
    {
        _db = db;
        _math = math;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/dashboard");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Query<Guid?>("environmentId", isRequired: false);

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

        var baseRole = isOrgAdmin ? ProjectRole.Owner : (projectMember?.Role ?? ProjectRole.None);

        var memberEnvRoles = await _db.MemberEnvironmentRoles
            .Where(r => r.ProjectMemberId == (projectMember != null ? projectMember.Id : Guid.Empty))
            .ToDictionaryAsync(r => r.EnvironmentId, r => r.Role, ct);

        var allProjectEnvs = await _db.Environments.Where(e => e.ProjectId == projectId).Select(e => e.Id).ToListAsync(ct);
        var accessibleEnvIds = allProjectEnvs.Where(envId => {
            var role = memberEnvRoles.TryGetValue(envId, out var specificRole) ? specificRole : baseRole;
            return role != ProjectRole.None;
        }).ToList();

        var activeFlagsQuery = _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
            .Where(x => x.FeatureFlag.ProjectId == projectId && x.IsEnabled && accessibleEnvIds.Contains(x.EnvironmentId));
            
                if (environmentId.HasValue && accessibleEnvIds.Contains(environmentId.Value))
            activeFlagsQuery = activeFlagsQuery.Where(x => x.EnvironmentId == environmentId.Value);
        else if (environmentId.HasValue)
            activeFlagsQuery = activeFlagsQuery.Where(x => false);

        var activeFlagsCount = await activeFlagsQuery
            .Select(x => x.FeatureFlag.Id)
            .Distinct()
            .CountAsync(ct);

        var mabActiveFlagsQuery = _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
            .Where(x => x.FeatureFlag.ProjectId == projectId && x.IsMabEnabled && accessibleEnvIds.Contains(x.EnvironmentId));

        if (environmentId.HasValue && accessibleEnvIds.Contains(environmentId.Value))
            mabActiveFlagsQuery = mabActiveFlagsQuery.Where(x => x.EnvironmentId == environmentId.Value);
        else if (environmentId.HasValue)
            mabActiveFlagsQuery = mabActiveFlagsQuery.Where(x => false);

        var mabActiveFlagsCount = await mabActiveFlagsQuery
            .Select(x => x.FeatureFlag.Id)
            .Distinct()
            .CountAsync(ct);

        var environmentsCount = accessibleEnvIds.Count;

        long? failingWebhooksCount = null;
        if (baseRole == ProjectRole.Owner || baseRole == ProjectRole.Admin)
        {
            failingWebhooksCount = await _db.Webhooks
                .Where(w => w.ProjectId == projectId)
                .SelectMany(w => _db.WebhookDeliveries.Where(d => d.WebhookId == w.Id && d.Status == WebhookDeliveryStatus.Failed && d.CreatedAt >= DateTime.UtcNow.AddHours(-24)))
                .Select(d => d.WebhookId)
                .Distinct()
                .CountAsync(ct);
        }

        var environmentIdsQuery = _db.Environments.Where(e => e.ProjectId == projectId && accessibleEnvIds.Contains(e.Id));
        if (environmentId.HasValue)
        {
            environmentIdsQuery = environmentIdsQuery.Where(e => e.Id == environmentId.Value);
        }
        var environmentIds = await environmentIdsQuery.Select(e => e.Id).ToListAsync(ct);

        var cutoff = DateTime.UtcNow.AddHours(-24);
        var evalQuery = await _db.FlagMetricBuckets
            .Where(b => environmentIds.Contains(b.EnvironmentId) && b.TimestampBucket >= cutoff)
            .GroupBy(b => new { b.TimestampBucket.Year, b.TimestampBucket.Month, b.TimestampBucket.Day, b.TimestampBucket.Hour })
            .Select(g => new { 
                Time = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0, DateTimeKind.Utc), 
                Count = g.Sum(b => b.TrueCount + b.FalseCount) 
            })
            .ToListAsync(ct);

        var evalPoints = evalQuery
            .OrderBy(x => x.Time)
            .Select(x => new DashboardEvaluationPointDto(x.Time, x.Count))
            .ToList();

        var metrics = await _db.ExperimentMetrics
            .Where(m => environmentIds.Contains(m.EnvironmentId) && m.TotalExposures > 50)
            .GroupBy(x => new { x.EnvironmentId, x.FlagKey, x.EventName })
            .ToListAsync(ct);

        var envNames = await _db.Environments
            .Where(e => environmentIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var insights = new List<DashboardExperimentInsightDto>();

        foreach (var group in metrics)
        {
            var control = group.FirstOrDefault(g => !g.Variant);
            var treatment = group.FirstOrDefault(g => g.Variant);

            if (control != null && treatment != null && control.TotalExposures > 50 && treatment.TotalExposures > 50)
            {
                var prob = _math.CalculateProbabilityBBeatsA(
                    control.TotalExposures, control.TotalConversions,
                    treatment.TotalExposures, treatment.TotalConversions);
                
                var uplift = _math.CalculateExpectedUplift(
                    control.TotalExposures, control.TotalConversions,
                    treatment.TotalExposures, treatment.TotalConversions);

                if (prob is >= 0.70 or <= 0.30)
                {
                    insights.Add(new DashboardExperimentInsightDto(
                        group.Key.FlagKey,
                        group.Key.EventName,
                        group.Key.EnvironmentId,
                        envNames.GetValueOrDefault(group.Key.EnvironmentId, "Unknown"),
                        prob,
                        uplift
                    ));
                }
            }
        }

        var sortedInsights = insights
            .OrderByDescending(x => Math.Max(x.ProbabilityToBeatBaseline, 1 - x.ProbabilityToBeatBaseline))
            .Take(5)
            .ToList();

        var response = new ProjectDashboardDto(
            activeFlagsCount,
            environmentsCount,
            failingWebhooksCount,
            mabActiveFlagsCount,
            evalPoints,
            sortedInsights
        );

        await Send.OkAsync(response, ct);
    }
}
