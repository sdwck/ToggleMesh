using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Features.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Organizations.InviteMember;

public class InviteMemberEndpoint : ToggleEndpoint<InviteMemberRequest, GetMembers.OrganizationMemberDto>
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public InviteMemberEndpoint(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/organizations/{OrganizationId}/members/invite");
        Version(1);
    }

    public override async Task HandleAsync(InviteMemberRequest req, CancellationToken ct)
    {
        var currentUserMember = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == req.OrganizationId && m.UserId == UserId, ct);

        if (currentUserMember is not { Role: OrganizationRole.Admin })
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var targetUser = await _userManager.FindByEmailAsync(req.Email);
        if (targetUser == null)
        {
            AddError("User not found.");
            await Send.ErrorsAsync(404, cancellation: ct);
            return;
        }

        var exists = await _db.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == req.OrganizationId && m.UserId == targetUser.Id, ct);
            
        if (exists)
        {
            AddError("User is already a member of this organization.");
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        var newMember = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = req.OrganizationId,
            UserId = targetUser.Id,
            Role = req.Role
        };

        _db.OrganizationMembers.Add(newMember);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new GetMembers.OrganizationMemberDto
        {
            UserId = targetUser.Id,
            Email = targetUser.Email!,
            Role = newMember.Role
        }, ct);
    }
}
