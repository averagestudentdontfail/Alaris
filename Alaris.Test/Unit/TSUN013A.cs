// =============================================================================
// Phase3ComponentTests.cs - Unit Tests for Signal Processing Enhancements
// Tests: STSD001A, STKF001A, STQT001A, STHD007B
// =============================================================================

using FluentAssertions;
using Xunit;

namespace Alaris.Test.Unit;

// =============================================================================
// STSD001A: Neyman-Pearson Signal Detection Tests
// =============================================================================

public sealed class SignalDetectionTests
{
    [Fact]
    public void LikelihoodRatio_HighIVRV_FavorsAlternative()
    {
        // Arrange
        var detector = new Alaris.Strategy.Detection.STSD001A();
        double highRatio = 1.50;

        // Act
        double lr = detector.ComputeLikelihoodRatio(highRatio);

        // Assert
        lr.Should().BeGreaterThan(1.0, "high IV/RV should favor H₁ (profitable)");
    }

    [Fact]
    public void LikelihoodRatio_LowIVRV_FavorsNull()
    {
        // Arrange
        var detector = new Alaris.Strategy.Detection.STSD001A();
        double lowRatio = 1.00;

        // Act
        double lr = detector.ComputeLikelihoodRatio(lowRatio);

        // Assert
        lr.Should().BeLessThan(1.0, "low IV/RV should favor H₀ (unprofitable)");
    }

    [Fact]
    public void PosteriorProbability_SumsToOne()
    {
        // Arrange
        var detector = new Alaris.Strategy.Detection.STSD001A();

        // Act
        double posterior = detector.ComputePosteriorProbability(1.25);

        // Assert
        posterior.Should().BeInRange(0, 1, "posterior must be a valid probability");
    }

    [Fact]
    public void OptimalThreshold_CostRatio2_HigherThanMidpoint()
    {
        // Arrange
        var detector = new Alaris.Strategy.Detection.STSD001A();

        // Act
        double threshold = detector.ComputeOptimalThreshold(costRatio: 2.0);
        var @params = detector.GetParameters();
        double midpoint = (@params.Mu0 + @params.Mu1) / 2;

        // Assert
        threshold.Should().BeGreaterThan(midpoint,
            "threshold should shift right when Type I errors are more costly");
    }

    [Fact]
    public void TypeIError_IncreasesWithLowerThreshold()
    {
        // Arrange
        var detector = new Alaris.Strategy.Detection.STSD001A();

        // Act
        double alphaLow = detector.ComputeTypeIError(1.30);
        double alphaHigh = detector.ComputeTypeIError(1.20);

        // Assert
        alphaHigh.Should().BeGreaterThan(alphaLow,
            "lower threshold increases Type I error rate");
    }
}

// =============================================================================
// STKF001A: Kalman-Filtered Volatility Tests
// =============================================================================

public sealed class KalmanVolatilityTests
{
    [Fact]
    public void Update_FirstMeasurement_InitialisesState()
    {
        // Arrange
        var filter = new Alaris.Strategy.Core.STKF001A();

        // Act
        var result = filter.Update(0.25, sampleSize: 30);

        // Assert
        filter.IsInitialised.Should().BeTrue();
        filter.UpdateCount.Should().Be(1);
        result.Volatility.Should().BeApproximately(0.25, 0.02);
    }

    [Fact]
    public void Update_MultipleSteps_ReducesVariance()
    {
        // Arrange
        var filter = new Alaris.Strategy.Core.STKF001A();
        filter.Reset(0.20, 0.05);

        double initialVariance = filter.Variance;

        // Act
        for (int i = 0; i < 10; i++)
        {
            filter.Update(0.21 + (0.01 * Math.Sin(i)), sampleSize: 30);
        }

        // Assert
        filter.Variance.Should().BeLessThan(initialVariance,
            "repeated measurements should reduce estimation uncertainty");
    }

    [Fact]
    public void SkipMeasurement_IncreasesVariance()
    {
        // Arrange
        var filter = new Alaris.Strategy.Core.STKF001A();
        filter.Reset(0.20, 0.01);

        double initialVariance = filter.Variance;

        // Act
        filter.SkipMeasurement();

        // Assert
        filter.Variance.Should().BeGreaterThan(initialVariance,
            "missing measurement should increase uncertainty");
    }
}

// =============================================================================
// STQT001A: Queue-Theoretic Position Management Tests
// =============================================================================

public sealed class QueueTheoryTests
{
    [Fact]
    public void MeanQueueLength_IncreasesWithUtilisation()
    {
        // Arrange & Act
        double L1 = Alaris.Strategy.Risk.STQT001A.ComputeMeanQueueLength(0.3, 1.0, 0.5);
        double L2 = Alaris.Strategy.Risk.STQT001A.ComputeMeanQueueLength(0.6, 1.0, 0.5);
        double L3 = Alaris.Strategy.Risk.STQT001A.ComputeMeanQueueLength(0.9, 1.0, 0.5);

        // Assert
        L3.Should().BeGreaterThan(L2);
        L2.Should().BeGreaterThan(L1);
    }

    [Fact]
    public void BlockingProbability_AtCapacity_ReturnsOne()
    {
        // Arrange
        var queue = new Alaris.Strategy.Risk.STQT001A();

        // Act
        double pk = queue.ComputeBlockingProbability(currentPositions: 10, maxCapacity: 10, utilisation: 0.8);

        // Assert
        pk.Should().Be(1.0);
    }

