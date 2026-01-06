// TSUN027A.cs - Risk Component Unit Tests
// Component ID: TSUN027A
//
// Tests for Risk Management Components:
// - STRK001A: Kelly Criterion position sizing
// - STRK002A: Position size validation
// - STMG001A: Maturity guard for entry/exit
//
// Mathematical Invariants Tested:
// 1. Kelly Formula: f* = (p*b - q) / b where p + q = 1
// 2. Net-of-Cost Kelly: f*_net ≤ f* (costs reduce optimal bet)
// 3. Fractional Kelly: Applied fraction ∈ (0, 1]
// 4. Position Bounds: 0 ≤ allocation ≤ MaxAllocation
// 5. Maturity Ordering: MinEntry > ForceExit thresholds
//
// References:
//   - Kelly, J.L. (1956) "A New Interpretation of Information Rate"
//   - Thorp, E.O. (2008) "The Kelly Criterion in Blackjack, Sports Betting, and the Stock Market"

using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Risk;
using Alaris.Strategy.Core;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN027A: Unit tests for risk management components.
/// Tests Kelly Criterion sizing and maturity guard logic.
/// </summary>
public sealed class TSUN027A
{

    private static List<Trade> CreateWinningTradeHistory(int count, double avgWin)
    {
        List<Trade> trades = new List<Trade>();
        for (int i = 0; i < count; i++)
        {
            trades.Add(new Trade
            {
                EntryDate = DateTime.Today.AddDays(-i - 1),
                ExitDate = DateTime.Today.AddDays(-i),
                ProfitLoss = avgWin * (0.8 + 0.4 * (i % 3) / 2.0), // Vary around avgWin
                Symbol = "TEST",
                Strategy = "CalendarSpread"
            });
        }
        return trades;
    }

    private static List<Trade> CreateMixedTradeHistory(int wins, int losses, double avgWin, double avgLoss)
    {
        List<Trade> trades = new List<Trade>();

        for (int i = 0; i < wins; i++)
        {
            trades.Add(new Trade
            {
                EntryDate = DateTime.Today.AddDays(-i - losses - 1),
                ExitDate = DateTime.Today.AddDays(-i - losses),
                ProfitLoss = avgWin,
                Symbol = "TEST",
                Strategy = "CalendarSpread"
            });
        }

        for (int i = 0; i < losses; i++)
        {
            trades.Add(new Trade
            {
                EntryDate = DateTime.Today.AddDays(-i - 1),
                ExitDate = DateTime.Today.AddDays(-i),
                ProfitLoss = -avgLoss,
                Symbol = "TEST",
                Strategy = "CalendarSpread"
            });
        }

        return trades;
    }

    private static STCR004A CreateSignal(STCR004AStrength strength = STCR004AStrength.Recommended)
    {
        return new STCR004A
        {
            Symbol = "TEST",
            Strength = strength
        };
    }


    // STRK001A: Kelly Criterion Tests

    /// <summary>
    /// INVARIANT: Net-of-cost Kelly ≤ Standard Kelly (costs reduce optimal bet).
    /// </summary>
    [Theory]
    [InlineData(0.6, 200, 100, 10)]   // Small costs
    [InlineData(0.6, 200, 100, 50)]   // Medium costs
    [InlineData(0.6, 200, 100, 100)]  // Large costs
    public void NetOfCostKelly_IsLessOrEqualToStandardKelly(
        double winProb, double avgWin, double avgLoss, double roundTripCost)
    {
        // Arrange
        double standardKelly = STRK001A.CalculateNetOfCostKelly(winProb, avgWin, avgLoss, 0);

        // Act
        double netOfCostKelly = STRK001A.CalculateNetOfCostKelly(winProb, avgWin, avgLoss, roundTripCost);

        // Assert
        netOfCostKelly.Should().BeLessThanOrEqualTo(standardKelly,
            "Net-of-cost Kelly should never exceed standard Kelly");
    }

    /// <summary>
    /// When costs exceed edge, Kelly should be zero (no positive expectancy).
    /// </summary>
    [Fact]
    public void NetOfCostKelly_CostsExceedEdge_ReturnsZero()
    {
        // Arrange - Costs ($250) exceed average win ($200)
        double winProb = 0.6;
        double avgWin = 200;
        double avgLoss = 100;
        double excessiveCost = 250;

        // Act
        double kelly = STRK001A.CalculateNetOfCostKelly(winProb, avgWin, avgLoss, excessiveCost);

        // Assert
        kelly.Should().Be(0, "When costs exceed edge, Kelly should be zero");
    }

