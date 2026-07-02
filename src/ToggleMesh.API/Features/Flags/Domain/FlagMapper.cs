using ToggleMesh.API.Features.Flags.Get;

namespace ToggleMesh.API.Features.Flags.Domain;

public static class FlagMapper
{
    public static GetFlagResponse ToDto(this FlagEnvironmentState state)
    {
        return new GetFlagResponse(
            state.FeatureFlag.Key,
            state.IsEnabled,
            state.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
            state.FeatureFlag.Tags,
            state.RolloutPercentage,
            0L,
            0L,
            state.IsMabEnabled,
            state.MabGoalEvent,
            state.IsExperimentActive,
            null,
            state.MabOptimizationType,
            state.ContextPartitionKeys,
            state.ContextualRollouts?.ToDictionary(x => x.ContextSlice, x => x.RolloutPercentage)
        );
    }
}
