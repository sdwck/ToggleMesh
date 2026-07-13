namespace ToggleMesh.API.Features.Analytics.GetContextualExperimentDetails;

public class ContextualExperimentVariationResultDto
{
    public Guid VariationId { get; set; }
    public long Exposures { get; set; }
    public long Conversions { get; set; }
    public double ConversionRate => Exposures > 0 ? (double)Conversions / Exposures : 0;
    
    public double TotalValue { get; set; }
    public double Arpu => Exposures > 0 ? TotalValue / Exposures : 0;
    
    public double ExpectedUplift { get; set; }
    public double ProbabilityToBeatBaseline { get; set; }
    
    public int RolloutWeight { get; set; }
}