using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Organizations.InviteMember;

public class InviteMemberEndpoint : ToggleEndpoint<InviteMemberRequest>
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IEmailTemplateService _templateService;

    public InviteMemberEndpoint(AppDbContext db, UserManager<ApplicationUser> userManager, IEmailSender emailSender, IConfiguration configuration, IEmailTemplateService templateService)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
        _configuration = configuration;
        _templateService = templateService;
    }

    public override void Configure()
    {
        Post("/organizations/{OrganizationId}/members/invite");
        Version(1);
        PreProcessor<RequireOrgAdminPreProcessor<InviteMemberRequest>>();
    }

    public override async Task HandleAsync(InviteMemberRequest req, CancellationToken ct)
    {
        var organization = await _db.Organizations.FindAsync([req.OrganizationId], ct);
        if (organization == null)
            ThrowError("Organization not found.");

        var targetUser = await _userManager.FindByEmailAsync(req.Email);
        if (targetUser != null)
        {
            var exists = await _db.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == req.OrganizationId && m.UserId == targetUser.Id, ct);
                
            if (exists)
                ThrowError("User is already a member of this organization.", 400);
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

        var startYear = 2026;
        var currentYear = DateTimeOffset.UtcNow.Year;
        var copyrightYear = currentYear > startYear ? $"{startYear}-{currentYear}" : startYear.ToString();

        var emailBody = await _templateService.RenderAsync("InviteTemplate", new 
        { 
            OrganizationName = organization.Name, 
            InviteUrl = inviteUrl,
            ToggleMeshLogoUrl = "https://raw.githubusercontent.com/sdwck/ToggleMesh/main/src/ToggleMesh.AdminUI/src/assets/icon.png",
            CopyrightYear = copyrightYear,
            DashboardUrl = frontendUrl
        }, ct);

        await _emailSender.SendEmailAsync(req.Email, $"Invitation to join {organization.Name}", emailBody, ct);

        await Send.OkAsync(cancellation: ct);
    }
}
