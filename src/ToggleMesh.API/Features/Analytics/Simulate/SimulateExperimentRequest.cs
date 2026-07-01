namespace ToggleMesh.API.Features.Analytics.Simulate;

public class SimulateExperimentRequest
{
    public string EventName { get; set; } = string.Empty;
    public int ParticipantsCount { get; set; } = 10000;
    public double ControlConversionRate { get; set; } = 0.05;
    public double TreatmentConversionRate { get; set; } = 0.08;
    public double? ControlValue { get; set; }
    public double? TreatmentValue { get; set; }
}