using ToggleMesh.API.Features.Client.SdkEvaluateFlags;

namespace ToggleMesh.API.Features.Client.Domain;

public interface ISdkEvaluatorService
{
    Task<List<CompiledFlagState>> GetCompiledFlagsAsync(Guid envId, CancellationToken ct);
    EvaluationResult? Evaluate(CompiledFlagState state, string identity, Dictionary<string, string> context);
}
