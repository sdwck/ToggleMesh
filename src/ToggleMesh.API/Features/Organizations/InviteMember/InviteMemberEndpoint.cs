using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Features.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Email;

namespace ToggleMesh.API.Features.Organizations.InviteMember;

public class InviteMemberEndpoint : ToggleEndpoint<InviteMemberRequest>
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public InviteMemberEndpoint(AppDbContext db, UserManager<ApplicationUser> userManager, IEmailSender emailSender, IConfiguration configuration)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
        _configuration = configuration;
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

        var organization = await _db.Organizations.FindAsync([req.OrganizationId], ct);
        if (organization == null)
            ThrowError("Organization not found.");

        var targetUser = await _userManager.FindByEmailAsync(req.Email);
        if (targetUser != null)
        {
            var exists = await _db.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == req.OrganizationId && m.UserId == targetUser.Id, ct);
                
            if (exists)
            {
                AddError("User is already a member of this organization.");
                await Send.ErrorsAsync(cancellation: ct);
                return;
            }
        }

        var existingInvite = await _db.OrganizationInvitations
            .FirstOrDefaultAsync(i => 
                i.OrganizationId == req.OrganizationId && i.Email == req.Email, ct);

        if (existingInvite != null)
            _db.OrganizationInvitations.Remove(existingInvite);

        var token = Guid.NewGuid().ToString("N");

        var invitation = new OrganizationInvitation
        {
            OrganizationId = req.OrganizationId,
            Email = req.Email,
            Role = req.Role,
            Token = token,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        _db.OrganizationInvitations.Add(invitation);
        await _db.SaveChangesAsync(ct);

        var frontendUrl = _configuration["Auth:FrontendUrl"] ?? "http://localhost:5173";
        var inviteUrl = $"{frontendUrl}/invites/{token}";

        var emailBody = $@"
            <h2>You've been invited!</h2>
            <p>You have been invited to join <strong>{organization.Name}</strong> on ToggleMesh.</p>
            <p><a href='{inviteUrl}'>Click here to accept the invitation</a></p>
        ";

        await _emailSender.SendEmailAsync(req.Email, $"Invitation to join {organization.Name}", emailBody, ct);

        await Send.OkAsync(cancellation: ct);
    }
}
