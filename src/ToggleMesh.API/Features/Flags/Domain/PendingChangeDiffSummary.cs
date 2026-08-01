namespace ToggleMesh.API.Features.Flags.Domain;

public class PendingChangeDiffSummary
{
    public bool IsEnabledChanged { get; set; }
    public bool? NewIsEnabled { get; set; }
    
    public bool FallthroughRolloutChanged { get; set; }
    public List<VariationWeight>? NewFallthroughRollout { get; set; }

    public List<RuleInput> AddedRules { get; set; } = [];
    public List<RuleInput> ModifiedRules { get; set; } = [];
    public List<RuleInput> DeletedRules { get; set; } = [];

    public bool IndividualTargetsChanged { get; set; }
    public int NewIndividualTargetsCount { get; set; }
}
