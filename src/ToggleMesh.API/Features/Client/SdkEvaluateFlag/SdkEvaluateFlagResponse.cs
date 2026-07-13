namespace ToggleMesh.API.Features.Client.SdkEvaluateFlag;

public record SdkEvaluateFlagResponse(string Key, Guid? VariationId, string? VariationValue, bool IsExperimentActive);
