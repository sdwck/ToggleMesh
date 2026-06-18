using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Projects.GetProjects;

public class GetProjectsEndpoint : ToggleEndpoint<GetProjectsRequest, List<ProjectListDto>>
{
    private readonly AppDbContext _db;

    public GetProjectsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects");
        Version(1);
    }

    public override async Task HandleAsync(GetProjectsRequest req, CancellationToken ct)
    {
        IQueryable<Project> query = _db.Projects.AsNoTracking();

        if (req.OrganizationId.HasValue)
        {
            query = query.Where(p => p.OrganizationId == req.OrganizationId.Value);

            var orgMember = await _db.OrganizationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(om => om.OrganizationId == req.OrganizationId.Value && om.UserId == UserId, ct);

            if (orgMember == null)
            {
                await Send.OkAsync([], ct);
                return;
            }

            if (orgMember.Role != Features.Organizations.OrganizationRole.Admin)
                query = query.Where(p => p.Members.Any(m => m.UserId == UserId));
        }
        else
            query = query.Where(p => 
                p.Members.Any(m => m.UserId == UserId) || 
                _db.OrganizationMembers.Any(om => om.OrganizationId == p.OrganizationId && om.UserId == UserId && om.Role == Features.Organizations.OrganizationRole.Admin)
            );

        var projects = await query
            .Select(p => new ProjectListDto
            {
                Id = p.Id,
                Name = p.Name,
                EnvironmentCount = p.Environments.Count,
                Environments = p.Environments
                    .OrderBy(e => e.SortOrder)
                    .Select(e => new ProjectEnvironmentDto
                    {
                        Id = e.Id,
                        Name = e.Name
                    }).ToList()
            })
            .ToListAsync(ct);

        await Send.OkAsync(projects, ct);
    }
}