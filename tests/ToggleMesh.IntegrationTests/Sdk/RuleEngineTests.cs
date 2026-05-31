using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.SDK.Rules;
using ToggleMesh.SDK.Rules.Operators;

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
        services.AddSingleton<IRuleOperator, LessThanOperator>();
        services.AddSingleton<IRuleOperator, InListOperator>();
        services.AddSingleton<IRuleEngine, RuleEngine>();

        var provider = services.BuildServiceProvider();
        _engine = provider.GetRequiredService<IRuleEngine>();
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
    [InlineData("LessThan", "15", "18", true)]
    [InlineData("LessThan", "25", "18", false)]
    [InlineData("InList", "US", "US,CA,UK", true)]
    [InlineData("InList", "RU", "US,CA,UK", false)]
    [InlineData("InList", "uk", "US, CA, UK", true)]
    public void Evaluate_SingleRule_ShouldComputeCorrectly(string op, string userValue, string ruleValue, bool expectedResult)
    {
        // Arrange
        var rules = new List<RuleDto>
        {
            new("CustomAttribute", op, ruleValue)
        };

        var context = new Dictionary<string, string>
        {
            { "CustomAttribute", userValue }
        };

        // Act
        var result = _engine.Evaluate(rules, context);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void Evaluate_MultipleRules_ShouldRequireAllToPass_AndLogic()
    {
        // Arrange
        var rules = new List<RuleDto>
        {
            new RuleDto("Email", "EndsWith", "@company.com"),
            new RuleDto("Age", "GreaterThan", "21"),
            new RuleDto("Role", "InList", "Admin,Manager")
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
        _engine.Evaluate(rules, passingContext).Should().BeTrue();
        _engine.Evaluate(rules, failingContext1).Should().BeFalse();
        _engine.Evaluate(rules, failingContext2).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NoRules_ShouldReturnTrue()
    {
        // Act
        var result = _engine.Evaluate(new List<RuleDto>(), new Dictionary<string, string>());

        // Assert
        result.Should().BeTrue("No rules means the flag relies solely on its IsEnabled status");
    }

    [Fact]
    public void Evaluate_UnknownOperator_ShouldReturnFalse()
    {
        // Arrange
        var rules = new List<RuleDto>
        {
            new RuleDto("Device", "MagicOperator", "iPhone")
        };

        var context = new Dictionary<string, string>
        {
            { "Device", "iPhone" }
        };

        // Act
        var result = _engine.Evaluate(rules, context);

        // Assert
        result.Should().BeFalse("Engine should safely fail un-evaluable rules to prevent accidental exposure");
    }
}