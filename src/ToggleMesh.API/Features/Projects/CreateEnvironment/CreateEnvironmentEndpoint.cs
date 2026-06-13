using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Projects.CreateEnvironment;

public class CreateEnvironmentEndpoint : ToggleEndpoint<CreateEnvironmentRequest, CreateEnvironmentResponse>
{
    private readonly AppDbContext _db;

    public CreateEnvironmentEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.EnvironmentsCreate);
    }

    public override async Task HandleAsync(CreateEnvironmentRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var projectExists = await _db.Projects
            .AnyAsync(p => p.Id == projectId, ct);
        
        if (!projectExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var maxSortOrder = await _db.Environments
            .Where(e => e.ProjectId == projectId)
            .MaxAsync(e => (int?)e.SortOrder, ct) ?? -1;

        var env = new ProjectEnvironment
        {
            ProjectId = projectId,
            Name = req.Name,
            SortOrder = maxSortOrder + 1
        };

        _db.Environments.Add(env);
        
        var existingFlags = await _db.FeatureFlags.Where(f => f.ProjectId == projectId).ToListAsync(ct);
        foreach (var flag in existingFlags)
        {
            _db.FlagEnvironmentStates.Add(new Flags.FlagEnvironmentState
            {
                FeatureFlag = flag,
                Environment = env,
                IsEnabled = false
            });
        }
        
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new CreateEnvironmentResponse
        {
            Id = env.Id,
            Name = env.Name
        }, ct);
    }
}