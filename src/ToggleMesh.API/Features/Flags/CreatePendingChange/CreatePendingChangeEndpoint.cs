using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using System.Text.Json;
using System.Text.Json;
using ToggleMesh.API.Features.Flags.Update;
using Quartz;
using ToggleMesh.API.Features.Flags.ScheduledChanges.Jobs;

namespace ToggleMesh.API.Features.Flags.CreatePendingChange;

public class CreatePendingChangeEndpoint : ToggleEndpoint<CreatePendingChangeRequest>
{
    private readonly AppDbContext _db;
    private readonly ISchedulerFactory _schedulerFactory;

    public CreatePendingChangeEndpoint(AppDbContext db, ISchedulerFactory schedulerFactory)
    {
        _db = db;
        _schedulerFactory = schedulerFactory;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/flags/{key}/environments/{environmentId}/changes");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(CreatePendingChangeRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var flagKey = Route<string>("key")!;
        var environmentId = Route<Guid>("environmentId");

        var flag = await _db.FeatureFlags
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Key == flagKey, ct);

        if (flag is null)
            ThrowError("Flag not found.", 404);

        var envState = await _db.FlagEnvironmentStates
            .AsSplitQuery()
            .Include(x => x.Rules)
            .Include(x => x.IndividualTargets)
            .FirstOrDefaultAsync(x => x.FeatureFlagId == flag.Id && x.EnvironmentId == environmentId, ct);

        if (envState is { IsExperimentActive: true })
            ThrowError("Cannot propose changes or schedule rollouts while an Experiment (A/B Test) is currently running in this environment.", 400);

