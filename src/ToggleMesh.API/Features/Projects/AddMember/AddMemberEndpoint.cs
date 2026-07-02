using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Projects.GetMembers;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


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
        this.RequirePermission(AuthModels.Permissions.ProjectsManageMembers);
    }

    public override async Task HandleAsync(AddMemberRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
        if (user == null)
            ThrowError("User not found.", 400);
        
        var (currentUserRole, _) = await new GetProjectRoleCommand 
        { 
            ProjectId = projectId, 
            UserId = UserId 
        }.ExecuteAsync(ct);

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
            ThrowError("User must be a member of the organization first.", 400);
        
        if (!RoleHierarchy.CanManageMember(currentUserRole.Value, ProjectRole.None, req.Role))
            ThrowError("You do not have permission to grant this role.", 403);

        var existingMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(m =>
                m.ProjectId == projectId &&
                m.UserId == user.Id, ct);

        if (existingMember != null)
            ThrowError("User is already a member of this project.", 400);

        var newMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = user.Id,
            Role = req.Role
        };

        _db.ProjectMembers.Add(newMember);
        await _db.SaveChangesAsync(ct);

        var cacheKey = CacheKeys.ProjectMemberState(projectId, user.Id);
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