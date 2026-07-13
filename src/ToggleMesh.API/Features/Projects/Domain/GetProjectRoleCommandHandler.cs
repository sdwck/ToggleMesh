using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Projects.Domain;

public class GetProjectRoleCommandHandler : ICommandHandler<GetProjectRoleCommand, ProjectRoleResult>
{
    private readonly AppDbContext _db;

    public GetProjectRoleCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProjectRoleResult> ExecuteAsync(GetProjectRoleCommand command, CancellationToken ct = default)
    {
        var data = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == command.ProjectId)
            .Select(p => new
            {
                p.OrganizationId,
                OrgMember = _db.OrganizationMembers.FirstOrDefault(om =>
                    om.OrganizationId == p.OrganizationId &&
                    om.UserId == command.UserId),
                ProjMember = _db.ProjectMembers.Include(pm => pm.EnvironmentRoles)
                    .FirstOrDefault(pm => pm.ProjectId == p.Id && pm.UserId == command.UserId)
            })
            .FirstOrDefaultAsync(ct);

        if (data == null) 
            return new(null, []);

        if (data.OrgMember?.Role == OrganizationRole.Admin)
            return new(ProjectRole.Owner, []);

        if (data.ProjMember == null)
            return new(null, []);

        var envRoles = data.ProjMember.EnvironmentRoles.ToDictionary(er => er.EnvironmentId, er => er.Role);
        
        return new(data.ProjMember.Role, envRoles);
    }
}
