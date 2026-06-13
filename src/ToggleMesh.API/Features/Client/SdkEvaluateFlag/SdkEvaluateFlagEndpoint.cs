using ToggleMesh.API.Features.Client.SdkEvaluateFlags;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlag;

public class SdkEvaluateFlagEndpoint : ToggleEndpoint<SdkEvaluateFlagsRequest, SdkEvaluateFlagResponse>
{
    private readonly ISdkEvaluatorService _evaluatorService;

    public SdkEvaluateFlagEndpoint(ISdkEvaluatorService evaluatorService)
    {
        _evaluatorService = evaluatorService;
    }

    public override void Configure()
    {
        Post("/sdk/evaluate/{flagKey}");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<SdkEvaluateFlagsRequest>>();
    }

    public override async Task HandleAsync(SdkEvaluateFlagsRequest req, CancellationToken ct)
    {
        var flagKey = Route<string>("flagKey");
        if (string.IsNullOrEmpty(flagKey))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var isClientSideRequest = req.KeyType == KeyType.Client;
        var compiledStates = await _evaluatorService.GetCompiledFlagsAsync(req.EnvId, ct);
        var state = compiledStates.FirstOrDefault(x => x.Key == flagKey);

        if (state is null || (isClientSideRequest && !state.IsClientSideExposed))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var result = _evaluatorService.Evaluate(state, req.Identity, req.Context);
        await Send.OkAsync(new SdkEvaluateFlagResponse(flagKey, result), ct);
    }
}