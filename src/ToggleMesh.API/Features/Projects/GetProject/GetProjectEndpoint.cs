using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Projects.GetProject;

public class GetProjectEndpoint : ToggleEndpointWithoutRequest<GetProjectResponse>
{
    private readonly AppDbContext _db;

    public GetProjectEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.ProjectsView}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var isOwner = User.HasClaim(c => c is { Type: "Role", Value: "Owner" });

        var query = _db.Projects
            .AsNoTracking()
            .Include(p => p.Environments)
            .ThenInclude(e => e.Keys)
            .AsQueryable();

        if (!isOwner)
            query = query.Where(p => p.Members.Any(m => m.UserId == UserId));

        var project = await query.FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = new GetProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Environments = project.Environments.Select(e => new EnvironmentDto
            {
                Id = e.Id,
                Name = e.Name,
                Keys = e.Keys.Where(k => k.ExpireOn == null || k.ExpireOn > DateTime.UtcNow).Select(k => new EnvironmentKeyDto
                {
                    Id = k.Id,
                    KeyPrefix = k.KeyPreview,
                    CreatedAt = k.CreatedOn
                }).ToList()
            }).ToList()
        };

        await Send.OkAsync(response, ct);
    }
}