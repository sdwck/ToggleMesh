using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.Common.Contexts;
using ToggleMesh.Common.Rules;
using ToggleMesh.Common.Rules.Operators;

namespace ToggleMesh.IntegrationTests.Sdk;

public class RuleEngineTests
{
    private readonly IRuleEngine _engine;

    public RuleEngineTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRuleOperator, EqualsOperator>();
        services.AddSingleton<IRuleOperator, NotEqualsOperator>();
        services.AddSingleton<IRuleOperator, ContainsOperator>();
        services.AddSingleton<IRuleOperator, StartsWithOperator>();
        services.AddSingleton<IRuleOperator, EndsWithOperator>();
        services.AddSingleton<IRuleOperator, GreaterThanOperator>();
        services.AddSingleton<IRuleOperator, GreaterThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, LessThanOperator>();
        services.AddSingleton<IRuleOperator, LessThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, InListOperator>();
        services.AddSingleton<IRuleOperator, DateAfterOperator>();
        services.AddSingleton<IRuleOperator, DateBeforeOperator>();
        services.AddSingleton<IRuleOperator, RegexOperator>();
        services.AddSingleton<IRuleOperator, SemVerEqualOperator>();
        services.AddSingleton<IRuleOperator, SemVerGreaterThanOperator>();
        services.AddSingleton<IRuleOperator, SemVerGreaterThanOrEqualOperator>();
        services.AddSingleton<IRuleOperator, SemVerLessThanOperator>();
        services.AddSingleton<IRuleOperator, SemVerLessThanOrEqualOperator>();
        services.AddSingleton<IRuleEngine, RuleEngine>();

        var provider = services.BuildServiceProvider();
        _engine = provider.GetRequiredService<IRuleEngine>();
    }

    private bool EvaluateHelper(IEnumerable<RuleDto> rules, IDictionary<string, string> context)
    {
        var compiledRules = _engine.CompileRules(rules);
        var accessor = new ContextAccessor<IDictionary<string, string>>(context);
        var evalContext = new EvaluationContext<ContextAccessor<IDictionary<string, string>>>(accessor, [], []);
        return _engine.Evaluate(compiledRules, ref evalContext);
    }

    [Theory]
    [InlineData("Equals", "admin", "admin", true)]
    [InlineData("Equals", "admin", "user", false)]
    [InlineData("NotEquals", "user", "admin", true)]
    [InlineData("NotEquals", "admin", "admin", false)]
    [InlineData("Contains", "boss@togglemesh.com", "@togglemesh.com", true)]
    [InlineData("Contains", "user@gmail.com", "@togglemesh.com", false)]
    [InlineData("StartsWith", "beta-tester-123", "beta-", true)]
    [InlineData("StartsWith", "alpha-tester-123", "beta-", false)]
    [InlineData("EndsWith", "image.png", ".png", true)]
    [InlineData("EndsWith", "image.jpg", ".png", false)]
    [InlineData("GreaterThan", "25", "18", true)]
    [InlineData("GreaterThan", "15", "18", false)]
    [InlineData("GreaterThanOrEqual", "18", "18", true)]
    [InlineData("LessThan", "15", "18", true)]
    [InlineData("LessThan", "25", "18", false)]
    [InlineData("LessThanOrEqual", "18", "18", true)]
    [InlineData("InList", "US", "US,CA,UK", true)]
    [InlineData("InList", "RU", "US,CA,UK", false)]
    [InlineData("InList", "uk", "US, CA, UK", true)]
    [InlineData("DateAfter", "2026-06-03", "2026-06-01", true)]
    [InlineData("DateBefore", "2026-06-01", "2026-06-03", true)]
    [InlineData("Regex", "user-123", "^user-\\d+$", true)]
    [InlineData("Regex", "admin", "^user-\\d+$", false)]
    [InlineData("SemVerEqual", "1.2.3", "1.2.3", true)]
    [InlineData("SemVerGreaterThan", "1.2.4", "1.2.3", true)]
    [InlineData("SemVerLessThan", "1.2.2", "1.2.3", true)]
    public void Evaluate_SingleRule_ShouldComputeCorrectly(string op, string userValue, string ruleValue, bool expectedResult)
    {
        // Arrange
        var rules = new List<RuleDto>
        {
            new(0, "CustomAttribute", op, ruleValue)
        };

        var context = new Dictionary<string, string>
        {
            { "CustomAttribute", userValue }
        };

        // Act
        var result = EvaluateHelper(rules, context);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void Evaluate_RuleGroups_ShouldUseOrBetweenGroups_AndAndWithinGroups()
    {
        // Arrange
        var rules = new List<RuleDto>
        {
            new(1, "Email", "EndsWith", "@company.com"),
            new(1, "Age", "GreaterThan", "21"),
            new(2, "Country", "Equals", "CA"),
            new(2, "Plan", "Equals", "Premium")
        };

        var contextGroup1Pass = new Dictionary<string, string>
        {
            { "Email", "user@company.com" },
            { "Age", "25" }
        };

        var contextGroup2Pass = new Dictionary<string, string>
        {
            { "Country", "CA" },
            { "Plan", "Premium" }
        };

        var contextBothGroupsFail = new Dictionary<string, string>
        {
            { "Email", "user@company.com" },
            { "Age", "18" },
            { "Country", "US" },
            { "Plan", "Premium" }
        };

        // Act & Assert
        EvaluateHelper(rules, contextGroup1Pass).Should().BeTrue();
        EvaluateHelper(rules, contextGroup2Pass).Should().BeTrue();
        EvaluateHelper(rules, contextBothGroupsFail).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MultipleRules_SameGroup_ShouldRequireAllToPass()
    {
        // Arrange
        var rules = new List<RuleDto>
        {
            new(0, "Email", "EndsWith", "@company.com"),
            new(0, "Age", "GreaterThan", "21"),
            new(0, "Role", "InList", "Admin,Manager")
        };

        var passingContext = new Dictionary<string, string>
        {
            { "Email", "ceo@company.com" },
            { "Age", "35" },
            { "Role", "Manager" }
        };

        var failingContext1 = new Dictionary<string, string>
        {
            { "Email", "ceo@company.com" },
            { "Age", "20" },
            { "Role", "Admin" }
        };

        var failingContext2 = new Dictionary<string, string>
        {
            { "Email", "ceo@company.com" },
            { "Age", "35" }
        };

        // Act & Assert
        EvaluateHelper(rules, passingContext).Should().BeTrue();
        EvaluateHelper(rules, failingContext1).Should().BeFalse();
        EvaluateHelper(rules, failingContext2).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NoRules_ShouldReturnTrue()
    {
        EvaluateHelper(new List<RuleDto>(), new Dictionary<string, string>()).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_UnknownOperator_ShouldReturnFalse()
    {
        // Arrange
        var rules = new List<RuleDto>
        {
            new(0, "Device", "MagicOperator", "iPhone")
        };

        var context = new Dictionary<string, string>
        {
            { "Device", "iPhone" }
        };

        // Act
        var result = EvaluateHelper(rules, context);

        // Assert
        result.Should().BeFalse();
    }
}
