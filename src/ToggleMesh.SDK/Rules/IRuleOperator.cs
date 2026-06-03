namespace ToggleMesh.SDK.Rules;

/// <summary>
/// Defines a custom rule operator for the ToggleMesh evaluation engine.
/// Implement this interface to add new targeting logic (e.g. "IsVip", "RegionMatches").
/// </summary>
public interface IRuleOperator
{
    /// <summary>
    /// Unique identifier of the operator.
    /// This name must match the operator key sent from the ToggleMesh Control Plane.
    /// Example: "Equals", "GreaterThan".
    /// </summary>
    string Name { get; }
    /// <summary>
    /// Evaluates whether the user's attribute satisfies the rule criteria.
    /// </summary>
    /// <param name="userValue">The value provided in the User Context.</param>
    /// <param name="ruleValue">The value defined in the Feature Flag rule.</param>
    /// <returns>True if the condition is met; otherwise, false.</returns>
    bool Evaluate(string userValue, string ruleValue);
}