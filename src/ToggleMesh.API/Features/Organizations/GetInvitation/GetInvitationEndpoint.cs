using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Organizations.GetInvitation;

public class GetInvitationEndpoint : ToggleEndpoint<GetInvitationRequest, OrganizationInvitationDto>
{
    private readonly AppDbContext _db;

    public GetInvitationEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/organizations/invites/{Token}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetInvitationRequest req, CancellationToken ct)
    {
        var invite = await _db.OrganizationInvitations
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Token == req.Token, ct);

        if (invite == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (invite.ExpiresAt < DateTimeOffset.UtcNow)
            ThrowError("This invitation has expired.");

        await Send.OkAsync(new OrganizationInvitationDto
        {
            OrganizationName = invite.Organization.Name,
            Email = invite.Email,
            Role = invite.Role
        }, ct);
    }
}
