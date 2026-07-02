using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Analytics.GetSchema;

public class GetAnalyticsSchemaEndpoint : ToggleEndpoint<GetAnalyticsSchemaRequest, GetAnalyticsSchemaResponse>
{
    private readonly IConnectionMultiplexer _redis;

    public GetAnalyticsSchemaEndpoint(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/environments/{environmentId}/analytics/schema");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(GetAnalyticsSchemaRequest req, CancellationToken ct)
    {
        var db = _redis.GetDatabase();

        var hasValue = false;
        if (!string.IsNullOrWhiteSpace(req.EventName))
            hasValue = await db.KeyExistsAsync(CacheKeys.EventSchemaHasValue(req.EnvironmentId, req.EventName));

        var contextKeys = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(req.FlagKey))
        {
            var keys = await db.SetMembersAsync(CacheKeys.FlagSchemaContextKeys(req.EnvironmentId, req.FlagKey));
            contextKeys = keys.Select(k => k.ToString()).OrderBy(k => k).ToArray();
        }

        await Send.OkAsync(new GetAnalyticsSchemaResponse(hasValue, contextKeys), ct);
    }
}
