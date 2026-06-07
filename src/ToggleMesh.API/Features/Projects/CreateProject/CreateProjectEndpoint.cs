using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Projects.CreateProject;

public class CreateProjectEndpoint : ToggleEndpoint<CreateProjectRequest, CreateProjectResponse>
{
    private readonly AppDbContext _db;

    public CreateProjectEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects");
        Version(1);
    }

    public override async Task HandleAsync(CreateProjectRequest req, CancellationToken ct)
    {
        var project = new Project
        {
            Name = req.Name
        };

        _db.Projects.Add(project);

        var projectMember = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = UserId,
            Role = ProjectRole.Owner,
            Project = project
        };

        _db.ProjectMembers.Add(projectMember);

        var devEnv = new ProjectEnvironment
        {
            Name = "Development",
            Project = project,
            ProjectId = project.Id,
            SortOrder = 0
        };
        var prodEnv = new ProjectEnvironment
        {
            Name = "Production",
            Project = project,
            ProjectId = project.Id,
            SortOrder = 1
        };

        _db.Environments.AddRange(devEnv, prodEnv);

        await _db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<GetProject.GetProjectEndpoint>(
            routeValues: new { projectId = project.Id },
            responseBody: new CreateProjectResponse { Id = project.Id, Name = project.Name },
            cancellation: ct);
    }
}