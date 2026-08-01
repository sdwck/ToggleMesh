using System.Diagnostics.Metrics;

namespace ToggleMesh.API.Infrastructure.Telemetry;

public static class ToggleMeshMetrics
{
    public const string MeterName = "ToggleMesh.API";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> FlagEvaluations = Meter.CreateCounter<long>(
        "togglemesh.flags.evaluations",
        description: "Total number of flag evaluations across local and server SDKs.");

    public static readonly UpDownCounter<long> ActiveSseConnections = Meter.CreateUpDownCounter<long>(
        "togglemesh.sdk.connections.active",
        description: "Current number of active SSE client connections.");

    public static readonly Counter<long> CacheRequests = Meter.CreateCounter<long>(
        "togglemesh.cache.requests",
        description: "Total Redis cache requests grouped by hit/miss.");

    public static readonly Counter<long> AnalyticsEventsIngested = Meter.CreateCounter<long>(
        "togglemesh.analytics.events.ingested",
        description: "Total analytics events received and processed.");

    public static readonly Counter<long> FlagUpdates = Meter.CreateCounter<long>(
        "togglemesh.flags.updated",
        description: "Total flag modifications from the Admin UI or API.");
}
