using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.BackgroundServices.Webhooks;

public class WebhookDispatcherService : BackgroundService
{
    private readonly Channel<WebhookEvent> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatcherService> _logger;

    public WebhookDispatcherService(
        Channel<WebhookEvent> channel,
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcherService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DispatchEventAsync(evt, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while dispatching webhook event.");
            }
        }
    }

    private async Task DispatchEventAsync(WebhookEvent webhookEvent, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var webhooks = await db.Webhooks
            .AsNoTracking()
            .Where(w => w.ProjectId == webhookEvent.ProjectId && w.IsActive)
            .ToListAsync(ct);

        if (webhooks.Count == 0)
            return;

        var project = await db.Projects
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(p => p.Id == webhookEvent.ProjectId, ct);

        if (project == null)
            return;

        string? envName = null;
        if (webhookEvent.EnvironmentId.HasValue)
        {
            envName = await db.Environments
                .AsNoTracking()
                .Where(e => e.Id == webhookEvent.EnvironmentId.Value)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(ct);
        }

        var client = _httpClientFactory.CreateClient("WebhookClient");

        foreach (var webhook in webhooks)
        {
            if (webhook.Events.Length > 0 && 
                !webhook.Events.Contains(webhookEvent.EventName))
                continue;
            
            if (webhookEvent.EnvironmentId.HasValue 
                && webhook.EnvironmentIds.Length > 0 
                && !webhook.EnvironmentIds
                    .Contains(webhookEvent.EnvironmentId.Value))
                continue;

            await FireWebhookAsync(client, webhook, webhookEvent, project.Name, envName, db, ct);
        }
    }

    private async Task FireWebhookAsync(
        HttpClient client, 
        Webhook webhook, 
        WebhookEvent webhookEvent, 
        string projectName,
        string? envName,
        AppDbContext db, 
        CancellationToken ct)
    {
        try
        {
            object? data = null;

            if (webhookEvent.EventName != "flag.deleted")
            {
                if (webhookEvent.EnvironmentId.HasValue)
                {
                    var state = await db.FlagEnvironmentStates
                        .AsNoTracking()
                        .Include(x => x.FeatureFlag)
                        .Include(x => x.Rules)
                        .FirstOrDefaultAsync(x => x.EnvironmentId == webhookEvent.EnvironmentId.Value && x.FeatureFlag.Key == webhookEvent.FlagKey, ct);

                    if (state != null)
                    {
                        data = new
                        {
                            key = state.FeatureFlag.Key,
                            isEnabled = state.IsEnabled,
                            rolloutPercentage = state.RolloutPercentage,
                            tags = state.FeatureFlag.Tags,
                            isClientSideExposed = state.FeatureFlag.IsClientSideExposed,
                            rules = state.Rules.Select(r => new { r.GroupId, r.Attribute, r.Operator, r.Value }).ToList()
                        };
                    }
                }
                else
                {
                    var flag = await db.FeatureFlags
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.ProjectId == webhookEvent.ProjectId && x.Key == webhookEvent.FlagKey, ct);

                    if (flag != null)
                    {
                        data = new
                        {
                            key = flag.Key,
                            tags = flag.Tags,
                            isClientSideExposed = flag.IsClientSideExposed
                        };
                    }
                }
            }

            var payload = new
            {
                id = Guid.CreateVersion7(),
                timestamp = DateTime.UtcNow,
                eventName = webhookEvent.EventName,
                projectId = webhookEvent.ProjectId,
                projectName,
                environmentId = webhookEvent.EnvironmentId,
                environmentName = envName,
                flagKey = webhookEvent.FlagKey,
                data
            };

            var json = JsonSerializer.Serialize(payload);
            var payloadBytes = Encoding.UTF8.GetBytes(json);
            var secretBytes = Encoding.UTF8.GetBytes(webhook.SecretKey);

            using var hmac = new HMACSHA256(secretBytes);
            var hash = hmac.ComputeHash(payloadBytes);
            var signature = "sha256=" + Convert
                .ToHexString(hash)
                .ToLowerInvariant();

            var req = new HttpRequestMessage(HttpMethod.Post, new Uri(webhook.Url))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("x-togglemesh-signature", signature);

            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Webhook {Id} ({Name}) failed with status {Status}", webhook.Id, webhook.Name, resp.StatusCode);
            else
                await db.Webhooks.Where(w => w.Id == webhook.Id).ExecuteUpdateAsync(
                    s => s.SetProperty(w => w.LastTriggeredAt, DateTime.UtcNow), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to dispatch webhook {Id} to {Url}. Error: {Message}", webhook.Id, webhook.Url, ex.Message);
        }
    }
}