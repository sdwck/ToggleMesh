namespace ToggleMesh.API.Features.Analytics.Simulate;

public class SimulationVariantDto
{
    public Guid VariationId { get; set; }
    public double ConversionRate { get; set; }
    public double? Value { get; set; }
}