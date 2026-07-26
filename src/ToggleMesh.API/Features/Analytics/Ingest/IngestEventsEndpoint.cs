using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.API.Features.Analytics.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public partial class IngestEventsEndpoint : ToggleEndpoint<IngestEventsRequest>
{
    private readonly IAnalyticsEventPublisher _publisher;
    private readonly ISseService _sseService;
    private readonly IServiceProvider _sp;
    private readonly IIdentityHasher _identityHasher;
    private readonly IPropertySanitizer _propertySanitizer;

    public IngestEventsEndpoint(
        IAnalyticsEventPublisher publisher, 
        ISseService sseService, 
        IServiceProvider sp, 
        IIdentityHasher identityHasher,
        IPropertySanitizer propertySanitizer)
    {
        _publisher = publisher;
        _sseService = sseService;
        _sp = sp;
        _identityHasher = identityHasher;
        _propertySanitizer = propertySanitizer;
    }

    public override void Configure()
    {
        Post("/sdk/events");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<IngestEventsRequest>>();
        Options(x => x.RequireCors("PublicSdk"));
        Options(x => x.RequireRateLimiting("sdk"));
        
        var maxPayloadSize = Config.GetValue<long>("Ingestion:MaxPayloadSizeBytes", 5242880);
        Options(x => x.Add(b => b.Metadata.Add(new RequestSizeLimitAttribute(maxPayloadSize))));
    }

    public override async Task HandleAsync(IngestEventsRequest req, CancellationToken ct)
    {
        if (req.Events.Count == 0)
        {
            await Send.OkAsync(cancellation: ct);
            return;
        }

        var blockPii = Config.GetValue<bool>("Analytics:BlockPiiEmails", true);
        
        if (blockPii)
        {
            var piiEvent = req.Events.FirstOrDefault(evt => 
                !string.IsNullOrEmpty(evt.Identity) && 
                EmailRegex().IsMatch(evt.Identity));
                
            if (piiEvent != null)
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var env = await db.Environments.FindAsync([req.EnvId], ct);
                if (env != null)
                {
                    var maskedIdentity = MaskEmail(piiEvent.Identity);
                    var contextObj = new 
                    {
                        timestamp = DateTimeOffset.UtcNow,
                        type = piiEvent.Type.ToString(),
                        eventName = piiEvent.EventName,
                        flagKey = piiEvent.FlagKey,
                        maskedIdentity
                    };
                    
                    env.LastPiiBlockedContext = JsonSerializer.Serialize(contextObj);
                    await db.SaveChangesAsync(ct);
                }

                Logger.LogWarning("Analytics batch rejected for environment {EnvId}: PII (email) detected in Identity field. " +
                                  "To disable this check, set 'Analytics__BlockPiiEmails=false'.", req.EnvId);
                                  
                AddError("PII detected in Identity field. Please hash your identifiers or use UUIDs.");
                await Send.ErrorsAsync(statusCode: 400, cancellation: ct);
                return;
            }
        }

        foreach (var evt in req.Events)
            evt.Properties = _propertySanitizer.Sanitize(evt.Properties);

        await _publisher.PublishBatchAsync(req.EnvId, req.Events, ct);

        var livetailTopic = $"livetail:{req.EnvId}";
        foreach (var evt in req.Events)
        {
            var sseEvt = new RawAnalyticsEventDto
            {
                Type = evt.Type,
                Timestamp = evt.Timestamp,
                Identity = _identityHasher.FormatLiveTailIdentity(evt.Identity),
                FlagKey = evt.FlagKey,
                VariationId = evt.VariationId,
                VariationValue = evt.VariationValue,
                EventName = evt.EventName,
                Value = evt.Value,
                Properties = evt.Properties
            };
            await _sseService.BroadcastAsync(livetailTopic, livetailTopic, sseEvt);
        }
        
        HttpContext.Response.StatusCode = 202;
        await HttpContext.Response.CompleteAsync();
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return email;
        var name = parts[0];
        if (name.Length <= 1) return $"*@{parts[1]}";
        return $"{name[0]}***@{parts[1]}";
    }



    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
