using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
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
        Policies($"Permission:{Auth.Models.Permissions.ProjectsManageMembers}");
    }

    public override async Task HandleAsync(UpdateMemberRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var userId = Route<Guid>("userId");

        var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (currentUserId == userId.ToString())
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

        var currentUserMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == Guid.Parse(currentUserId!), ct);

        if (currentUserMember == null || currentUserMember.Role > ProjectRole.Admin)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (currentUserMember.Role == ProjectRole.Admin)
        {
            if (member.Role == ProjectRole.Owner)
            {
                AddError("Admins cannot modify Owners.");
                await Send.ErrorsAsync(403, cancellation: ct);
                return;
            }
            if (req.Role == ProjectRole.Owner || req.Role == ProjectRole.Admin)
            {
                AddError("Admins cannot grant Owner or Admin roles.");
                await Send.ErrorsAsync(403, cancellation: ct);
                return;
            }
        }

        member.Role = req.Role;

        _db.MemberEnvironmentRoles.RemoveRange(member.EnvironmentRoles);
        member.EnvironmentRoles.Clear();

        if ((req.Role == ProjectRole.Editor || req.Role == ProjectRole.Viewer || req.Role == ProjectRole.None) && req.EnvironmentRoles != null)
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
