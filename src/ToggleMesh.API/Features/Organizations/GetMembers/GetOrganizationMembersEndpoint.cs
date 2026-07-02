using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
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
        PreProcessor<RequireOrgAdminPreProcessor<GetOrganizationMembersRequest>>();
    }

    public override async Task HandleAsync(GetOrganizationMembersRequest req, CancellationToken ct)
    {
        var members = await _db.OrganizationMembers
            .AsNoTracking()
            .Where(m => 
                m.OrganizationId == req.OrganizationId)
            .Select(m => new OrganizationMemberDto
            {
                UserId = m.UserId,
                Email = m.User.Email!,
                Role = m.Role,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        await Send.OkAsync(members, ct);
    }
}
