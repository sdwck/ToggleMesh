using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Organizations.GetOrganizations;

public class GetOrganizationsEndpoint : ToggleEndpointWithoutRequest<List<OrganizationDto>>
{
    private readonly AppDbContext _db;

    public GetOrganizationsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/organizations");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var organizations = await _db.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == UserId)
            .Select(m => new OrganizationDto
            {
                Id = m.Organization.Id,
                Name = m.Organization.Name,
                CreatedAt = m.Organization.CreatedAt,
                Role = m.Role
            })
            .OrderBy(o => o.Name)
            .ToListAsync(ct);

        await Send.OkAsync(organizations, ct);
    }
}
