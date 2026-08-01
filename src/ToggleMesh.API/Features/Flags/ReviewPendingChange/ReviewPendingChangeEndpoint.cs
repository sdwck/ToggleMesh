using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.ScheduledChanges;
using ToggleMesh.API.Features.Flags.Commands;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.ReviewPendingChange;

public class ReviewPendingChangeEndpoint : ToggleEndpoint<ReviewPendingChangeRequest>
{
    private readonly AppDbContext _db;

    public ReviewPendingChangeEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/flags/{key}/environments/{environmentId}/changes/{changeId}/review");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(ReviewPendingChangeRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");
        var changeId = Route<Guid>("changeId");

        var projectMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == UserId, ct);

        var orgMember = await _db.OrganizationMembers
            .Include(x => x.Organization)
            .ThenInclude(o => o.Projects)
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.Organization.Projects.Any(p => p.Id == projectId), ct);

        bool isAdminOrOwner = (projectMember != null && (projectMember.Role == ProjectRole.Owner || projectMember.Role == ProjectRole.Admin)) ||
                              (orgMember != null && orgMember.Role == OrganizationRole.Admin);

        if (!isAdminOrOwner)
            ThrowError("Only Project Admins or Owners can review and approve flag changes.", 403);

        var change = await _db.PendingFlagChanges
            .FirstOrDefaultAsync(x => x.Id == changeId && x.FlagId == x.Flag.Id && x.EnvironmentId == environmentId, ct);

        if (change is null)
            ThrowError("Change not found.", 404);

        if (change.Status != PendingFlagChangeStatus.PendingReview && change.Status != PendingFlagChangeStatus.Scheduled)
            ThrowError($"Cannot review a change with status '{change.Status}'.", 400);

        if (change.RequestedByUserId == UserId)
            ThrowError("Author cannot approve their own change request.", 400);

        if (req.Action == ReviewAction.Reject)
        {
            change.Status = PendingFlagChangeStatus.Rejected;
            change.ReviewedByUserId = UserId;
            await _db.SaveChangesAsync(ct);
            await Send.OkAsync(new { change.Id, change.Status }, ct);
            return;
        }

        if (req.Action != ReviewAction.Approve)
            ThrowError("Invalid action.", 400);

        if (!change.ApprovedByUserIds.Contains(UserId))
            change.ApprovedByUserIds.Add(UserId);

        var env = await _db.Environments.FirstOrDefaultAsync(x => x.Id == environmentId, ct);
        int requiredCount = env?.RequiredApprovalsCount ?? 1;

        if (change.ApprovedByUserIds.Count >= requiredCount)
        {
            if (!change.ExecuteAt.HasValue || change.ExecuteAt.Value <= DateTimeOffset.UtcNow)
            {
                FlagEnvironmentState state;
                try
                {
                    state = await FlagPatchApplicationHelper.ApplyPatchAsync(_db, change, ct);
                }
                catch (Exception ex)
                {
                    ThrowError($"Failed to apply patch: {ex.Message}", 400);
                    return;
                }
                
                try
                {
                    await new NotifyFlagUpdatedCommand(
                        change.EnvironmentId,
                        state.FeatureFlag.Key,
                        state.ToDto(),
                        state.ToSdkDto()
                    ).ExecuteAsync(ct);
                }
                catch
                {
                    // ignore
                }
            }
            else
            {
                change.Status = PendingFlagChangeStatus.Scheduled;
                await _db.SaveChangesAsync(ct);
            }
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }

        await Send.OkAsync(new
        {
            change.Id,
            change.Status,
            ApprovedCount = change.ApprovedByUserIds.Count,
            RequiredCount = requiredCount
        }, ct);
    }
}
