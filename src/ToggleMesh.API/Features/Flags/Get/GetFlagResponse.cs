using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Segments.Domain;

namespace ToggleMesh.API.Features.Flags.Get;

public record GetFlagResponse(string Key, bool IsEnabled, IEnumerable<RuleDto> Rules, IEnumerable<string> Tags, int? RolloutPercentage = null, long TrueCount = 0, long FalseCount = 0, bool IsMabEnabled = false, string? MabGoalEvent = null, bool IsExperimentActive = false, IEnumerable<SegmentDto>? Segments = null, MabOptimizationType MabOptimizationType = MabOptimizationType.Conversion, string[]? ContextPartitionKeys = null, Dictionary<string, int>? ContextualRollouts = null);