using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Projects.GetMembers;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Projects.AddMember;

public class AddMemberEndpoint : ToggleEndpoint<AddMemberRequest, MemberDto>
{
    private readonly AppDbContext _db;

    public AddMemberEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/members");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.ProjectsManageMembers}");
    }

    public override async Task HandleAsync(AddMemberRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
        if (user == null)
        {
            AddError("User not found.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var currentUserId = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        var currentUserMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == Guid.Parse(currentUserId!), ct);

        if (currentUserMember == null || currentUserMember.Role > ProjectRole.Admin)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (currentUserMember.Role == ProjectRole.Admin && (req.Role == ProjectRole.Owner || req.Role == ProjectRole.Admin))
        {
            AddError("Admins cannot grant Owner or Admin roles.");
            await Send.ErrorsAsync(403, cancellation: ct);
            return;
        }

        var existingMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(m =>
                m.ProjectId == projectId &&
                m.UserId == user.Id, ct);
            
        if (existingMember != null)
        {
            AddError("User is already a member of this project.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var newMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = user.Id,
            Role = req.Role
        };

        _db.ProjectMembers.Add(newMember);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new MemberDto
        {
            Id = newMember.Id,
            UserId = newMember.UserId.ToString(),
            Email = user.Email!,
            Role = newMember.Role
        }, ct);
    }
}