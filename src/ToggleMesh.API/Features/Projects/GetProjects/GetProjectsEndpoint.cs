using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Organizations;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.Common.Pagination;

namespace ToggleMesh.API.Features.Projects.GetProjects;

public class GetProjectsEndpoint : ToggleEndpoint<GetProjectsRequest, PagedResponse<ProjectListDto>>
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
        var query = _db.Projects.AsNoTracking();

        if (req.OrganizationId.HasValue)
        {
            query = query.Where(p => p.OrganizationId == req.OrganizationId.Value);

            var orgMember = await _db.OrganizationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(om => om.OrganizationId == req.OrganizationId.Value && om.UserId == UserId, ct);

            if (orgMember == null)
            {
                await Send.OkAsync(new PagedResponse<ProjectListDto>([], 0, req.Page, req.PageSize), ct);
                return;
            }

            if (orgMember.Role != OrganizationRole.Admin)
                query = query.Where(p => p.Members.Any(m => m.UserId == UserId));
        }
        else
            query = query.Where(p => 
                p.Members.Any(m => m.UserId == UserId) || 
                _db.OrganizationMembers.Any(om => om.OrganizationId == p.OrganizationId && om.UserId == UserId && om.Role == OrganizationRole.Admin)
            );

        var totalCount = await query.CountAsync(ct);

        var projects = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(p => new ProjectListDto
            {
                Id = p.Id,
                Name = p.Name,
                EnvironmentCount = p.Environments.Count,
                UserRole = p.Organization.Members.Any(om => om.UserId == UserId && om.Role == OrganizationRole.Admin)
                    ? ProjectRole.Owner
                    : p.Members.Where(m => m.UserId == UserId).Select(m => (ProjectRole?)m.Role).FirstOrDefault() ?? ProjectRole.None,
                Environments = p.Environments
                    .OrderBy(e => e.SortOrder)
                    .Select(e => new ProjectEnvironmentDto
                    {
                        Id = e.Id,
                        Name = e.Name
                    }).ToList()
            })
            .ToListAsync(ct);

        await Send.OkAsync(new PagedResponse<ProjectListDto>(projects, totalCount, req.Page, req.PageSize), ct);
    }
}