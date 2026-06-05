using Microsoft.EntityFrameworkCore;
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
        Policies($"Permission:{Auth.Models.Permissions.EnvironmentsCreate}");
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

        var env = new ProjectEnvironment
        {
            ProjectId = projectId,
            Name = req.Name
        };

        _db.Environments.Add(env);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new CreateEnvironmentResponse
        {
            Id = env.Id,
            Name = env.Name
        }, ct);
    }
}