using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Extensions;
using Quartz;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.ScheduledChanges;

public class CancelScheduledChangeEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly ISchedulerFactory _schedulerFactory;

    public CancelScheduledChangeEndpoint(AppDbContext db, ISchedulerFactory schedulerFactory)
    {
        _db = db;
        _schedulerFactory = schedulerFactory;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/flags/{key}/environments/{environmentId}/changes/{changeId}/cancel");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(CancellationToken ct)
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

        var change = await _db.PendingFlagChanges
            .FirstOrDefaultAsync(x => x.Id == changeId && x.EnvironmentId == environmentId, ct);

        if (change is null)
            ThrowError("Change not found.", 404);

        var isAdminOrOwner = (projectMember != null && (projectMember.Role == ProjectRole.Owner || projectMember.Role == ProjectRole.Admin)) ||
                             orgMember is { Role: OrganizationRole.Admin };

        if (!isAdminOrOwner && change.RequestedByUserId != UserId)
            ThrowError("Only Project Admins, Owners, or the original author can perform this action.", 403);

        if (change.Status != PendingFlagChangeStatus.Scheduled && change.Status != PendingFlagChangeStatus.PendingReview)
            ThrowError($"Cannot cancel a change with status '{change.Status}'.", 400);

        var wasScheduled = change.Status == PendingFlagChangeStatus.Scheduled;

        change.Status = PendingFlagChangeStatus.Cancelled;
        change.ReviewedByUserId = UserId;
        await _db.SaveChangesAsync(ct);

        if (wasScheduled)
        {
            var scheduler = await _schedulerFactory.GetScheduler(ct);
            await scheduler.UnscheduleJob(new TriggerKey($"scheduled-change-{change.Id}"), ct);
        }

        await Send.OkAsync(new { change.Id, change.Status }, ct);
    }
}
