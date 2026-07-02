using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Analytics.GetUniqueEvents;

public class GetUniqueEventsEndpoint : ToggleEndpoint<GetUniqueEventsRequest, List<string>>
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;

    public GetUniqueEventsEndpoint(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/environments/{environmentId}/analytics/events");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(GetUniqueEventsRequest req, CancellationToken ct)
    {
        var isValid = await _db.Environments
            .AnyAsync(e => e.Id == req.EnvironmentId && e.ProjectId == req.ProjectId, ct);

        if (!isValid)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var redisEvents = await _redis.GetDatabase()
            .SetMembersAsync(CacheKeys.UniqueEvents(req.EnvironmentId));
        var events = redisEvents.Select(x => x.ToString()).ToList();

        await Send.OkAsync(events, ct);
    }
}
