using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Extensions;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Projects.UpdateApprovalPolicy;

public class UpdateEnvironmentApprovalPolicyEndpoint : ToggleEndpoint<UpdateEnvironmentApprovalPolicyRequest>
{
    private readonly AppDbContext _db;

    public UpdateEnvironmentApprovalPolicyEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/projects/{projectId}/environments/{environmentId}/approval-policy");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(UpdateEnvironmentApprovalPolicyRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");

        var projectMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == UserId, ct);

        var orgMember = await _db.OrganizationMembers
            .Include(x => x.Organization)
            .ThenInclude(o => o.Projects)
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.Organization.Projects.Any(p => p.Id == projectId), ct);

        var isOwner = projectMember is { Role: ProjectRole.Owner } ||
                      orgMember is { Role: OrganizationRole.Admin };

        if (!isOwner)
        {
            await Send.ResponseAsync(new { message = "Only Project or Organization Owners can modify environment approval policies." }, 403, ct);
            return;
        }

        var env = await _db.Environments
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Id == environmentId, ct);

        if (env is null)
        {
            await Send.ResponseAsync(new { message = "Environment not found" }, 404, ct);
            return;
        }

        if (req.RequireApprovals)
        {
            if (req.RequiredApprovalsCount < 1)
            {
                await Send.ResponseAsync(new { message = "Required approvals count must be at least 1." }, 400, ct);
                return;
            }

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

            var totalApprovers = projectMemberUserIds.Union(orgOwnerUserIds).Distinct().Count();
            var minRequiredUsers = req.RequiredApprovalsCount;

            if (totalApprovers < minRequiredUsers)
            {
                await Send.ResponseAsync(new { message = $"Need at least {minRequiredUsers} Admins/Owners to reach the required approvals. Currently: {totalApprovers}." }, 400, cancellation: ct);
                return;
            }
        }

        env.RequireApprovals = req.RequireApprovals;
        env.RequiredApprovalsCount = Math.Max(1, req.RequiredApprovalsCount);
        env.RequireForProtectedFlagsOnly = req.RequireForProtectedFlagsOnly;

        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new
        {
            env.Id,
            env.RequireApprovals,
            env.RequiredApprovalsCount,
            env.RequireForProtectedFlagsOnly
        }, ct);
    }
}
