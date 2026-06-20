using ToggleMesh.API.Features.Organizations.GetOrganizations;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Organizations.CreateOrganization;

public class CreateOrganizationEndpoint : ToggleEndpoint<CreateOrganizationRequest, OrganizationDto>
{
    private readonly AppDbContext _db;

    public CreateOrganizationEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/organizations");
        Version(1);
    }

    public override async Task HandleAsync(CreateOrganizationRequest req, CancellationToken ct)
    {
        var org = new Organization
        {
            Name = req.Name,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Organizations.AddAsync(org, ct);

        var member = new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = UserId,
            Role = OrganizationRole.Admin
        };

        await _db.OrganizationMembers.AddAsync(member, ct);

        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new OrganizationDto
        {
            Id = org.Id,
            Name = org.Name,
            CreatedAt = org.CreatedAt,
            Role = member.Role
        }, ct);
    }
}
