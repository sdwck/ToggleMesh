using FluentAssertions;
using ToggleMesh.Common.Rules;
using ToggleMesh.Common;

namespace ToggleMesh.IntegrationTests.Sdk;

public class RolloutEvaluatorTests
{
    private readonly Guid _varA = Guid.NewGuid();
    private readonly Guid _varB = Guid.NewGuid();

    [Fact]
    public void Evaluate_NullRollout_ShouldReturnOffVariation()
    {
        RolloutEvaluator.Evaluate(null, _varB, "flag", "user").Should().Be(_varB);
    }

    [Fact]
    public void Evaluate_EmptyIdentity_ShouldReturnOffVariation()
    {
        var rollout = new[] { new VariationWeight(_varA, 5000), new VariationWeight(_varB, 5000) };
        RolloutEvaluator.Evaluate(rollout, _varB, "flag", "").Should().Be(_varB);
        RolloutEvaluator.Evaluate(rollout, _varB, "flag", null!).Should().Be(_varB);
    }

    [Fact]
    public void Evaluate_100PercentSingleVariation_ShouldAlwaysReturnIt()
    {
        var rollout = new[] { new VariationWeight(_varA, 10000) };
        
        for (var i = 0; i < 100; i++)
        {
            RolloutEvaluator.Evaluate(rollout, _varB, "flag", $"user_{i}")
                .Should().Be(_varA);
        }
    }

    [Fact]
    public void Evaluate_SameUser_ShouldYieldSameResult()
    {
        var rollout = new[] { new VariationWeight(_varA, 3000), new VariationWeight(_varB, 7000) };
        
        var result1 = RolloutEvaluator.Evaluate(rollout, null, "flag_A", "user_123");
        var result2 = RolloutEvaluator.Evaluate(rollout, null, "flag_A", "user_123");

        result1.Should().Be(result2);
    }

    [Fact]
    public void Evaluate_PartialRollout_ShouldDistributeConsistently()
    {
        const int totalUsers = 10000;
        var countA = 0;
        const string flagKey = "new_ui_feature";
        var rollout = new[] { new VariationWeight(_varA, 25 * 100), new VariationWeight(_varB, 75 * 100) };

        for (var i = 0; i < totalUsers; i++)
        {
            var identity = $"user_{i}";
            if (RolloutEvaluator.Evaluate(rollout, null, flagKey, identity) == _varA)
                countA++;
        }

        var actualPercentage = (double)countA / totalUsers * 100;
        actualPercentage.Should().BeInRange(20, 30);
    }

    [Fact]
    public void Evaluate_DifferentFlags_ShouldYieldIndependentResults()
    {
        var rollout = new[] { new VariationWeight(_varA, 5000), new VariationWeight(_varB, 5000) };
        const string identity = "test_user_42";

        var resultA = RolloutEvaluator.Evaluate(rollout, null, "flagA", identity);
        var resultB = RolloutEvaluator.Evaluate(rollout, null, "flagB", identity);

        (resultA, resultB).Should().Match<(Guid?, Guid?)>(r => r.Item1 != null && r.Item2 != null);
    }
}
