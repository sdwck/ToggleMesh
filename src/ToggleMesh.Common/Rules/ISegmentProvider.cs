namespace ToggleMesh.Common.Rules;

public interface ISegmentProvider
{
    CompiledRuleGroup[]? GetSegmentRules(string segmentId);
}
