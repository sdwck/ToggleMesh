using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Projects.GetProjects;

public class GetProjectsEndpoint : ToggleEndpointWithoutRequest<List<ProjectListDto>>
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

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projects = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == UserId))
            .Select(p => new ProjectListDto
            {
                Id = p.Id,
                Name = p.Name,
                EnvironmentCount = p.Environments.Count
            })
            .ToListAsync(ct);

        await Send.OkAsync(projects, ct);
    }
}