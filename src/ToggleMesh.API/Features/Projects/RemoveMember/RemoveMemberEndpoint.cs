using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Projects.RemoveMember;

public class RemoveMemberEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;

    public RemoveMemberEndpoint(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis.GetDatabase();
    }

    public override void Configure()
    {
        Delete("/projects/{projectId:guid}/members/{userId:guid}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.ProjectsManageMembers);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var userId = Route<Guid>("userId");

        var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (currentUserId == userId.ToString())
        {
            AddError("You cannot remove yourself from the project.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var member = await _db.ProjectMembers
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

        if (currentUserMember.Role == ProjectRole.Admin && member.Role == ProjectRole.Owner)
        {
            AddError("Admins cannot remove Owners.");
            await Send.ErrorsAsync(403, cancellation: ct);
            return;
        }

        _db.ProjectMembers.Remove(member);
        await _db.SaveChangesAsync(ct);

        var cacheKey = $"project-member-state:{projectId}:{userId}";
        await _redis.KeyDeleteAsync(cacheKey);

        await Send.NoContentAsync(ct);
    }
}
