using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
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
        var members = await _db.ProjectMembers
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Include(m => m.User)
            .Include(m => m.EnvironmentRoles)
            .Select(m => new MemberDto
            {
                Id = m.Id,
                UserId = m.UserId.ToString(),
                Email = m.User.Email!,
                Role = m.Role,
                EnvironmentRoles = m.EnvironmentRoles.Select(er => new EnvironmentRoleDto
                {
                    EnvironmentId = er.EnvironmentId,
                    Role = er.Role
                }).ToList()
            })
            .ToListAsync(ct);

        await Send.OkAsync(members, ct);
    }
}