    [Fact]
    public void GittinsProxy_HigherProfit_HigherPriority()
    {
        // Act
        double p1 = Alaris.Strategy.Risk.STQT001A.ComputeGittinsProxy(100, 5, 0);
        double p2 = Alaris.Strategy.Risk.STQT001A.ComputeGittinsProxy(200, 5, 0);

        // Assert
        p2.Should().BeGreaterThan(p1);
    }

    [Fact]
    public void SelectForEjection_NewSignalHigherPriority_ReturnsMinIndex()
    {
        // Arrange
        var positions = new Alaris.Strategy.Risk.PositionPriority[]
        {
            new("AAPL", 100, 5, 0, 20),
            new("GOOG", 80, 10, 0, 8),
            new("MSFT", 150, 7, 0, 21.4),
        };

        // Act
        int ejectIndex = Alaris.Strategy.Risk.STQT001A.SelectForEjection(positions, newSignalPriority: 15);

        // Assert
        ejectIndex.Should().Be(1, "should eject position with lowest priority (GOOG)");
    }
}

// =============================================================================
// STHD007B: Rule-Based Exit Monitor Tests
// =============================================================================

public sealed class RuleBasedExitTests
{
    [Fact]
    public void Evaluate_TargetCapture_ExitsImmediately()
    {
        // Arrange
        var monitor = new Alaris.Strategy.Hedge.STHD007B();

        // Act - captured 100% of expected crush
        var result = monitor.Evaluate(crushCaptured: 1.0, daysElapsed: 2, daysRemaining: 5);

        // Assert
        result.Action.Should().Be(Alaris.Strategy.Hedge.ExitAction.Exit);
        result.Reason.Should().Be(Alaris.Strategy.Hedge.ExitReason.TargetCaptured);
    }

    [Fact]
    public void Evaluate_SignificantCaptureButStalled_Exits()
    {
        // Arrange
        var monitor = new Alaris.Strategy.Hedge.STHD007B();

        // Simulate rate going to zero (stall)
        monitor.Evaluate(0.55, 1, 6);
        monitor.Evaluate(0.58, 2, 5);
        monitor.Evaluate(0.60, 3, 4);

        // Act - 60% captured, rate stalled
        var result = monitor.Evaluate(crushCaptured: 0.61, daysElapsed: 4, daysRemaining: 3);

        // Assert
        result.Action.Should().Be(Alaris.Strategy.Hedge.ExitAction.Exit);
        result.Reason.Should().Be(Alaris.Strategy.Hedge.ExitReason.CrushStalled);
    }

    [Fact]
    public void Evaluate_TracksUpdateCount()
    {
        // Arrange
        var monitor = new Alaris.Strategy.Hedge.STHD007B();

        // Act - perform multiple evaluations
        monitor.Evaluate(0.10, 1, 10);
        monitor.Evaluate(0.20, 2, 9);
        monitor.Evaluate(0.30, 3, 8);

        // Assert - should track update count
        monitor.UpdateCount.Should().Be(3);
        monitor.IsInitialised.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ApproachingExpiry_TimeDecayExit()
    {
        // Arrange
        var monitor = new Alaris.Strategy.Hedge.STHD007B();

        // Act - 50% captured, only 1.5 days remaining
        var result = monitor.Evaluate(crushCaptured: 0.55, daysElapsed: 5, daysRemaining: 1.5);

        // Assert
        result.Action.Should().Be(Alaris.Strategy.Hedge.ExitAction.Exit);
        result.Reason.Should().Be(Alaris.Strategy.Hedge.ExitReason.TimeDecay);
    }

    [Fact]
    public void Evaluate_GoodProgress_Holds()
    {
        // Arrange
        var monitor = new Alaris.Strategy.Hedge.STHD007B();

        // Simulate good crush progress
        monitor.Evaluate(0.20, 1, 7);
        monitor.Evaluate(0.35, 2, 6);

        // Act - 50% captured, rate declining, plenty of time
        var result = monitor.Evaluate(crushCaptured: 0.50, daysElapsed: 3, daysRemaining: 5);

        // Assert
        result.Action.Should().Be(Alaris.Strategy.Hedge.ExitAction.Hold);
    }

    [Fact]
    public void ComputeCrushCaptured_CorrectCalculation()
    {
        // Arrange
        double ivObserved = 0.25;
        double ivExpected = 0.35;
        double expectedCrush = 0.15;

        // Act
        double captured = Alaris.Strategy.Hedge.STHD007B.ComputeCrushCaptured(
            ivObserved, ivExpected, expectedCrush);

        // Assert - (0.35 - 0.25) / 0.15 = 0.667
        captured.Should().BeApproximately(0.667, 0.01);
    }

    [Fact]
    public void SmoothedRate_TracksRateChange()
    {
        // Arrange
        var monitor = new Alaris.Strategy.Hedge.STHD007B();

        // Simulate consistent crush
        monitor.Evaluate(0.20, 1, 7);
        monitor.Evaluate(0.40, 2, 6);
        monitor.Evaluate(0.60, 3, 5);

        // Assert - rate should be positive (crush increasing)
        monitor.SmoothedRate.Should().BePositive();
    }
}
