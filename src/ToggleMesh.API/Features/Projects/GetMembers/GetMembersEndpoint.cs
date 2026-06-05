using Microsoft.EntityFrameworkCore;
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
        Policies($"Permission:{Auth.Models.Permissions.ProjectsManageMembers}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var members = await _db.ProjectMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.ProjectId == projectId)
            .Select(m => new MemberDto
            {
                Id = m.Id,
                UserId = m.UserId.ToString(),
                Email = m.User.Email!,
                Role = m.Role
            })
            .ToListAsync(ct);

        await Send.OkAsync(members, ct);
    }
}