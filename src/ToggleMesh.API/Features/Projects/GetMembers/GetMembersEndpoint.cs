using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Organizations;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Projects.GetMembers;

public class GetMembersEndpoint : ToggleEndpointWithoutRequest<List<MemberDto>>
{
    private readonly AppDbContext _db;

    public GetMembersEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/members");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.ProjectsManageMembers);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var organizationId = project.OrganizationId;

        var explicitMembers = await _db.ProjectMembers
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Include(m => m.User)
            .Include(m => m.EnvironmentRoles)
            .ToListAsync(ct);

        var orgAdmins = await _db.OrganizationMembers
            .AsNoTracking()
            .Where(om => om.OrganizationId == organizationId && om.Role == OrganizationRole.Admin)
            .Include(om => om.User)
            .ToListAsync(ct);

        var members = new List<MemberDto>();
        var orgAdminUserIds = new HashSet<Guid>();

        foreach (var admin in orgAdmins)
        {
            orgAdminUserIds.Add(admin.UserId);
            var explicitMember = explicitMembers.FirstOrDefault(m => m.UserId == admin.UserId);

            members.Add(new MemberDto
            {
                Id = explicitMember?.Id ?? admin.UserId,
                UserId = admin.UserId.ToString(),
                Email = admin.User.Email!,
                Role = ProjectRole.Owner,
                IsOrganizationAdmin = true,
                EnvironmentRoles = explicitMember?.EnvironmentRoles.Select(er => new EnvironmentRoleDto
                {
                    EnvironmentId = er.EnvironmentId,
                    Role = er.Role
                }).ToList() ?? [],
                CreatedAt = admin.CreatedAt
            });
        }

        foreach (var m in explicitMembers)
            if (!orgAdminUserIds.Contains(m.UserId))
                members.Add(new MemberDto
                {
                    Id = m.Id,
                    UserId = m.UserId.ToString(),
                    Email = m.User.Email!,
                    Role = m.Role,
                    IsOrganizationAdmin = false,
                    EnvironmentRoles = m.EnvironmentRoles.Select(er => new EnvironmentRoleDto
                    {
                        EnvironmentId = er.EnvironmentId,
                        Role = er.Role
                    }).ToList(),
                    CreatedAt = m.CreatedAt
                });

        await Send.OkAsync(members, ct);
    }
}