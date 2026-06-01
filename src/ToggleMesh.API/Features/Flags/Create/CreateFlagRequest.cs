using ToggleMesh.API.Features.Flags.Get;

namespace ToggleMesh.API.Features.Flags.Create;

public class CreateFlagRequest
{
    public Guid EnvironmentId { get; set; }
    public string Key { get; set; } = string.Empty;
    public List<RuleDto> Rules { get; set; } = new();
    public int? RolloutPercentage { get; set; }
}