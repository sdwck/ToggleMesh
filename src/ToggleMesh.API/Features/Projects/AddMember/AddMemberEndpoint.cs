using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Projects.GetMembers;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Memory;

namespace ToggleMesh.API.Features.Projects.AddMember;

public class AddMemberEndpoint : ToggleEndpoint<AddMemberRequest, MemberDto>
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    public AddMemberEndpoint(AppDbContext db, IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _db = db;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/members");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.ProjectsManageMembers);
    }

    public override async Task HandleAsync(AddMemberRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
        if (user == null)
        {
            AddError("User not found.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }
        
        var (currentUserRole, _) =
            await _db.GetProjectRoleAndEnvOverridesAsync(
                projectId, 
                UserId, 
                ct);

        if (currentUserRole is null or > ProjectRole.Admin)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var isOrgMember = await _db.OrganizationMembers
            .AnyAsync(om => om.OrganizationId == project.OrganizationId && om.UserId == user.Id, ct);
            
        if (!isOrgMember)
        {
            AddError("User must be a member of the organization first.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }



        if (currentUserRole.Value == ProjectRole.Admin
            && req.Role 
                is ProjectRole.Owner 
                or ProjectRole.Admin)
        {
            AddError("Admins cannot grant Owner or Admin roles.");
            await Send.ErrorsAsync(403, cancellation: ct);
            return;
        }

        var existingMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(m =>
                m.ProjectId == projectId &&
                m.UserId == user.Id, ct);

        if (existingMember != null)
        {
            AddError("User is already a member of this project.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var newMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = user.Id,
            Role = req.Role
        };

        _db.ProjectMembers.Add(newMember);
        await _db.SaveChangesAsync(ct);

        var cacheKey = $"project-member-state:{projectId}:{user.Id}";
        await _redis.KeyDeleteAsync(cacheKey);
        _memoryCache.Remove(cacheKey);

        await Send.OkAsync(new MemberDto
        {
            Id = newMember.Id,
            UserId = newMember.UserId.ToString(),
            Email = user.Email!,
            Role = newMember.Role
        }, ct);
    }
}