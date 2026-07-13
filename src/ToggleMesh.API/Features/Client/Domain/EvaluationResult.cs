namespace ToggleMesh.API.Features.Client.Domain;

public readonly struct EvaluationResult
{
    public Guid VariationId { get; }
    public string VariationValue { get; }

    public EvaluationResult(Guid variationId, string variationValue)
    {
        VariationId = variationId;
        VariationValue = variationValue;
    }
}
