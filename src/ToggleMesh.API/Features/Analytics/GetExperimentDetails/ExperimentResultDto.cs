namespace ToggleMesh.API.Features.Analytics.GetExperimentDetails;

public class ExperimentResultDto
{
    public string EventName { get; set; } = null!;
    public bool IsRevenueBased { get; set; }
    public DateTimeOffset LastCalculatedAt { get; set; }
    public List<ExperimentVariationResultDto> Variations { get; set; } = [];
}
