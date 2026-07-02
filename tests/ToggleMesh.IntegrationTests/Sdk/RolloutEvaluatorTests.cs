using FluentAssertions;
using ToggleMesh.Common.Rules;

namespace ToggleMesh.IntegrationTests.Sdk;

public class RolloutEvaluatorTests
{
    [Theory]
    [InlineData(100, "flag1", "user1", true)]
    [InlineData(100, "flag1", "user2", true)]
    [InlineData(0, "flag1", "user1", false)]
    [InlineData(0, "flag1", "user2", false)]
    public void Evaluate_AbsolutePercentages_ShouldBeDeterministic(int percentage, string flagKey, string identity, bool expected)
    {
        var result = RolloutEvaluator.Evaluate(percentage, flagKey, identity);
        result.Should().Be(expected);
    }

    [Fact]
    public void Evaluate_NullPercentage_ShouldAlwaysReturnTrue()
    {
        RolloutEvaluator.Evaluate(null, "flag", "user").Should().BeTrue();
        RolloutEvaluator.Evaluate(null, "flag", "").Should().BeTrue();
    }

    [Fact]
    public void Evaluate_EmptyIdentity_ShouldReturnFalseForPartialRollout()
    {
        RolloutEvaluator.Evaluate(50, "flag", "").Should().BeFalse();
        RolloutEvaluator.Evaluate(50, "flag", null!).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_PartialRollout_ShouldDistributeConsistently()
    {
        // Arrange
        int totalUsers = 10000;
        int targetPercentage = 25;
        int enabledCount = 0;
        string flagKey = "new_ui_feature";

        // Act
        for (int i = 0; i < totalUsers; i++)
        {
            var identity = $"user_{i}";
            if (RolloutEvaluator.Evaluate(targetPercentage, flagKey, identity))
            {
                enabledCount++;
            }
        }

        // Assert
        var actualPercentage = (double)enabledCount / totalUsers * 100;
        actualPercentage.Should().BeInRange(targetPercentage - 5, targetPercentage + 5);
    }

    [Fact]
    public void Evaluate_SameUser_ShouldYieldSameResult()
    {
        // Act
        var result1 = RolloutEvaluator.Evaluate(30, "flag_A", "user_123");
        var result2 = RolloutEvaluator.Evaluate(30, "flag_A", "user_123");

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void Evaluate_DifferentFlags_ShouldYieldIndependentResultsForSameUser()
    {
        var identity = "test_user_42";

        var bucketA = CalculateBucket("flagA", identity);
        var bucketB = CalculateBucket("flagB", identity);

        bucketA.Should().NotBe(bucketB);
    }

    private static int CalculateBucket(string flagKey, string identity)
    {
        var text = flagKey + identity;
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;

        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= prime;
        }
        return (int)(hash % 100);
    }
}
