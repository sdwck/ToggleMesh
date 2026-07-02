using FluentAssertions;
using ToggleMesh.API.Features.Analytics.Services;

namespace ToggleMesh.IntegrationTests.Analytics;

public class MabStatisticsTests
{
    private readonly BayesianMathService _math;

    public MabStatisticsTests()
    {
        _math = new BayesianMathService();
    }

    [Fact]
    public void Test_Treatment_Significantly_Better()
    {
        long controlExposures = 10000;
        long controlConversions = 500;
        
        long treatmentExposures = 10000;
        long treatmentConversions = 1000;

        var prob = _math.CalculateProbabilityBBeatsA(controlExposures, controlConversions, treatmentExposures, treatmentConversions);
        var uplift = _math.CalculateExpectedUplift(controlExposures, controlConversions, treatmentExposures, treatmentConversions);

        prob.Should().BeGreaterThan(0.99, "Treatment is clearly twice as good, probability should approach 1.0");
        uplift.Should().BeApproximately(1.0, 0.05, "Uplift is 100% relative (5% -> 10%)");
        
        var targetRollout = (int)Math.Round(prob * 100);
        targetRollout = Math.Clamp(targetRollout, 5, 95);

        targetRollout.Should().Be(95, "Algorithm should shift max traffic to winner");
    }

    [Fact]
    public void Test_Control_Significantly_Better()
    {
        long controlExposures = 10000;
        long controlConversions = 1000;
        
        long treatmentExposures = 10000;
        long treatmentConversions = 500;

        var prob = _math.CalculateProbabilityBBeatsA(controlExposures, controlConversions, treatmentExposures, treatmentConversions);
        var uplift = _math.CalculateExpectedUplift(controlExposures, controlConversions, treatmentExposures, treatmentConversions);
        
        prob.Should().BeLessThan(0.01, "Treatment is much worse, probability of beating control should approach 0");
        uplift.Should().BeApproximately(-0.5, 0.05, "Uplift is -50% relative (10% -> 5%)");
        
        var targetRollout = (int)Math.Round(prob * 100);
        targetRollout = Math.Clamp(targetRollout, 5, 95);

        targetRollout.Should().Be(5, "Algorithm should shift minimum traffic to loser");
    }

    [Fact]
    public void Test_Tie_Behavior()
    {
        long controlExposures = 10000;
        long controlConversions = 1000;
        
        long treatmentExposures = 10000;
        long treatmentConversions = 1000;

        var prob = _math.CalculateProbabilityBBeatsA(controlExposures, controlConversions, treatmentExposures, treatmentConversions);
        var uplift = _math.CalculateExpectedUplift(controlExposures, controlConversions, treatmentExposures, treatmentConversions);
        
        prob.Should().BeInRange(0.40, 0.60, "Both variants are identical, probability should hover around 50%");
        uplift.Should().BeApproximately(0.0, 0.05, "Uplift should be approximately 0%");
        
        var targetRollout = (int)Math.Round(prob * 100);
        targetRollout = Math.Clamp(targetRollout, 5, 95);

        targetRollout.Should().BeInRange(40, 60, "Algorithm should maintain ~50/50 split");
    }

    [Fact]
    public void Test_Rollout_Velocity_Limits()
    {
        var currentRollout = 50;
        
        var probBBeatsA = 0.99;
        var targetRollout = (int)Math.Round(probBBeatsA * 100);

        var maxStep = 10;
        var newRollout = targetRollout;

        if (newRollout > currentRollout + maxStep) 
            newRollout = currentRollout + maxStep;
        if (newRollout < currentRollout - maxStep) 
            newRollout = currentRollout - maxStep;

        newRollout.Should().Be(60, "Rollout should only increase by maxStep (10) per iteration to prevent sudden shocks");
    }
}
