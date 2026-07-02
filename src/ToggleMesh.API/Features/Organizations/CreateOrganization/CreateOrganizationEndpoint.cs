using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Organizations.GetOrganizations;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Organizations.CreateOrganization;

public class CreateOrganizationEndpoint : ToggleEndpoint<CreateOrganizationRequest, OrganizationDto>
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public CreateOrganizationEndpoint(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public override void Configure()
    {
        Post("/organizations");
        Version(1);
    }

    public override async Task HandleAsync(CreateOrganizationRequest req, CancellationToken ct)
    {
        var allowCreation = _configuration.GetValue("Auth:AllowUserOrganizationCreation", true);
        if (!allowCreation)
        {
            var adminEmail = _configuration["DEFAULT_ADMIN_EMAIL"];
            var user = await _db.Users.FindAsync(new object[] { UserId }, ct);
            if (user == null || !string.Equals(user.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
                ThrowError("User organization creation is disabled in this environment.", 403);
        }

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
