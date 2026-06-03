using ToggleMesh.API.Features.Flags.Get;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagRequest
{
    public Guid EnvironmentId { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<RuleDto> Rules { get; set; } = [];
    public int? RolloutPercentage { get; set; }
}