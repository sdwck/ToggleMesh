namespace ToggleMesh.API.Features.Analytics.GetContextualExperimentDetails;

public class ContextualExperimentResultDto
{
    public string ContextSlice { get; set; } = null!;
    public string EventName { get; set; } = null!;
    public bool IsRevenueBased { get; set; }
    public DateTimeOffset LastCalculatedAt { get; set; }
    public bool IsAutoManaged { get; set; }
    public List<ContextualExperimentVariationResultDto> Variations { get; set; } = [];
}
