using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Analytics.GetProjectExperiments;


public class GetProjectExperimentsEndpoint : ToggleEndpointWithoutRequest<List<ProjectExperimentSummaryDto>>
{
    private readonly AppDbContext _db;
    private readonly BayesianMathService _math;

    public GetProjectExperimentsEndpoint(AppDbContext db, BayesianMathService math)
    {
        _db = db;
        _math = math;
    }

    public override void Configure()
    {
        Get("/projects/{projectId:guid}/experiments");
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

        var baseRole = isOrgAdmin ? ProjectRole.Owner : (projectMember?.Role ?? ProjectRole.None);

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

        var activeFlags = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
            .Where(x => envIds.Contains(x.EnvironmentId) && x.FeatureFlag.ProjectId == projectId && x.IsExperimentActive)
            .ToListAsync(ct);

        var activeFlagKeys = activeFlags.Select(x => x.FeatureFlag.Key).Distinct().ToList();

        var allMetrics = await _db.ExperimentMetrics
            .Where(x => envIds.Contains(x.EnvironmentId) && activeFlagKeys.Contains(x.FlagKey))
            .ToListAsync(ct);

        var groupedMetrics = allMetrics
            .GroupBy(x => new { x.EnvironmentId, x.FlagKey, x.EventName })
            .ToDictionary(g => g.Key, g => g.ToList());

        var summaries = new List<ProjectExperimentSummaryDto>();

        foreach (var state in activeFlags)
        {
            var envId = state.EnvironmentId;
            var flagKey = state.FeatureFlag.Key;
            
            var targetEvent = state.MabGoalEvent;
            
            List<ExperimentMetric>? metrics = null;
            var eventName = targetEvent ?? "Any Event";

            if (targetEvent != null)
            {
                var groupKey = new { EnvironmentId = envId, FlagKey = flagKey, EventName = targetEvent };
                if (groupedMetrics.TryGetValue(groupKey, out var g1))
                    metrics = g1;
            }
            else
            {
                var anyGroup = groupedMetrics.Where(x => x.Key.EnvironmentId == envId && x.Key.FlagKey == flagKey)
                    .OrderByDescending(x => x.Value.Sum(m => m.TotalExposures))
                    .FirstOrDefault();
                
                if (anyGroup.Value != null)
                {
                    metrics = anyGroup.Value;
                    eventName = anyGroup.Key.EventName;
                }
            }

            if (metrics == null || metrics.Count == 0)
            {
                summaries.Add(new ProjectExperimentSummaryDto
                {
                    EnvironmentId = envId,
                    EnvironmentName = envs[envId],
                    FlagKey = flagKey,
                    EventName = eventName,
                    TotalParticipants = 0,
                    LastCalculatedAt = state.ExperimentStartedAt ?? DateTimeOffset.UtcNow,
                    ProbabilityToBeatBaseline = 0.5,
                    ExpectedUplift = 0,
                    IsPrimaryGoal = true,
                    IsExperimentActive = state.IsExperimentActive,
                    IsMabEnabled = state.IsMabEnabled,
                    RolloutPercentage = state.RolloutPercentage
                });
                continue;
            }

            var control = metrics.FirstOrDefault(g => !g.Variant);
            var treatment = metrics.FirstOrDefault(g => g.Variant);
            
            var totalExposures = metrics.Sum(x => x.TotalExposures);
            var maxLastCalculatedAt = metrics.Max(x => x.LastCalculatedAt);

            var prob = 0.5;
            double uplift = 0;
            double expectedValueUplift = 0;
            var isRevenueBased = false;

            if (control != null && treatment != null && control.TotalExposures > 0 && treatment.TotalExposures > 0)
            {
                prob = _math.CalculateProbabilityBBeatsA(
                    control.TotalExposures, control.TotalConversions,
                    treatment.TotalExposures, treatment.TotalConversions);
                uplift = _math.CalculateExpectedUplift(
                    control.TotalExposures, control.TotalConversions,
                    treatment.TotalExposures, treatment.TotalConversions);

                isRevenueBased = control.TotalValue > 0 || treatment.TotalValue > 0;
                if (isRevenueBased)
                {
                    var controlArpu = control.TotalValue / control.TotalExposures;
                    var treatmentArpu = treatment.TotalValue / treatment.TotalExposures;
                    expectedValueUplift = controlArpu > 0 ? (treatmentArpu - controlArpu) / controlArpu : 0;
                }
            }

            summaries.Add(new ProjectExperimentSummaryDto
            {
                EnvironmentId = envId,
                EnvironmentName = envs[envId],
                FlagKey = flagKey,
                EventName = eventName,
                TotalParticipants = totalExposures,
                LastCalculatedAt = maxLastCalculatedAt,
                ProbabilityToBeatBaseline = prob,
                ExpectedUplift = uplift,
                ExpectedValueUplift = expectedValueUplift,
                IsRevenueBased = isRevenueBased,
                IsPrimaryGoal = eventName == state.MabGoalEvent,
                IsExperimentActive = state.IsExperimentActive,
                IsMabEnabled = state.IsMabEnabled,
                RolloutPercentage = state.RolloutPercentage
            });
        }

        var finalResponse = summaries
            .OrderByDescending(x => x.LastCalculatedAt)
            .ToList();

        await Send.OkAsync(finalResponse, ct);
    }
}
