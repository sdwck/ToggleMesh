using ToggleMesh.API.Features.Client.Domain;
using ToggleMesh.API.Features.Client.SdkEvaluateFlag;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlags;

public class SdkEvaluateFlagsEndpoint : ToggleEndpoint<SdkEvaluateFlagsRequest, List<SdkEvaluateFlagResponse>>
{
    private readonly ISdkEvaluatorService _evaluatorService;

    public SdkEvaluateFlagsEndpoint(ISdkEvaluatorService evaluatorService)
    {
        _evaluatorService = evaluatorService;
    }

    public override void Configure()
    {
        Post("/sdk/evaluate");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<SdkEvaluateFlagsRequest>>();
        Options(x => x.RequireCors("PublicSdk"));
        Options(x => x.RequireRateLimiting("sdk"));
    }

    public override async Task HandleAsync(SdkEvaluateFlagsRequest req, CancellationToken ct)
    {
        var isClientSideRequest = req.KeyType == KeyType.Client;

        var compiledStates = await _evaluatorService.GetCompiledFlagsAsync(req.EnvId, ct);
        var response = new List<SdkEvaluateFlagResponse>(compiledStates.Count);

        foreach (var state in compiledStates)
        {
            if (isClientSideRequest && !state.IsClientSideExposed)
                continue;

            var result = _evaluatorService.Evaluate(state, req.Identity, req.Context);
            response.Add(new SdkEvaluateFlagResponse(state.Key, result?.VariationId, result?.VariationValue, state.IsExperimentActive));
        }

        await Send.OkAsync(response, ct);
    }
}
