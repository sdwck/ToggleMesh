namespace ToggleMesh.Common;

/// <summary>
/// Pre-calculated optimization strategy determined by the ToggleMesh Compiler.
/// Tells the SDK exactly which execution path to take, eliminating runtime checks.
/// </summary>
public enum EvaluationStrategy
{
    /// <summary>
    /// Complex flag with both rules and rollouts. Full EvaluationContext required.
    /// </summary>
    Complex = 0,
    
    /// <summary>
    /// Flag has no rules but has a percentage rollout. Rules engine can be skipped.
    /// </summary>
    RolloutOnly = 1,
    
    /// <summary>
    /// Flag has rules, but no percentage rollouts (all weights are 100%). Hash calculation can be skipped.
    /// </summary>
    RulesOnly = 2,
    
    /// <summary>
    /// Flag has no rules and no rollouts. Result is entirely static based on IsEnabled.
    /// </summary>
    Static = 3
}
