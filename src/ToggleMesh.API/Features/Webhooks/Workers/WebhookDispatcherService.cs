using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Features.Integrations.Domain;
using ToggleMesh.API.Features.Integrations.Formatters;
using ToggleMesh.API.Infrastructure.Security;
using System.Text;

namespace ToggleMesh.API.Features.Webhooks.Workers;

public class WebhookDispatcherService : BackgroundService
{
    private readonly Channel<WebhookEvent> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookDispatcherService> _logger;
    private readonly TimeProvider _timeProvider;

    public WebhookDispatcherService(
        Channel<WebhookEvent> channel,
        IServiceProvider serviceProvider,
        ILogger<WebhookDispatcherService> logger,
        TimeProvider timeProvider)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await QueueDeliveriesAsync(evt, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred while queuing webhook event.");
            }
        }
    }

    private async Task QueueDeliveriesAsync(WebhookEvent webhookEvent, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var webhooks = await db.Webhooks
            .AsNoTracking()
            .Where(w => w.ProjectId == webhookEvent.ProjectId && w.Status == WebhookStatus.Active)
            .ToListAsync(ct);

        var project = await db.Projects
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(p => 
                p.Id == webhookEvent.ProjectId, ct);

        if (project == null)
            return;

        string? envName = null;
        if (webhookEvent.EnvironmentId.HasValue)
            envName = await db.Environments
                .AsNoTracking()
                .Where(e => 
                    e.Id == webhookEvent.EnvironmentId.Value)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(ct);

        string[]? flagTags = null;
        if (webhooks.Any(w => w.FlagTags.Length > 0) && !string.IsNullOrEmpty(webhookEvent.FlagKey))
        {
            flagTags = await db.FeatureFlags
                .AsNoTracking()
                .Where(f => f.ProjectId == webhookEvent.ProjectId && f.Key == webhookEvent.FlagKey)
                .Select(f => f.Tags)
                .FirstOrDefaultAsync(ct);
        }

        foreach (var webhook in webhooks)
        {
            if (webhook.Events.Length == 0)
                continue;
            
            if (!webhook.Events.Contains(webhookEvent.EventName))
                continue;
            
            if (webhookEvent.EnvironmentId.HasValue 
                && webhook.EnvironmentIds.Length > 0 
                && !webhook.EnvironmentIds
                    .Contains(webhookEvent.EnvironmentId.Value))
                continue;

            if (webhook.FlagTags.Length > 0)
            {
                if (flagTags == null || flagTags.Length == 0)
                    continue;
                
                if (!webhook.FlagTags.Intersect(flagTags).Any())
                    continue;
            }

            await CreateDeliveryAsync(
                webhook, 
                webhookEvent, 
                project.Name, 
                envName, 
                db, 
                ct);
        }
        
        await DispatchIntegrationsAsync(webhookEvent, project.Name, envName, db, scope.ServiceProvider, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task CreateDeliveryAsync(
        Webhook webhook, 
        WebhookEvent webhookEvent, 
        string projectName,
        string? envName,
        AppDbContext db, 
        CancellationToken ct)
    {
        object? data = null;

        if (webhookEvent.EventName != "flag.deleted" && webhookEvent.EnvironmentId.HasValue)
        {
            var state = await db.FlagEnvironmentStates
                .AsNoTracking()
                .Include(x => x.FeatureFlag)
                    .ThenInclude(f => f.Variations)
                .Include(x => x.Rules)
                .Include(x => x.IndividualTargets)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.EnvironmentId == webhookEvent.EnvironmentId.Value && x.FeatureFlag.Key == webhookEvent.FlagKey, ct);

            if (state != null)
            {
                var baseData = new Dictionary<string, object?>
                {
                    ["key"] = state.FeatureFlag.Key,
                    ["type"] = state.FeatureFlag.Type.ToString(),
                    ["tags"] = state.FeatureFlag.Tags,
                    ["isClientSideExposed"] = state.FeatureFlag.IsClientSideExposed,
                    ["rules"] = state.Rules.Select(r => new { r.GroupId, r.Attribute, r.Operator, r.Value }).ToList(),
                    ["individualTargets"] = state.IndividualTargets.Select(t => new { t.IdentityKey, t.VariationId }).ToList()
                };

                if (state.FeatureFlag.Type == Flags.Domain.FlagType.Boolean)
                    baseData["isEnabled"] = state.IsEnabled;
                else
                {
                    baseData["variations"] = state.FeatureFlag.Variations.OrderBy(v => v.Sequence).Select(v => new { v.Id, v.Value }).ToList();
                    baseData["defaultRollout"] = state.FallthroughRollout;
                }

                data = baseData;
            }
        }
        else
        {
            var flag = await db.FeatureFlags
                .AsNoTracking()
                .Include(f => f.Variations)
                .FirstOrDefaultAsync(x => x.ProjectId == webhookEvent.ProjectId && x.Key == webhookEvent.FlagKey, ct);

            if (flag != null)
            {
                var baseData = new Dictionary<string, object?>
                {
                    ["key"] = flag.Key,
                    ["type"] = flag.Type.ToString(),
                    ["tags"] = flag.Tags,
                    ["isClientSideExposed"] = flag.IsClientSideExposed
                };

                if (flag.Type != Flags.Domain.FlagType.Boolean)
                    baseData["variations"] = flag.Variations
                        .OrderBy(v => v.Sequence)
                        .Select(v => new { v.Id, v.Value })
                        .ToList();

                data = baseData;
            }
        }

        var payloadObj = new
        {
            id = Guid.CreateVersion7(),
            timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            eventName = webhookEvent.EventName,
            projectId = webhookEvent.ProjectId,
            projectName,
            environmentId = webhookEvent.EnvironmentId,
            environmentName = envName,
            flagKey = webhookEvent.FlagKey,
            contextMessage = webhookEvent.ContextMessage,
            data
        };

        var delivery = new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            WebhookId = webhook.Id,
            EventName = webhookEvent.EventName,
            Payload = JsonSerializer.Serialize(payloadObj),
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = _timeProvider.GetUtcNow().UtcDateTime,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        db.WebhookDeliveries.Add(delivery);
    }

    private async Task DispatchIntegrationsAsync(
        WebhookEvent webhookEvent, 
        string projectName,
        string? envName,
        AppDbContext db,
        IServiceProvider sp,
        CancellationToken ct)
    {
        var integrations = await db.Integrations
            .AsNoTracking()
            .Where(i => i.ProjectId == webhookEvent.ProjectId && i.IsActive)
            .ToListAsync(ct);

        if (integrations.Count == 0)
            return;

        var encryptionService = sp.GetRequiredService<IAesEncryptionService>();
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var config = sp.GetRequiredService<IConfiguration>();
        var adminBaseUrl = config["App:AdminBaseUrl"]?.TrimEnd('/');

        var evt = new IntegrationEvent(
            webhookEvent.EventName,
            projectName,
            envName,
            webhookEvent.FlagKey,
            null,
            _timeProvider.GetUtcNow(),
            adminBaseUrl,
            webhookEvent.ContextMessage
        );
        
        var dispatchTasks = new List<Task>();

        foreach (var integration in integrations)
        {
            if (integration.Events.Length > 0 && !integration.Events.Contains(webhookEvent.EventName))
                continue;

            if (webhookEvent.EnvironmentId.HasValue && 
                integration.EnvironmentIds.Length > 0 && 
                !integration.EnvironmentIds.Contains(webhookEvent.EnvironmentId.Value))
                continue;

            IIntegrationFormatter formatter = integration.Provider switch
            {
                IntegrationProvider.Slack => new SlackFormatter(),
                IntegrationProvider.Discord => new DiscordFormatter(),
                IntegrationProvider.MicrosoftTeams => new TeamsFormatter(),
                _ => throw new NotImplementedException()
            };

            var payload = formatter.FormatMessage(evt);
            var webhookUrl = encryptionService.Decrypt(integration.WebhookUrl);

            dispatchTasks.Add(Task.Run(async () =>
            {
                try
                {
                    var client = httpClientFactory.CreateClient("IntegrationClient");
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    await client.PostAsync(webhookUrl, content, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch integration {IntegrationId} to {Provider}", integration.Id, integration.Provider);
                }
            }, ct));
        }
        
        if (dispatchTasks.Count > 0)
            await Task.WhenAll(dispatchTasks);
    }
}
