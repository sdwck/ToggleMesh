using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Segments.Domain;

namespace ToggleMesh.API.Features.Flags.Get;

public record GetFlagResponse(
    string Key, 
    bool IsEnabled, 
    IEnumerable<RuleInput> Rules, 
    IEnumerable<string> Tags, 
    Guid? OffVariationId = null,
    IEnumerable<VariationWeight>? FallthroughRollout = null,
    long TrueCount = 0, 
    long FalseCount = 0, 
    bool IsMabEnabled = false, 
    string? MabGoalEvent = null, 
    bool IsExperimentActive = false, 
    IEnumerable<SegmentDto>? Segments = null, 
    MabOptimizationType MabOptimizationType = MabOptimizationType.Conversion, 
    string[]? ContextPartitionKeys = null, 
    Dictionary<string, IEnumerable<VariationWeight>>? ContextualRollouts = null,
    List<VariationDto>? Variations = null,
    int MabExplorationFloor = 5,
    Dictionary<string, Guid>? IndividualTargets = null,
    FlagType Type = FlagType.Boolean,
    bool IsSrmAlertSent = false,
    double? SrmPValue = null);

