using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Integrations.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Extensions;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Integrations.CreateIntegration;

public class CreateIntegrationEndpoint : Endpoint<CreateIntegrationRequest, IntegrationDto>
{
    private readonly AppDbContext _db;
    private readonly IAesEncryptionService _encryptionService;

    public CreateIntegrationEndpoint(AppDbContext db, IAesEncryptionService encryptionService)
    {
        _db = db;
        _encryptionService = encryptionService;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/integrations");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(CreateIntegrationRequest req, CancellationToken ct)
    {
        var projectExists = await _db.Projects
            .AnyAsync(p => p.Id == req.ProjectId, ct);
        
        if (!projectExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var integration = new Integration
        {
            Id = Guid.CreateVersion7(),
            ProjectId = req.ProjectId,
            Provider = req.Provider,
            Name = req.Name,
            WebhookUrl = _encryptionService.Encrypt(req.WebhookUrl),
            Events = req.Events,
            EnvironmentIds = req.EnvironmentIds,
            IsActive = true
        };

        _db.Integrations.Add(integration);
        await _db.SaveChangesAsync(ct);

        var dto = new IntegrationDto(
            integration.Id,
            integration.ProjectId,
            integration.Provider,
            integration.Name,
            req.WebhookUrl,
            integration.Events,
            integration.EnvironmentIds,
            integration.IsActive
        );

        await Send.CreatedAtAsync(
            $"/api/v1/projects/{req.ProjectId}/integrations/{integration.Id}", 
            null, 
            dto, 
            cancellation: ct);
    }
}
