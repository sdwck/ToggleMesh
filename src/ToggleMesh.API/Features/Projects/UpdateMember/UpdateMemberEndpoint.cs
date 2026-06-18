using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Projects.UpdateMember;

public class UpdateMemberEndpoint : ToggleEndpoint<UpdateMemberRequest>
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;

    public UpdateMemberEndpoint(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis.GetDatabase();
    }

    public override void Configure()
    {
        Put("/projects/{projectId:guid}/members/{userId:guid}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.ProjectsManageMembers);
    }

    public override async Task HandleAsync(UpdateMemberRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var userId = Route<Guid>("userId");

        if (UserId == userId)
        {
            AddError("You cannot change your own role.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var member = await _db.ProjectMembers
            .Include(m => m.EnvironmentRoles)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId, ct);

        if (member == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var (currentUserRole, _) = await _db.GetProjectRoleAndEnvOverridesAsync(projectId, UserId, ct);

        if (currentUserRole is null or > ProjectRole.Admin)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (currentUserRole.Value == ProjectRole.Admin)
        {
            if (member.Role == ProjectRole.Owner)
            {
                AddError("Admins cannot modify Owners.");
                await Send.ErrorsAsync(403, cancellation: ct);
                return;
            }
            
            if (req.Role is ProjectRole.Owner or ProjectRole.Admin)
            {
                AddError("Admins cannot grant Owner or Admin roles.");
                await Send.ErrorsAsync(403, cancellation: ct);
                return;
            }
        }

        member.Role = req.Role;

        _db.MemberEnvironmentRoles.RemoveRange(member.EnvironmentRoles);
        member.EnvironmentRoles.Clear();

        if (req.Role 
                is ProjectRole.Editor 
                or ProjectRole.Viewer 
                or ProjectRole.None 
            && req.EnvironmentRoles != null)
        {
            foreach (var er in req.EnvironmentRoles)
            {
                member.EnvironmentRoles.Add(new MemberEnvironmentRole
                {
                    EnvironmentId = er.EnvironmentId,
                    Role = er.Role
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var cacheKey = $"project-member-state:{projectId}:{userId}";
        await _redis.KeyDeleteAsync(cacheKey);

        await Send.NoContentAsync(ct);
    }
}
