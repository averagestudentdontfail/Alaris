using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;
using Alaris.Strategy.Hedge;
using Alaris.Strategy.Risk;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for Phase 2 Medium/Lower priority components.
/// </summary>
public sealed class Phase2MediumPriorityTests
{
    // STDD001A - Dividend Ex-Date Detector

    [Fact]
    public void STDD001A_FindExDatesInWindow_ReturnsMatchingDates()
    {
        // Arrange
        STDD001A detector = new STDD001A();
        List<STDD002A> schedule = new List<STDD002A>
        {
            new STDD002A { ExDate = new DateTime(2024, 1, 15), Amount = 1.50 },
            new STDD002A { ExDate = new DateTime(2024, 2, 15), Amount = 1.50 },
            new STDD002A { ExDate = new DateTime(2024, 3, 15), Amount = 1.50 }
        };

        // Act
        IReadOnlyList<STDD002A> result = detector.FindExDatesInWindow(
            schedule,
            new DateTime(2024, 2, 1),
            new DateTime(2024, 2, 28));

        // Assert
        result.Should().HaveCount(1);
        result[0].ExDate.Should().Be(new DateTime(2024, 2, 15));
    }

    [Fact]
    public void STDD001A_CalculateEarlyExerciseRisk_HighDividend_ReturnsHighRisk()
    {
        // Arrange: Large dividend relative to strike×(1-e^(-rτ))
        STDD001A detector = new STDD001A();

        // Act: $5 dividend on $100 strike with 5% rate, 1 week to expiry
        double risk = detector.CalculateEarlyExerciseRisk(
            dividendAmount: 5.0,
            strike: 100.0,
            riskFreeRate: 0.05,
            timeFromExToExpiry: 7.0 / 365.0);

        // Assert: Should be high risk (>= 0.5)
        risk.Should().BeGreaterThanOrEqualTo(0.5);
    }

    [Fact]
    public void STDD001A_CalculateEarlyExerciseRisk_NoDividend_ReturnsZero()
    {
        // Arrange
        STDD001A detector = new STDD001A();

        // Act
        double risk = detector.CalculateEarlyExerciseRisk(
            dividendAmount: 0.0, strike: 100.0,
            riskFreeRate: 0.05, timeFromExToExpiry: 30.0 / 365.0);

        // Assert
        risk.Should().Be(0.0);
    }

    // STHD009A - Pin Risk Monitor

