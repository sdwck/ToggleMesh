using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Extensions;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.ScheduledChanges;

public class ExecuteScheduledChangeEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public ExecuteScheduledChangeEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/flags/{key}/environments/{environmentId}/changes/{changeId}/execute-now");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var flagKey = Route<string>("key")!;
        var environmentId = Route<Guid>("environmentId");
        var changeId = Route<Guid>("changeId");

        var projectMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == UserId, ct);

        var orgMember = await _db.OrganizationMembers
            .Include(x => x.Organization)
            .ThenInclude(o => o.Projects)
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.Organization.Projects.Any(p => p.Id == projectId), ct);

        var isAdminOrOwner = (projectMember != null && (projectMember.Role == ProjectRole.Owner || projectMember.Role == ProjectRole.Admin)) ||
                             orgMember is { Role: OrganizationRole.Admin };

        var change = await _db.PendingFlagChanges
            .FirstOrDefaultAsync(x => x.Id == changeId && x.EnvironmentId == environmentId, ct);

        if (change is null)
            ThrowError("Change not found.", 404);

        if (!isAdminOrOwner && change.RequestedByUserId != UserId)
            ThrowError("You do not have permission to execute this scheduled change. Only Admins or the author can execute it.", 403);

        if (change.Status != PendingFlagChangeStatus.Scheduled)
            ThrowError($"Cannot execute a change with status '{change.Status}'. It must be approved and scheduled first.", 400);

        FlagEnvironmentState state;
        try
        {
            state = await FlagPatchApplicationHelper.ApplyPatchAsync(_db, change, ct);
        }
        catch (Exception ex)
        {
            ThrowError($"Failed to execute change: {ex.Message}", 400);
            return;
        }

        try
        {
            await new NotifyFlagUpdatedCommand(
                environmentId,
                flagKey,
                state.ToDto(),
                state.ToSdkDto()
            ).ExecuteAsync(ct);
        }
        catch
        {
            // ignore
        }

        await Send.OkAsync(new { change.Id, change.Status }, ct);
    }
}
