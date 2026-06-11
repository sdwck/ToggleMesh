using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;
using ToggleMesh.Common.Contexts;
using ToggleMesh.Common.Rules;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlags;

public class SdkEvaluateFlagsEndpoint : ToggleEndpoint<SdkEvaluateFlagsRequest, List<SdkEvaluateFlagsResponse>>
{
    private readonly AppDbContext _db;
    private readonly IRuleEngine _ruleEngine;
    private static readonly List<string> DefaultIdentityKeys 
        = ["UserId", "sub", "Email", "SessionId", "DeviceId", "Id"];

    public SdkEvaluateFlagsEndpoint(AppDbContext db, IRuleEngine ruleEngine)
    {
        _db = db;
        _ruleEngine = ruleEngine;
    }

    public override void Configure()
    {
        Post("/sdk/evaluate");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<SdkEvaluateFlagsRequest>>();
    }

    public override async Task HandleAsync(SdkEvaluateFlagsRequest req, CancellationToken ct)
    {
        var isClientSideRequest = req.KeyType == KeyType.Client;
        var states = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .Include(x => x.FeatureFlag)
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == req.EnvId
                        && (!isClientSideRequest || x.FeatureFlag.IsClientSideExposed))
            .ToListAsync(ct);

        var accessor = new ContextAccessor<Dictionary<string, string>>(req.Context);
        var evalContext = new EvaluationContext<ContextAccessor<Dictionary<string, string>>>(
            accessor, 
            [], 
            DefaultIdentityKeys);

        var response = new List<SdkEvaluateFlagsResponse>();

        foreach (var state in states)
        {
            var flagKey = state.FeatureFlag.Key;
            var isEnabled = state.IsEnabled;

            if (!isEnabled)
            {
                response.Add(new SdkEvaluateFlagsResponse(flagKey, false));
                continue;
            }

            var ruleDtos = state.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value));
            var groups = _ruleEngine.CompileRules(ruleDtos);

            if (!_ruleEngine.Evaluate(groups, ref evalContext))
            {
                response.Add(new SdkEvaluateFlagsResponse(flagKey, false));
                continue;
            }

            var actualIdentity = evalContext.GetIdentity(req.Identity);
            var result = RolloutEvaluator.Evaluate(state.RolloutPercentage, flagKey, actualIdentity);
            
            response.Add(new SdkEvaluateFlagsResponse(flagKey, result));
        }

        await Send.OkAsync(response, ct);
    }
}