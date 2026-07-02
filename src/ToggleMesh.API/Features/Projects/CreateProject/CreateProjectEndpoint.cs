using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

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
        if (req.OrganizationId == Guid.Empty)
            req.OrganizationId = await _db.OrganizationMembers
                .Where(om => om.UserId == UserId)
                .Select(om => om.OrganizationId)
                .FirstOrDefaultAsync(ct);

        var isMember = await _db.OrganizationMembers
            .AnyAsync(om => om.OrganizationId == req.OrganizationId && om.UserId == UserId, ct);

        if (!isMember)
            ThrowError("You must be a member of the organization to create a project.", 403);

        var project = new Project
        {
            Name = req.Name,
            OrganizationId = req.OrganizationId
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