        var environment = await _db.Environments
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Id == environmentId, ct);
        if (environment is null)
            ThrowError("Environment not found.", 404);

        if (string.IsNullOrWhiteSpace(req.PatchInstructionsJson) ||
            req.PatchInstructionsJson.Trim() == "{}" ||
            req.PatchInstructionsJson.Trim() == "[]")
            ThrowError("Cannot create a change request with no modifications.", 400);

        UpdateFlagRequest? parsedPatch = null;
        try
        {
            parsedPatch = JsonSerializer.Deserialize<UpdateFlagRequest>(
                req.PatchInstructionsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsedPatch == null || 
                (parsedPatch.Rules == null && parsedPatch.OffVariationId == null && 
                 parsedPatch.FallthroughRollout == null && parsedPatch.IndividualTargets == null && 
                 parsedPatch.IsEnabled == null))
            {
                ThrowError("Cannot create a change request with no modifications.", 400);
            }
        }
        catch
        {
            ThrowError("Invalid JSON patch instructions.", 400);
        }

        if (req.ExecuteAt.HasValue && req.ExecuteAt.Value <= DateTimeOffset.UtcNow)
            ThrowError("Scheduled execution time must be in the future.", 400);

        var requiresApproval = environment.RequireApprovals &&
                               (!environment.RequireForProtectedFlagsOnly || flag.IsProtected);

        if (!requiresApproval && !req.ExecuteAt.HasValue)
            ThrowError("Approvals are not enabled for this environment and no execution date was scheduled.", 400);

        if (requiresApproval)
        {
            var projectMemberUserIds = await _db.ProjectMembers
                .Where(pm => pm.ProjectId == projectId && (pm.Role == ProjectRole.Owner || pm.Role == ProjectRole.Admin))
                .Select(pm => pm.UserId)
                .ToListAsync(ct);

            var orgOwnerUserIds = await _db.OrganizationMembers
                .Include(x => x.Organization)
                .ThenInclude(o => o.Projects)
                .Where(om => om.Organization.Projects.Any(p => p.Id == projectId) && om.Role == OrganizationRole.Admin)
                .Select(om => om.UserId)
                .ToListAsync(ct);

            var adminUserIds = projectMemberUserIds.Union(orgOwnerUserIds).Distinct().ToList();
            var isAuthorAdmin = adminUserIds.Contains(UserId);
            var eligibleApproversCount = adminUserIds.Count - (isAuthorAdmin ? 1 : 0);

            if (eligibleApproversCount < environment.RequiredApprovalsCount)
            {
                await Send.ResponseAsync(new { message = $"Not enough eligible approvers in the project. You need {environment.RequiredApprovalsCount} approver(s), but there are only {eligibleApproversCount} eligible Admin(s). Please invite more Admins or disable the Approval Policy." }, 400, cancellation: ct);
                return;
            }
        }

        string? diffSummaryJson = null;
        if (parsedPatch != null && envState != null)
        {
            var summary = ComputeDiffSummary(envState, parsedPatch);
            diffSummaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        var change = new PendingFlagChange
        {
            Id = Guid.NewGuid(),
            FlagId = flag.Id,
            EnvironmentId = environmentId,
            RequestedByUserId = UserId,
            PatchInstructionsJson = req.PatchInstructionsJson,
            DiffSummaryJson = diffSummaryJson,
            ExecuteAt = req.ExecuteAt,
            IsPurelyScheduled = !requiresApproval && req.ExecuteAt.HasValue,
            Comment = req.Comment,
            Status = requiresApproval ? PendingFlagChangeStatus.PendingReview : (req.ExecuteAt.HasValue ? PendingFlagChangeStatus.Scheduled : PendingFlagChangeStatus.PendingReview)
        };

        _db.PendingFlagChanges.Add(change);
        await _db.SaveChangesAsync(ct);

        if (change.Status == PendingFlagChangeStatus.Scheduled && change.ExecuteAt.HasValue)
        {
            var scheduler = await _schedulerFactory.GetScheduler(ct);

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"scheduled-change-{change.Id}")
                .StartAt(change.ExecuteAt.Value)
                .Build();

            var job = JobBuilder.Create<ExecuteScheduledChangeJob>()
                .WithIdentity($"job-scheduled-change-{change.Id}")
                .UsingJobData("ChangeId", change.Id.ToString())
                .Build();

            await scheduler.ScheduleJob(job, trigger, ct);
        }

        await Send.OkAsync(new
        {
            change.Id,
            change.FlagId,
            change.EnvironmentId,
            change.RequestedByUserId,
            change.Status,
            change.ExecuteAt,
            change.Comment,
            change.CreatedAt
        }, ct);
    }
    private PendingChangeDiffSummary ComputeDiffSummary(FlagEnvironmentState envState, UpdateFlagRequest parsedPatch)
    {
        var summary = new PendingChangeDiffSummary();

        if (parsedPatch.IsEnabled.HasValue && parsedPatch.IsEnabled.Value != envState.IsEnabled)
        {
            summary.IsEnabledChanged = true;
            summary.NewIsEnabled = parsedPatch.IsEnabled.Value;
        }

        if (parsedPatch.FallthroughRollout != null)
        {
            var oldFallthrough = JsonSerializer.Serialize(envState.FallthroughRollout);
            var newFallthrough = JsonSerializer.Serialize(parsedPatch.FallthroughRollout);
            if (oldFallthrough != newFallthrough)
            {
                summary.FallthroughRolloutChanged = true;
                summary.NewFallthroughRollout = parsedPatch.FallthroughRollout;
            }
        }

        if (parsedPatch.Rules != null)
        {
            var envRulesList = envState.Rules.ToList();
            var oldMap = envRulesList.Select((r, idx) => new { Key = r.GroupId.ToString() ?? $"idx_{idx}", Rule = r }).ToDictionary(x => x.Key, x => x.Rule);
            var currentMap = new Dictionary<string, RuleInput>();

            for (int idx = 0; idx < parsedPatch.Rules.Count; idx++)
            {
                var r = parsedPatch.Rules[idx];
                var key = r.GroupId.ToString() ?? $"idx_{idx}";
                currentMap[key] = r;

                if (!oldMap.TryGetValue(key, out var oldRule))
                {
                    summary.AddedRules.Add(r);
                }
                else
                {
                    var oldRuleDto = new RuleInput(
                        oldRule.GroupId,
                        oldRule.Attribute,
                        oldRule.Operator,
                        oldRule.Value,
                        oldRule.Rollout?.Select(ro => new VariationWeight { VariationId = ro.VariationId, Weight = ro.Weight }).ToList()
                    );
                    var oldJson = JsonSerializer.Serialize(oldRuleDto);
                    var newJson = JsonSerializer.Serialize(r);
                    if (oldJson != newJson)
                    {
                        summary.ModifiedRules.Add(r);
                    }
                }
            }

            for (int idx = 0; idx < envRulesList.Count; idx++)
            {
                var r = envRulesList[idx];
                var key = r.GroupId.ToString() ?? $"idx_{idx}";
                if (!currentMap.ContainsKey(key))
                {
                    summary.DeletedRules.Add(new RuleInput(
                        r.GroupId,
                        r.Attribute,
                        r.Operator,
                        r.Value,
                        r.Rollout?.Select(ro => new VariationWeight { VariationId = ro.VariationId, Weight = ro.Weight }).ToList()
                    ));
                }
            }
        }

        if (parsedPatch.IndividualTargets != null)
        {
            var oldTargets = JsonSerializer.Serialize(envState.IndividualTargets.ToDictionary(x => x.IdentityKey, x => x.VariationId));
            var newTargets = JsonSerializer.Serialize(parsedPatch.IndividualTargets);
            if (oldTargets != newTargets)
            {
                summary.IndividualTargetsChanged = true;
                summary.NewIndividualTargetsCount = parsedPatch.IndividualTargets.Count;
            }
        }

        return summary;
    }
}
