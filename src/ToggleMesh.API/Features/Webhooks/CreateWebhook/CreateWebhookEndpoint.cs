using System.Security.Cryptography;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Webhooks.CreateWebhook;

public class CreateWebhookEndpoint : ToggleEndpoint<CreateWebhookRequest>
{
    private readonly AppDbContext _db;

    public CreateWebhookEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/webhooks");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.WebhooksCreate);
    }

    public override async Task HandleAsync(CreateWebhookRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var secretBytes = new byte[32];
        RandomNumberGenerator.Fill(secretBytes);
        var secret = "whsec_" + Convert
            .ToBase64String(secretBytes)
            .Replace("+", "")
            .Replace("/", "")
            .TrimEnd('=');

        var hook = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Name = req.Name,
            Url = req.Url,
            SecretKey = secret,
            EnvironmentIds = req.EnvironmentIds,
            Events = req.Events,
            IsActive = true
        };

        _db.Webhooks.Add(hook);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(hook, ct);
    }
}