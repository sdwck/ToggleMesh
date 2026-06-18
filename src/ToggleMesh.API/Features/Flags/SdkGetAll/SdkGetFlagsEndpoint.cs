using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public class SdkGetFlagsEndpoint : ToggleEndpoint<SdkGetFlagsRequest, List<GetFlagResponse>>
{
    private readonly AppDbContext _db;
    private readonly HybridCache _cache;

    public SdkGetFlagsEndpoint(AppDbContext db, HybridCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public override void Configure()
    {
        Get("/sdk/flags");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<SdkGetFlagsRequest>>();
        Options(x => x.RequireCors("PublicSdk"));
        Options(x => x.RequireRateLimiting("sdk"));
    }

    public override async Task HandleAsync(SdkGetFlagsRequest req, CancellationToken ct)
    {
        if (req.KeyType == KeyType.Client)
        {
            AddError("Client keys are not supported.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var cacheKey = $"sdk:flags:states:{req.EnvId}";

        var states = await _cache.GetOrCreateAsync(cacheKey, async ct1 =>
        {
            return await _db.FlagEnvironmentStates
                .AsNoTracking()
                .Include(x => x.FeatureFlag)
                .Include(x => x.Rules)
                .Where(x => x.EnvironmentId == req.EnvId)
                .Select(state => new GetFlagResponse(
                    state.FeatureFlag.Key,
                    state.IsEnabled,
                    state.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
                    state.FeatureFlag.Tags,
                    state.RolloutPercentage,
                    state.TrueCount,
                    state.FalseCount))
                .ToListAsync(ct1);
        }, cancellationToken: ct);

        await Send.OkAsync(states, ct);
    }
}