    [Fact]
    public void STHD009A_IsInPinZone_AtStrike_ReturnsTrue()
    {
        // Act
        bool result = STHD009A.IsInPinZone(spotPrice: 100.0, strike: 100.0);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void STHD009A_IsInPinZone_FarFromStrike_ReturnsFalse()
    {
        // Act: 5% away from strike
        bool result = STHD009A.IsInPinZone(spotPrice: 105.0, strike: 100.0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void STHD009A_Evaluate_HighPinRisk_RecommendsAction()
    {
        // Arrange
        STHD009A monitor = new STHD009A();

        // Act: ATM with 1 DTE - high pin risk zone
        STHD010A result = monitor.Evaluate(
            spotPrice: 100.0, strike: 100.0,
            daysToExpiry: 1, gamma: null, contracts: 10);

        // Assert: Risk should be High or Critical, recommending action
        result.RiskLevel.Should().BeOneOf(STHD011A.High, STHD011A.Critical);
        result.RecommendedAction.Should().BeOneOf(STHD012A.RollOut, STHD012A.CloseEarly);
    }

    [Fact]
    public void STHD009A_Evaluate_FarFromExpiry_ReturnsLowRisk()
    {
        // Arrange
        STHD009A monitor = new STHD009A();

        // Act: ATM but 10 DTE
        STHD010A result = monitor.Evaluate(
            spotPrice: 100.0, strike: 100.0,
            daysToExpiry: 10, gamma: null, contracts: 10);

        // Assert: Low risk due to time buffer (None or Low)
        result.RiskLevel.Should().BeOneOf(STHD011A.None, STHD011A.Low);
    }

    // STCR005A - Signal Freshness Monitor

    [Fact]
    public void STCR005A_CalculateFreshness_JustGenerated_ReturnsOne()
    {
        // Arrange
        STCR005A monitor = new STCR005A();
        DateTime now = DateTime.UtcNow;

        // Act
        double freshness = monitor.CalculateFreshness(now, now);

        // Assert
        freshness.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void STCR005A_CalculateFreshness_AtHalfLife_ReturnsHalf()
    {
        // Arrange: 60-minute half-life
        STCR005A monitor = new STCR005A(halfLifeMinutes: 60.0);
        DateTime signalTime = DateTime.UtcNow.AddMinutes(-60);

        // Act
        double freshness = monitor.CalculateFreshness(signalTime, DateTime.UtcNow);

        // Assert: Should be ~0.5 at half-life
        freshness.Should().BeApproximately(0.5, 0.05);
    }

    [Fact]
    public void STCR005A_RequiresRevalidation_StaleSignal_ReturnsTrue()
    {
        // Arrange
        STCR005A monitor = new STCR005A(halfLifeMinutes: 60.0);
        DateTime staleSignal = DateTime.UtcNow.AddMinutes(-120); // 2 hours old

        // Act
        bool needsRevalidation = monitor.RequiresRevalidation(staleSignal, DateTime.UtcNow);

        // Assert: 2 half-lives = 25% freshness, below 50% threshold
        needsRevalidation.Should().BeTrue();
    }

    // STHD001A - VIX-Conditional Threshold

    [Fact]
    public void STHD001A_GetConditionalCorrelationThreshold_LowVIX_ReturnsBase()
    {
        // Act
        double threshold = STHD001A.GetConditionalCorrelationThreshold(currentVIX: 15.0);

        // Assert
        threshold.Should().Be(0.70);
    }

    [Fact]
    public void STHD001A_GetConditionalCorrelationThreshold_HighVIX_ReturnsHigher()
    {
        // Act
        double threshold = STHD001A.GetConditionalCorrelationThreshold(currentVIX: 30.0);

        // Assert
        threshold.Should().Be(0.85);
    }

    // STRK001A - Net-of-Cost Kelly

    [Fact]
    public void STRK001A_CalculateNetOfCostKelly_NoCost_ReturnsStandardKelly()
    {
        // Arrange: 60% win rate, 2:1 win/loss ratio
        double winProb = 0.60;
        double avgWin = 200.0;
        double avgLoss = 100.0;

        // Standard Kelly: (0.6 * 2 - 0.4) / 2 = 0.4
        double standardKelly = ((winProb * (avgWin / avgLoss)) - (1 - winProb)) / (avgWin / avgLoss);

        // Act
        double netKelly = STRK001A.CalculateNetOfCostKelly(winProb, avgWin, avgLoss, roundTripCost: 0);

        // Assert
        netKelly.Should().BeApproximately(standardKelly, 0.001);
    }

    [Fact]
    public void STRK001A_CalculateNetOfCostKelly_HighCost_ReturnsReduced()
    {
        // Arrange
        double winProb = 0.60;
        double avgWin = 200.0;
        double avgLoss = 100.0;
        double zeroCostKelly = STRK001A.CalculateNetOfCostKelly(winProb, avgWin, avgLoss, 0);

        // Act: With significant costs
        double withCostKelly = STRK001A.CalculateNetOfCostKelly(winProb, avgWin, avgLoss, roundTripCost: 50);

        // Assert: Costs should reduce Kelly fraction
        withCostKelly.Should().BeLessThan(zeroCostKelly);
    }

    [Fact]
    public void STRK001A_CalculateNetOfCostKelly_CostsExceedEdge_ReturnsZero()
    {
        // Arrange: Costs exceed average win
        double result = STRK001A.CalculateNetOfCostKelly(
            winProbability: 0.55,
            avgWin: 100.0,
            avgLoss: 100.0,
            roundTripCost: 150.0);

        // Assert: Should return zero when costs exceed edge
        result.Should().Be(0.0);
    }
}
