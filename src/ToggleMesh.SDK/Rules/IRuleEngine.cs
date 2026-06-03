namespace ToggleMesh.SDK.Rules;

public interface IRuleEngine
{
    bool Evaluate(IEnumerable<RuleDto> rules, IDictionary<string, string> context);
}