namespace ToggleMesh.API.Features.Analytics.GetExperimentDetails;

public class ExperimentResultDto
{
    public string EventName { get; set; } = null!;
    
    public long ControlExposures { get; set; }
    public long ControlConversions { get; set; }
    public double ControlConversionRate => ControlExposures > 0 ? (double)ControlConversions / ControlExposures : 0;
    
    public long TreatmentExposures { get; set; }
    public long TreatmentConversions { get; set; }
    public double TreatmentConversionRate => TreatmentExposures > 0 ? (double)TreatmentConversions / TreatmentExposures : 0;

    public double ExpectedUplift { get; set; }
    public double ProbabilityToBeatBaseline { get; set; }

    public double ControlTotalValue { get; set; }
    public double TreatmentTotalValue { get; set; }

    public double ControlARPU => ControlExposures > 0 ? ControlTotalValue / ControlExposures : 0;
    public double TreatmentARPU => TreatmentExposures > 0 ? TreatmentTotalValue / TreatmentExposures : 0;
    
    public double ExpectedValueUplift => ControlARPU > 0 ? (TreatmentARPU - ControlARPU) / ControlARPU : 0;
    public bool IsRevenueBased => ControlTotalValue > 0 || TreatmentTotalValue > 0;
    
    public DateTimeOffset LastCalculatedAt { get; set; }
}