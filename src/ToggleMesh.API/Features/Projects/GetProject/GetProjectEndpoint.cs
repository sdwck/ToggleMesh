using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;

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
        this.RequirePermission(Auth.Models.Permissions.ProjectsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        (var role, var envRoles) = await _db.GetProjectRoleAndEnvOverridesAsync(projectId, UserId, ct);

        if (role == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Environments)
            .ThenInclude(e => e.Keys)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        var visibleEnvironments = project.Environments.Where(env => 
        {
            var effectiveRole = role.Value;
            if (envRoles.TryGetValue(env.Id, out var overrideRole))
                effectiveRole = overrideRole;
                
            return effectiveRole != ProjectRole.None;
        }).OrderBy(e => e.SortOrder).ToList();

        var response = new GetProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            UserRole = role.Value,
            CreatedAt = project.CreatedAt,
            Environments = visibleEnvironments.Select(e => 
            {
                var effectiveRole = role.Value;
                if (envRoles.TryGetValue(e.Id, out var overrideRole))
                    effectiveRole = overrideRole;
                    
                return new EnvironmentDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    UserRole = effectiveRole,
                    Keys = e.Keys.Where(k => k.ExpireOn == null || k.ExpireOn > DateTime.UtcNow).Select(k => new EnvironmentKeyDto
                    {
                        Id = k.Id,
                        KeyPrefix = k.KeyPreview,
                        KeyType = k.KeyType,
                        CreatedAt = k.CreatedOn
                    }).ToList()
                };
            }).ToList()
        };

        await Send.OkAsync(response, ct);
    }
}