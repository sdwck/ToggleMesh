namespace ToggleMesh.API.Features.Analytics.Simulate;

public class SimulateExperimentRequest
{
    public string EventName { get; set; } = string.Empty;
    public int ParticipantsCount { get; set; } = 10000;
    public List<SimulationVariantDto> Variations { get; set; } = [];
    public Dictionary<string, string[]> ContextProperties { get; set; } = new();
}