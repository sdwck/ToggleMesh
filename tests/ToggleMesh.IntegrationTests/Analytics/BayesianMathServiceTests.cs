using FluentAssertions;
using ToggleMesh.API.Features.Analytics.Services;

namespace ToggleMesh.IntegrationTests.Analytics;

public class BayesianMathServiceTests
{
    private readonly BayesianMathService _service;

    public BayesianMathServiceTests()
    {
        _service = new BayesianMathService();
    }

    [Fact]
    public void CalculateProbabilityBBeatsA_WhenBIsObviouslyBetter_ShouldReturnHighProbability()
    {
        // Arrange
        long exposuresA = 1000;
        long conversionsA = 100;

        long exposuresB = 1000;
        long conversionsB = 200;

        // Act
        var prob = _service.CalculateProbabilityBBeatsA(exposuresA, conversionsA, exposuresB, conversionsB);

        // Assert
        prob.Should().BeGreaterThan(0.99);
    }

    [Fact]
    public void CalculateProbabilityBBeatsA_WhenAIsObviouslyBetter_ShouldReturnLowProbability()
    {
        // Arrange
        long exposuresA = 1000;
        long conversionsA = 200;

        long exposuresB = 1000;
        long conversionsB = 100;

        // Act
        var prob = _service.CalculateProbabilityBBeatsA(exposuresA, conversionsA, exposuresB, conversionsB);

        // Assert
        prob.Should().BeLessThan(0.01);
    }

    [Fact]
    public void CalculateProbabilityBBeatsA_WhenAAndBAreIdentical_ShouldReturnAround50Percent()
    {
        // Arrange
        long exposuresA = 10000;
        long conversionsA = 500;

        long exposuresB = 10000;
        long conversionsB = 500;

        // Act
        var prob = _service.CalculateProbabilityBBeatsA(exposuresA, conversionsA, exposuresB, conversionsB, iterations: 100000);

        // Assert
        prob.Should().BeInRange(0.48, 0.52);
    }

    [Fact]
    public void CalculateExpectedUplift_ShouldCalculateCorrectly()
    {
        // Arrange
        long exposuresA = 1000;
        long conversionsA = 100;

        long exposuresB = 1000;
        long conversionsB = 115;

        // Act
        var uplift = _service.CalculateExpectedUplift(exposuresA, conversionsA, exposuresB, conversionsB);

        // Assert
        uplift.Should().BeApproximately(0.15, 0.001);
    }

    [Fact]
    public void SimulateDataProgression_ShouldShowProbabilityConvergence()
    {
        var scenarios = new[]
        {
            new { Step = "Day 1", ExpA = 100L, ConvA = 10L, ExpB = 100L, ConvB = 13L, ExpectedMinProb = 0.50, ExpectedMaxProb = 0.85 },
            new { Step = "Day 2", ExpA = 500L, ConvA = 50L, ExpB = 500L, ConvB = 65L, ExpectedMinProb = 0.80, ExpectedMaxProb = 0.95 },
            new { Step = "Day 3", ExpA = 1000L, ConvA = 100L, ExpB = 1000L, ConvB = 130L, ExpectedMinProb = 0.90, ExpectedMaxProb = 0.99 },
            new { Step = "Day 4", ExpA = 3000L, ConvA = 300L, ExpB = 3000L, ConvB = 390L, ExpectedMinProb = 0.99, ExpectedMaxProb = 1.00 }
        };

        double previousProb = 0.0;

        foreach (var batch in scenarios)
        {
            var prob = _service.CalculateProbabilityBBeatsA(batch.ExpA, batch.ConvA, batch.ExpB, batch.ConvB);

            prob.Should().BeInRange(batch.ExpectedMinProb, batch.ExpectedMaxProb,
                $"{batch.Step}: With ExpA={batch.ExpA} and ExpB={batch.ExpB}, probability should converge.");

            if (previousProb > 0)
            {
                prob.Should().BeGreaterThan(previousProb, $"{batch.Step}: Confidence should increase as sample size grows.");
            }

            previousProb = prob;
        }

        var finalBatch = scenarios.Last();
        var uplift = _service.CalculateExpectedUplift(finalBatch.ExpA, finalBatch.ConvA, finalBatch.ExpB, finalBatch.ConvB);
        uplift.Should().BeApproximately(0.30, 0.01);
    }
}