    /// <summary>
    /// INVARIANT: Kelly = 0 when win probability = 0 or 1.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void NetOfCostKelly_InvalidWinProbability_ReturnsZero(double winProb)
    {
        // Act
        double kelly = STRK001A.CalculateNetOfCostKelly(winProb, 200, 100, 10);

        // Assert
        kelly.Should().Be(0, $"Kelly should be 0 for invalid win probability {winProb}");
    }

    /// <summary>
    /// Standard Kelly formula: f* = (p*b - q) / b where b = avgWin/avgLoss.
    /// </summary>
    [Fact]
    public void NetOfCostKelly_ZeroCosts_MatchesStandardFormula()
    {
        // Arrange
        double p = 0.6;  // Win probability
        double q = 0.4;  // Loss probability
        double avgWin = 200;
        double avgLoss = 100;
        double b = avgWin / avgLoss;  // Win/loss ratio = 2

        // Expected: f* = (0.6 * 2 - 0.4) / 2 = (1.2 - 0.4) / 2 = 0.4
        double expectedKelly = (p * b - q) / b;

        // Act
        double actualKelly = STRK001A.CalculateNetOfCostKelly(p, avgWin, avgLoss, 0);

        // Assert
        actualKelly.Should().BeApproximately(expectedKelly, 1e-10,
            "Zero-cost Kelly should match standard formula");
    }

    /// <summary>
    /// INVARIANT: Kelly ≥ 0 (never bet negative amounts).
    /// </summary>
    [Theory]
    [InlineData(0.3, 100, 200)]  // Negative expectancy
    [InlineData(0.4, 100, 100)]  // Zero expectancy
    [InlineData(0.1, 50, 200)]   // Strong negative expectancy
    public void NetOfCostKelly_NegativeExpectancy_ReturnsZeroOrPositive(
        double winProb, double avgWin, double avgLoss)
    {
        // Act
        double kelly = STRK001A.CalculateNetOfCostKelly(winProb, avgWin, avgLoss, 0);

        // Assert
        kelly.Should().BeGreaterThanOrEqualTo(0, "Kelly should never be negative");
    }

