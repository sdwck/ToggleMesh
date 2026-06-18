using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Organizations.GetMembers;

public class GetOrganizationMembersEndpoint : ToggleEndpoint<GetOrganizationMembersRequest, List<OrganizationMemberDto>>
{
    private readonly AppDbContext _db;

    public GetOrganizationMembersEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/organizations/{OrganizationId}/members");
        Version(1);
    }

    public override async Task HandleAsync(GetOrganizationMembersRequest req, CancellationToken ct)
    {
        var isMember = await _db.OrganizationMembers.AnyAsync(m => 
            m.OrganizationId == req.OrganizationId && m.UserId == UserId, ct);
        if (!isMember)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var members = await _db.OrganizationMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => 
                m.OrganizationId == req.OrganizationId)
            .Select(m => new OrganizationMemberDto
            {
                UserId = m.UserId,
                Email = m.User.Email!,
                Role = m.Role
            })
            .ToListAsync(ct);

        await Send.OkAsync(members, ct);
    }
}
