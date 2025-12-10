using System;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Hedge;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STHD007A - IV-Based Early Exit Monitor.
/// Tests early exit triggers based on IV crush behaviour.
/// </summary>
public sealed class STHD007ATests
{
    private readonly STHD007A _exitMonitor;

    public STHD007ATests()
    {
        _exitMonitor = new STHD007A();
    }

    [Fact]
    public void Evaluate_WhenIVCrushesFasterThanExpected_ReturnsExitDecision()
    {
        // Arrange: IV has dropped well below expected post-earnings level
        string symbol = "AAPL";
        double currentIV = 0.20;           // Current IV at 20%
        double entryIV = 0.45;             // Entry IV at 45%
        double expectedPostEarningsIV = 0.30;  // Expected post-earnings at 30%
        double expectedCrush = 0.15;       // Expected 15 vol point crush
        int daysToExpiry = 10;             // Still 10 days to go

        // Act
        var result = _exitMonitor.Evaluate(
            symbol, currentIV, entryIV, expectedPostEarningsIV, expectedCrush, daysToExpiry);

        // Assert: Should trigger exit (current IV < 70% of expected post-earnings)
        result.ShouldExit.Should().BeTrue();
        result.RecommendedAction.Should().Be(EarlyExitAction.ExitNow);
        result.CrushRatio.Should().BeGreaterThan(1.0); // Crushed more than expected
    }

    [Fact]
    public void Evaluate_WhenCrushRatioExceedsThreshold_ReturnsExitDecision()
    {
        // Arrange: 85% of expected crush has been realised
        string symbol = "MSFT";
        double currentIV = 0.32;           // Current IV at 32%
        double entryIV = 0.45;             // Entry IV at 45%
        double expectedPostEarningsIV = 0.30;  // Expected at 30%
        double expectedCrush = 0.15;       // Expected 15 vol points
        int daysToExpiry = 8;              // Still 8 days remaining

        // Act
        var result = _exitMonitor.Evaluate(
            symbol, currentIV, entryIV, expectedPostEarningsIV, expectedCrush, daysToExpiry);

        // Assert: Actual crush = 0.13, ratio = 0.87 > 0.80 threshold
        result.CrushRatio.Should().BeApproximately(0.87, 0.01);
        result.ShouldExit.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WhenIVWithinExpectedRange_ReturnsHold()
    {
        // Arrange: IV crush is progressing normally
        string symbol = "GOOGL";
        double currentIV = 0.40;           // Current IV at 40%
        double entryIV = 0.45;             // Entry IV at 45%
        double expectedPostEarningsIV = 0.30;  // Expected at 30%
        double expectedCrush = 0.15;       // Expected 15 vol points
        int daysToExpiry = 10;

        // Act
        var result = _exitMonitor.Evaluate(
            symbol, currentIV, entryIV, expectedPostEarningsIV, expectedCrush, daysToExpiry);

        // Assert: Actual crush = 0.05, ratio = 0.33 < 0.80 threshold
        result.ShouldExit.Should().BeFalse();
        result.RecommendedAction.Should().Be(EarlyExitAction.Hold);
        result.CrushRatio.Should().BeApproximately(0.33, 0.01);
    }

    [Fact]
    public void Evaluate_WhenNearExpiryWithPartialCrush_ReturnsExit()
    {
        // Arrange: Close to expiry with 50% crush captured
        string symbol = "TSLA";
        double currentIV = 0.375;          // Current IV
        double entryIV = 0.45;             // Entry IV
        double expectedPostEarningsIV = 0.30;
        double expectedCrush = 0.15;
        int daysToExpiry = 2;              // Only 2 days left

        // Act
        var result = _exitMonitor.Evaluate(
            symbol, currentIV, entryIV, expectedPostEarningsIV, expectedCrush, daysToExpiry);

        // Assert: Should exit due to approaching expiry
        result.ShouldExit.Should().BeTrue();
        result.Reason.Should().Contain("Near expiry");
    }

    [Fact]
    public void Evaluate_CapturedProfitPercent_IsCalculatedCorrectly()
    {
        // Arrange
        string symbol = "NVDA";
        double currentIV = 0.33;           // 80% crush realised
        double entryIV = 0.45;
        double expectedPostEarningsIV = 0.30;
        double expectedCrush = 0.15;
        int daysToExpiry = 5;

        // Act
        var result = _exitMonitor.Evaluate(
            symbol, currentIV, entryIV, expectedPostEarningsIV, expectedCrush, daysToExpiry);

        // Assert: CapturedProfitPercent should be ~76% (80% × 0.95 concavity factor)
        result.CrushRatio.Should().BeApproximately(0.80, 0.01);
        result.CapturedProfitPercent.Should().BeApproximately(0.76, 0.05);
    }

    [Theory]
    [InlineData("")]       // Empty symbol
    [InlineData(null)]     // Null symbol
    [InlineData("   ")]    // Whitespace symbol
    public void Evaluate_WithInvalidSymbol_ThrowsArgumentException(string? symbol)
    {
        // Act & Assert
        var act = () => _exitMonitor.Evaluate(
            symbol!, 0.30, 0.45, 0.30, 0.15, 10);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.1, 0.45)]   // Negative current IV
    [InlineData(6.0, 0.45)]    // Current IV > 500%
    [InlineData(0.30, -0.1)]   // Negative entry IV
    [InlineData(0.30, 0.0)]    // Zero entry IV
    public void Evaluate_WithInvalidIV_ThrowsArgumentOutOfRangeException(
        double currentIV, double entryIV)
    {
        // Act & Assert
        var act = () => _exitMonitor.Evaluate(
            "TEST", currentIV, entryIV, 0.30, 0.15, 10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Evaluate_ResultSummary_IsHumanReadable()
    {
        // Arrange
        var result = _exitMonitor.Evaluate(
            "AAPL", 0.35, 0.45, 0.30, 0.15, 10);

        // Act
        string summary = result.Summary;

        // Assert
        summary.Should().StartWith("AAPL:");
        summary.Should().Contain(result.RecommendedAction.ToString());
    }

    [Fact]
    public void Evaluate_WithZeroExpectedCrush_HandlesGracefully()
    {
        // Arrange: Edge case where no crush expected
        var result = _exitMonitor.Evaluate(
            "TEST", 0.30, 0.30, 0.30, 0.0, 10);

        // Assert: CrushRatio should be 0 (not NaN or Infinity)
        result.CrushRatio.Should().Be(0);
        result.ShouldExit.Should().BeFalse();
    }
}
