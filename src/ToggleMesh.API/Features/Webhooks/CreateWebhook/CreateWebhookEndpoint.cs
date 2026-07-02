using System.Security.Cryptography;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Webhooks.CreateWebhook;

public class CreateWebhookEndpoint : ToggleEndpoint<CreateWebhookRequest>
{
    private readonly AppDbContext _db;
    private readonly IAesEncryptionService _encryption;

    public CreateWebhookEndpoint(AppDbContext db, IAesEncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/webhooks");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.WebhooksCreate);
    }

    public override async Task HandleAsync(CreateWebhookRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var secretBytes = new byte[32];
        RandomNumberGenerator.Fill(secretBytes);
        var plainSecret = "whsec_" + Convert
            .ToBase64String(secretBytes)
            .Replace("+", "")
            .Replace("/", "")
            .TrimEnd('=');

        var encryptedSecret = "v1:" + _encryption.Encrypt(plainSecret);

        if (!await SsrfValidator.IsSafeUrlAsync(req.Url, ct))
            ThrowError("The provided URL is invalid or points to a restricted internal network address.", 400);

        var hook = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Name = req.Name,
            Url = req.Url,
            SecretKey = encryptedSecret,
            Status = WebhookStatus.Active,
            EnvironmentIds = req.EnvironmentIds,
            Events = req.Events,
            FlagTags = req.FlagTags
        };

        _db.Webhooks.Add(hook);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new
        {
            hook.Id,
            hook.ProjectId,
            hook.Name,
            hook.Url,
            SecretKey = plainSecret,
            hook.Status,
            hook.EnvironmentIds,
            hook.Events,
            hook.FlagTags,
            hook.ConsecutiveFailures,
            hook.LastTriggeredAt
        }, ct);
    }
}