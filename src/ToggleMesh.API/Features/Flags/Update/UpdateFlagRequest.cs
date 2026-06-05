using ToggleMesh.API.Features.Flags.Get;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagRequest
{
    public bool IsEnabled { get; set; }
    public List<RuleDto> Rules { get; set; } = [];
    public int? RolloutPercentage { get; set; }
}