    /// <summary>
    /// Invalid inputs should throw appropriate exceptions.
    /// </summary>
    [Fact]
    public void NetOfCostKelly_NegativeAvgWin_Throws()
    {
        // Act & Assert
        Action act = () => STRK001A.CalculateNetOfCostKelly(0.6, -100, 100, 10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NetOfCostKelly_NegativeAvgLoss_Throws()
    {
        // Act & Assert
        Action act = () => STRK001A.CalculateNetOfCostKelly(0.6, 100, -100, 10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NetOfCostKelly_NegativeCost_Throws()
    {
        // Act & Assert
        Action act = () => STRK001A.CalculateNetOfCostKelly(0.6, 100, 100, -10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // STRK001A: Position Sizing from History Tests

    /// <summary>
    /// Insufficient trade history should return minimum position.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(19)]
    public void CalculateFromHistory_InsufficientHistory_ReturnsMinimumPosition(int tradeCount)
    {
        // Arrange
        STRK001A sizer = new STRK001A();
        List<Trade> trades = CreateWinningTradeHistory(tradeCount, 200);
        STCR004A signal = CreateSignal();

        // Act
        STRK002A position = sizer.CalculateFromHistory(100000, trades, 2.50, signal);

        // Assert
        position.Contracts.Should().BeGreaterThanOrEqualTo(1);
        position.AllocationPercent.Should().BeApproximately(0.01, 0.001,
            "Insufficient history should use 1% minimum allocation");
    }

    /// <summary>
    /// INVARIANT: Position allocation ≤ MaxAllocation (6%).
    /// </summary>
    [Fact]
    public void CalculateFromHistory_CapsAtMaxAllocation()
    {
        // Arrange - Create highly profitable history that would suggest high Kelly
        STRK001A sizer = new STRK001A();
        List<Trade> trades = CreateMixedTradeHistory(wins: 18, losses: 2, avgWin: 500, avgLoss: 100);
        STCR004A signal = CreateSignal();

        // Act
        STRK002A position = sizer.CalculateFromHistory(100000, trades, 2.50, signal);

        // Assert
        position.AllocationPercent.Should().BeLessThanOrEqualTo(0.06,
            "Allocation should be capped at 6%");
    }

    /// <summary>
    /// INVARIANT: Contracts ≥ 0 (never negative positions).
    /// </summary>
    [Fact]
    public void CalculateFromHistory_NeverReturnsNegativeContracts()
    {
        // Arrange - Create losing history
        STRK001A sizer = new STRK001A();
        List<Trade> trades = CreateMixedTradeHistory(wins: 2, losses: 18, avgWin: 100, avgLoss: 200);
        STCR004A signal = CreateSignal();

        // Act
        STRK002A position = sizer.CalculateFromHistory(100000, trades, 2.50, signal);

        // Assert
        position.Contracts.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Signal strength affects allocation.
    /// </summary>
    [Fact]
    public void CalculateFromHistory_WeakSignal_ReducesAllocation()
    {
        // Arrange
        STRK001A sizer = new STRK001A();
        List<Trade> trades = CreateMixedTradeHistory(wins: 14, losses: 6, avgWin: 200, avgLoss: 100);

        STCR004A strongSignal = CreateSignal(STCR004AStrength.Recommended);
        STCR004A weakSignal = CreateSignal(STCR004AStrength.Consider);

        // Act
        STRK002A strongPosition = sizer.CalculateFromHistory(100000, trades, 2.50, strongSignal);
        STRK002A weakPosition = sizer.CalculateFromHistory(100000, trades, 2.50, weakSignal);

        // Assert
        weakPosition.AllocationPercent.Should().BeLessThanOrEqualTo(strongPosition.AllocationPercent);
    }

    /// <summary>
    /// Avoid signal should result in zero allocation.
    /// </summary>
    [Fact]
    public void CalculateFromHistory_AvoidSignal_ReturnsZeroAllocation()
    {
        // Arrange
        STRK001A sizer = new STRK001A();
        List<Trade> trades = CreateMixedTradeHistory(wins: 14, losses: 6, avgWin: 200, avgLoss: 100);
        STCR004A avoidSignal = CreateSignal(STCR004AStrength.Avoid);

        // Act
        STRK002A position = sizer.CalculateFromHistory(100000, trades, 2.50, avoidSignal);

        // Assert
        position.AllocationPercent.Should().Be(0);
        position.Contracts.Should().Be(0);
    }

    /// <summary>
    /// Invalid portfolio value should throw.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-10000)]
    public void CalculateFromHistory_InvalidPortfolioValue_Throws(double portfolioValue)
    {
        // Arrange
        STRK001A sizer = new STRK001A();
        List<Trade> trades = CreateWinningTradeHistory(25, 200);
        STCR004A signal = CreateSignal();

        // Act & Assert
        Action act = () => sizer.CalculateFromHistory(portfolioValue, trades, 2.50, signal);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Invalid spread cost should throw.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-2.50)]
    public void CalculateFromHistory_InvalidSpreadCost_Throws(double spreadCost)
    {
        // Arrange
        STRK001A sizer = new STRK001A();
        List<Trade> trades = CreateWinningTradeHistory(25, 200);
        STCR004A signal = CreateSignal();

        // Act & Assert
        Action act = () => sizer.CalculateFromHistory(100000, trades, spreadCost, signal);
        act.Should().Throw<ArgumentException>();
    }

    // STRK002A: Position Size Validation Tests

    /// <summary>
    /// Valid position size should pass validation.
    /// </summary>
    [Fact]
    public void STRK002A_Validate_ValidPosition_DoesNotThrow()
    {
        // Arrange
        STRK002A position = new STRK002A
        {
            Contracts = 5,
            AllocationPercent = 0.05,
            DollarAllocation = 5000,
            MaxLossPerContract = 250,
            TotalRisk = 1250,
            KellyFraction = 0.10
        };

        // Act & Assert
        Action act = () => position.Validate();
        act.Should().NotThrow();
    }

    /// <summary>
    /// Negative contracts should fail validation.
    /// </summary>
    [Fact]
    public void STRK002A_Validate_NegativeContracts_Throws()
    {
        // Arrange
        STRK002A position = new STRK002A { Contracts = -1 };

        // Act & Assert
        Action act = () => position.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*negative*");
    }

    /// <summary>
    /// Allocation exceeding maximum should fail validation.
    /// </summary>
    [Fact]
    public void STRK002A_Validate_ExceedsMaxAllocation_Throws()
    {
        // Arrange
        STRK002A position = new STRK002A
        {
            Contracts = 100,
            AllocationPercent = 0.15,  // 15% > 10% default max
            DollarAllocation = 15000
        };

        // Act & Assert
        Action act = () => position.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exceeds*");
    }

    /// <summary>
    /// Custom max allocation should be respected.
    /// </summary>
    [Fact]
    public void STRK002A_Validate_CustomMaxAllocation_Respected()
    {
        // Arrange
        STRK002A position = new STRK002A
        {
            Contracts = 20,
            AllocationPercent = 0.15,  // 15%
            DollarAllocation = 15000
        };

        // Act & Assert - 20% max should pass
        Action act = () => position.Validate(maxAllocationPercent: 0.20);
        act.Should().NotThrow();
    }

    /// <summary>
    /// Risk exceeding allocation should fail validation.
    /// </summary>
    [Fact]
    public void STRK002A_Validate_RiskExceedsAllocation_Throws()
    {
        // Arrange
        STRK002A position = new STRK002A
        {
            Contracts = 5,
            AllocationPercent = 0.05,
            DollarAllocation = 5000,
            TotalRisk = 6000  // Risk > Allocation
        };

        // Act & Assert
        Action act = () => position.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*risk*");
    }

    /// <summary>
    /// RiskRewardRatio computed property should work correctly.
    /// </summary>
    [Fact]
    public void STRK002A_RiskRewardRatio_ComputesCorrectly()
    {
        // Arrange
        STRK002A position = new STRK002A
        {
            MaxLossPerContract = 200,
            ExpectedProfitPerContract = 400
        };

        // Act & Assert
        position.RiskRewardRatio.Should().BeApproximately(2.0, 1e-10);
    }

    /// <summary>
    /// RiskRewardRatio with zero max loss should be zero.
    /// </summary>
    [Fact]
    public void STRK002A_RiskRewardRatio_ZeroMaxLoss_ReturnsZero()
    {
        // Arrange
        STRK002A position = new STRK002A
        {
            MaxLossPerContract = 0,
            ExpectedProfitPerContract = 400
        };

        // Act & Assert
        position.RiskRewardRatio.Should().Be(0);
    }

    // STMG001A: Maturity Guard Tests

    /// <summary>
    /// Entry should be allowed above minimum maturity threshold.
    /// </summary>
    [Fact]
    public void STMG001A_EvaluateEntry_AboveMinimum_Allows()
    {
        // Arrange
        STMG001A guard = new STMG001A();
        double timeToExpiry = 10.0 / 252;  // 10 trading days (~0.04 years)

        // Act
        MaturityEntryResult result = guard.EvaluateEntry("TEST", timeToExpiry);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RecommendedAction.Should().Be(MaturityAction.Allow);
    }

    /// <summary>
    /// Entry should be rejected below minimum maturity threshold.
    /// </summary>
    [Fact]
    public void STMG001A_EvaluateEntry_BelowMinimum_Rejects()
    {
        // Arrange
        STMG001A guard = new STMG001A();
        double timeToExpiry = 3.0 / 252;  // 3 trading days - below default 5 day minimum

        // Act
        MaturityEntryResult result = guard.EvaluateEntry("TEST", timeToExpiry);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.RecommendedAction.Should().Be(MaturityAction.Reject);
    }

    /// <summary>
    /// Exit should not be required above force-exit threshold.
    /// </summary>
    [Fact]
    public void STMG001A_EvaluateExit_AboveThreshold_NoExit()
    {
        // Arrange
        STMG001A guard = new STMG001A();
        double timeToExpiry = 10.0 / 252;  // Well above threshold

        // Act
        MaturityExitResult result = guard.EvaluateExit("TEST", timeToExpiry);

        // Assert
        result.RequiresExit.Should().BeFalse();
        result.UrgencyLevel.Should().Be(ExitUrgency.None);
    }

    /// <summary>
    /// Exit should be immediate below force-exit threshold.
    /// </summary>
    [Fact]
    public void STMG001A_EvaluateExit_BelowThreshold_ImmediateExit()
    {
        // Arrange
        STMG001A guard = new STMG001A();
        double timeToExpiry = 1.0 / 252;  // 1 trading day - below default 3 day threshold

        // Act
        MaturityExitResult result = guard.EvaluateExit("TEST", timeToExpiry);

        // Assert
        result.RequiresExit.Should().BeTrue();
        result.UrgencyLevel.Should().Be(ExitUrgency.Immediate);
    }

    /// <summary>
    /// Custom thresholds should be respected.
    /// </summary>
    [Fact]
    public void STMG001A_CustomThresholds_Respected()
    {
        // Arrange - Very permissive thresholds
        double minEntry = 3.0 / 252;    // 3 days (minEntry must be > forceExit)
        double forceExit = 1.0 / 252;   // 1 day
        STMG001A guard = new STMG001A(minEntry, forceExit);

        // Act - 4 days should be allowed entry
        MaturityEntryResult entryResult = guard.EvaluateEntry("TEST", 4.0 / 252);

        // Assert
        entryResult.IsAllowed.Should().BeTrue();
        entryResult.RecommendedAction.Should().Be(MaturityAction.Allow);
    }

    /// <summary>
    /// Invalid threshold configuration should throw.
    /// </summary>
    [Fact]
    public void STMG001A_Constructor_InvalidThresholds_Throws()
    {
        // Arrange - ForceExit >= MinEntry (invalid)
        double minEntry = 3.0 / 252;
        double forceExit = 5.0 / 252;

        // Act & Assert
        Action act = () => _ = new STMG001A(minEntry, forceExit);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Time-to-expiry calculation should be reasonable.
    /// </summary>
    [Fact]
    public void STMG001A_CalculateTimeToExpiryApproximate_ReturnsReasonableValue()
    {
        // Arrange
        DateTime expiryDate = DateTime.Today.AddDays(30);  // 30 calendar days
        DateTime currentDate = DateTime.Today;

        // Act
        double tte = STMG001A.CalculateTimeToExpiryApproximate(expiryDate, currentDate);

        // Assert - Should be approximately 30 * (5/7) / 252 ≈ 0.085 years
        tte.Should().BeGreaterThan(0);
        tte.Should().BeLessThan(1.0);
        tte.Should().BeApproximately(30.0 * (5.0 / 7.0) / 252.0, 0.01);
    }

    /// <summary>
    /// Expired options (past expiry) should return zero or negative time.
    /// </summary>
    [Fact]
    public void STMG001A_CalculateTimeToExpiryApproximate_PastExpiry_ReturnsNonPositive()
    {
        // Arrange
        DateTime expiryDate = DateTime.Today.AddDays(-5);  // 5 days ago
        DateTime currentDate = DateTime.Today;

        // Act
        double tte = STMG001A.CalculateTimeToExpiryApproximate(expiryDate, currentDate);

        // Assert
        tte.Should().BeLessThanOrEqualTo(0);
    }

    // Trade Class Tests

    /// <summary>
    /// HoldingPeriod computed property should be correct.
    /// </summary>
    [Fact]
    public void Trade_HoldingPeriod_ComputesCorrectly()
    {
        // Arrange
        Trade trade = new Trade
        {
            EntryDate = new DateTime(2024, 1, 1),
            ExitDate = new DateTime(2024, 1, 11),
            ProfitLoss = 100
        };

        // Act & Assert
        trade.HoldingPeriod.Should().Be(10);
    }

    /// <summary>
    /// IsWinner should be true for positive P&L.
    /// </summary>
    [Theory]
    [InlineData(100, true)]
    [InlineData(0.01, true)]
    [InlineData(0, false)]
    [InlineData(-100, false)]
    public void Trade_IsWinner_BasedOnProfitLoss(double pl, bool expectedWinner)
    {
        // Arrange
        Trade trade = new Trade { ProfitLoss = pl };

        // Act & Assert
        trade.IsWinner.Should().Be(expectedWinner);
    }
}
