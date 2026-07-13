using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Integrations.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Extensions;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Integrations.GetIntegrations;

public class GetIntegrationsEndpoint : Endpoint<GetIntegrationsRequest, List<IntegrationDto>>
{
    private readonly AppDbContext _db;

    public GetIntegrationsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/integrations");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsView);
    }

    public override async Task HandleAsync(GetIntegrationsRequest req, CancellationToken ct)
    {
        var integrations = await _db.Integrations
            .AsNoTracking()
            .Where(i => i.ProjectId == req.ProjectId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        var dtos = integrations.Select(i => new IntegrationDto(
            i.Id,
            i.ProjectId,
            i.Provider,
            i.Name,
            "*** HIDDEN ***",
            i.Events,
            i.EnvironmentIds,
            i.IsActive
        )).ToList();

        await Send.OkAsync(dtos, ct);
    }
}
