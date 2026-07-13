using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.Common;

namespace ToggleMesh.API.Features.Flags.Domain;

public static class FlagMapper
{
    public static GetFlagResponse ToDto(this FlagEnvironmentState state)
    {
        return new GetFlagResponse(
            state.FeatureFlag.Key,
            state.IsEnabled,
            state.Rules.Select(r => new RuleInput(r.GroupId, r.Attribute, r.Operator, r.Value, r.Rollout.ToList())),
            state.FeatureFlag.Tags,
            state.OffVariationId,
            state.FallthroughRollout,
            0L,
            0L,
            state.IsMabEnabled,
            state.MabGoalEvent,
            state.IsExperimentActive,
            null,
            state.MabOptimizationType,
            state.ContextPartitionKeys,
            state.ContextualRollouts.ToDictionary(x => x.ContextSlice, x => (IEnumerable<VariationWeight>)x.Rollout),
            state.FeatureFlag.Variations.OrderBy(v => v.Sequence).Select(v => new VariationDto(v.Id, v.Value)).ToList(),
            state.MabExplorationFloor,
            state.IndividualTargets.ToDictionary(x => x.IdentityKey, x => x.VariationId),
            state.FeatureFlag.Type,
            state.IsSrmAlertSent,
            state.SrmPValue
        );

    }

    public static FeatureFlagDto ToSdkDto(this FlagEnvironmentState state)
    {
        return new FeatureFlagDto(
            state.FeatureFlag.Key,
            state.IsEnabled,
            state.Rules.Select(r => new Common.Rules.RuleDto(r.Priority, r.GroupId, r.Attribute, r.Operator, r.Value, r.Rollout.Select(w => new ToggleMesh.Common.VariationWeight(w.VariationId, w.Weight)).ToArray())),
            state.OffVariationId,
            state.FallthroughRollout.Select(w => new Common.VariationWeight(w.VariationId, w.Weight)),
            state.FeatureFlag.Variations.ToDictionary(v => v.Id, v => v.Value),
            state.IsExperimentActive,
            state.ContextualRollouts.ToDictionary(x => x.ContextSlice, x => (IEnumerable<Common.VariationWeight>)x.Rollout.Select(w => new Common.VariationWeight(w.VariationId, w.Weight)).ToList()),
            state.ContextPartitionKeys,
            state.IndividualTargets.ToDictionary(x => x.IdentityKey, x => x.VariationId)
        );
    }
}

