using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public class SdkGetFlagsEndpoint : Endpoint<SdkGetFlagsRequest, List<GetFlagResponse>>
{
    private readonly AppDbContext _db;
    private readonly IApiKeyCacheService _apiKeyCache;

    public SdkGetFlagsEndpoint(AppDbContext db, IApiKeyCacheService apiKeyCache)
    {
        _db = db;
        _apiKeyCache = apiKeyCache;
    }

    public override void Configure()
    {
        Get("/sdk/flags");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(SdkGetFlagsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ApiKey))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var envId = await _apiKeyCache.GetEnvironmentIdAsync(req.ApiKey, ct);

        if (envId == null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var flags = await _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == envId.Value)
            .Select(x => new GetFlagResponse(
                x.Key, 
                x.IsEnabled, 
                x.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
                x.RolloutPercentage))
            .ToListAsync(ct);

        await Send.OkAsync(flags, ct);
    }
}