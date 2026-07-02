using System.Text.RegularExpressions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Sse;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public partial class IngestEventsEndpoint : ToggleEndpoint<IngestEventsRequest>
{
    private readonly IAnalyticsEventPublisher _publisher;
    private readonly ISseService _sseService;

    public IngestEventsEndpoint(IAnalyticsEventPublisher publisher, ISseService sseService)
    {
        _publisher = publisher;
        _sseService = sseService;
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
        Options(x => x.Add(b => b.Metadata.Add(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(maxPayloadSize))));
    }

    public override async Task HandleAsync(IngestEventsRequest req, CancellationToken ct)
    {
        if (req.Events == null || req.Events.Count == 0)
        {
            await Send.OkAsync(cancellation: ct);
            return;
        }

        foreach (var evt in req.Events)
        {
            if (!string.IsNullOrEmpty(evt.Identity) && EmailRegex().IsMatch(evt.Identity))
            {
                AddError("PII detected in Identity field. Please hash your identifiers or use UUIDs.");
                await Send.ErrorsAsync(400, cancellation: ct);
                return;
            }
        }

        await _publisher.PublishBatchAsync(req.EnvId, req.Events, ct);

        var livetailTopic = $"livetail:{req.EnvId}";
        foreach (var evt in req.Events)
        {
            await _sseService.BroadcastAsync(livetailTopic, livetailTopic, evt);
        }
        
        HttpContext.Response.StatusCode = 202;
        await HttpContext.Response.CompleteAsync();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
