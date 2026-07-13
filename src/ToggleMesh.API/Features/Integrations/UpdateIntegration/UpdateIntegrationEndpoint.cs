using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Integrations.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Extensions;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Integrations.UpdateIntegration;

public class UpdateIntegrationEndpoint : Endpoint<UpdateIntegrationRequest, IntegrationDto>
{
    private readonly AppDbContext _db;

    public UpdateIntegrationEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/projects/{projectId}/integrations/{id}");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(UpdateIntegrationRequest req, CancellationToken ct)
    {
        var integration = await _db.Integrations
            .FirstOrDefaultAsync(i => 
                i.Id == req.Id && i.ProjectId == req.ProjectId, ct);

        if (integration == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        integration.Name = req.Name;
        integration.Events = req.Events;
        integration.EnvironmentIds = req.EnvironmentIds;
        integration.IsActive = req.IsActive;

        await _db.SaveChangesAsync(ct);

        var dto = new IntegrationDto(
            integration.Id,
            integration.ProjectId,
            integration.Provider,
            integration.Name,
            "*** HIDDEN ***",
            integration.Events,
            integration.EnvironmentIds,
            integration.IsActive
        );

        await Send.OkAsync(dto, ct);
    }
}
