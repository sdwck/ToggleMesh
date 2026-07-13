using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Integrations.Domain;
using ToggleMesh.API.Features.Integrations.Formatters;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Extensions;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using System.Text.Json;
using System.Text;

namespace ToggleMesh.API.Features.Integrations.TestIntegration;

public class TestIntegrationEndpoint : Endpoint<TestIntegrationRequest>
{
    private readonly AppDbContext _db;
    private readonly IAesEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;

    public TestIntegrationEndpoint(
        AppDbContext db,
        IAesEncryptionService encryptionService,
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider)
    {
        _db = db;
        _encryptionService = encryptionService;
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/integrations/{id}/test");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(TestIntegrationRequest req, CancellationToken ct)
    {
        var integration = await _db.Integrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => 
                i.Id == req.Id && i.ProjectId == req.ProjectId, ct);

        if (integration == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct);
        if (project == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var evt = new IntegrationEvent(
            "integration.test",
            project.Name,
            null,
            null,
            User.Identity?.Name,
            _timeProvider.GetUtcNow(),
            null
        );

        IIntegrationFormatter formatter = integration.Provider switch
        {
            IntegrationProvider.Slack => new SlackFormatter(),
            IntegrationProvider.Discord => new DiscordFormatter(),
            IntegrationProvider.MicrosoftTeams => new TeamsFormatter(),
            _ => throw new NotImplementedException()
        };

        var payload = formatter.FormatMessage(evt);
        var webhookUrl = _encryptionService.Decrypt(integration.WebhookUrl);

        var client = _httpClientFactory.CreateClient("IntegrationClient");
        var content = new StringContent(
            JsonSerializer.Serialize(payload), 
            Encoding.UTF8, 
            "application/json");

        try
        {
            var response = await client.PostAsync(webhookUrl, content, ct);
            if (response.IsSuccessStatusCode)
                await Send.OkAsync(cancellation: ct);
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                ThrowError($"Failed to send test message. Status: {response.StatusCode}. Body: {errorBody}", 500);
            }
        }
        catch (Exception ex)
        {
            ThrowError($"Exception while sending test message: {ex.Message}", 500);
        }
    }
}
