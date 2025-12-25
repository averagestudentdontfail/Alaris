using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Risk;
using Alaris.Strategy.Core;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STRK003A - Concurrent Position Reserve Manager.
/// Tests reserve-adjusted Kelly sizing with Little's Law calculations.
/// </summary>
public sealed class STRK003ATests
{
    private readonly STRK001A _baseKelly;
    private readonly STRK003A _reserveManager;

    public STRK003ATests()
    {
        _baseKelly = new STRK001A();
        _reserveManager = new STRK003A(_baseKelly);
    }

    [Fact]
    public void CalculateExpectedConcurrent_WithValidInputs_ReturnsLittlesLawResult()
    {
        // Arrange
        double arrivalRate = 0.4;      // 0.4 signals per day
        double holdingPeriod = 20.0;   // 20 days average holding

        // Act
        double expected = _reserveManager.CalculateExpectedConcurrent(arrivalRate, holdingPeriod);

        // Assert: Little's Law: L = λ × W
        expected.Should().BeApproximately(8.0, 0.001);
    }

    [Theory]
    [InlineData(0.0, 20.0)]    // Zero arrival rate
    [InlineData(-0.1, 20.0)]   // Negative arrival rate
    [InlineData(0.4, 0.0)]     // Zero holding period
    [InlineData(0.4, -5.0)]    // Negative holding period
    public void CalculateExpectedConcurrent_WithInvalidInputs_ThrowsArgumentOutOfRangeException(
        double arrivalRate, double holdingPeriod)
    {
        // Act & Assert
        var act = () => _reserveManager.CalculateExpectedConcurrent(arrivalRate, holdingPeriod);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateExpectedConcurrentFromHistory_WithValidTrades_ReturnsExpectedValue()
    {
        // Arrange: 50 trades over ~125 days with avg holding of 5 days
        var trades = GenerateHistoricalTrades(50, 5);

        // Act
        double expected = _reserveManager.CalculateExpectedConcurrentFromHistory(trades);

        // Assert: arrival rate ≈ 50/125 ≈ 0.4, holding = 5 → expected ≈ 2
        expected.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateExpectedConcurrentFromHistory_WithInsufficientTrades_ThrowsArgumentException()
    {
        // Arrange: Only 5 trades (< 10 minimum)
        var trades = GenerateHistoricalTrades(5, 5);

        // Act & Assert
        var act = () => _reserveManager.CalculateExpectedConcurrentFromHistory(trades);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 10 trades*");
    }

    [Fact]
    public void CalculateWithReserve_WhenUtilisationHigh_RejectsConsiderSignals()
    {
        // Arrange
        var trades = GenerateHistoricalTrades(30, 5);
        var signal = CreateSignal("AAPL", STCR004AStrength.Consider);

        // Act: 8 open positions with expected 8 = 100% utilisation
        var position = _reserveManager.CalculateWithReserve(
            portfolioValue: 100000,
            historicalTrades: trades,
            spreadCost: 2.0,
            signal: signal,
            currentOpenPositions: 8,
            expectedConcurrent: 8.0,
            reserveBuffer: 1.0  // No buffer = utilisation at 100%
        );

        // Assert: Should return zero position (filtered)
        position.Contracts.Should().Be(0);
    }

    [Fact]
    public void CalculateWithReserve_WhenUtilisationLow_AcceptsConsiderSignals()
    {
        // Arrange
        var trades = GenerateHistoricalTrades(30, 5);
        var signal = CreateSignal("AAPL", STCR004AStrength.Consider);

        // Act: 2 open positions with expected 10 = 20% utilisation
        var position = _reserveManager.CalculateWithReserve(
            portfolioValue: 100000,
            historicalTrades: trades,
            spreadCost: 2.0,
            signal: signal,
            currentOpenPositions: 2,
            expectedConcurrent: 10.0,
            reserveBuffer: 1.3
        );

        // Assert: Should accept with reduced allocation
        position.AllocationPercent.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateWithReserve_AppliesReserveBufferToAllocation()
    {
        // Arrange
        var trades = GenerateHistoricalTrades(30, 5);
        var signal = CreateSignal("AAPL", STCR004AStrength.Recommended);

        // Act: Calculate with different buffer values
        var position1 = _reserveManager.CalculateWithReserve(
            portfolioValue: 100000,
            historicalTrades: trades,
            spreadCost: 2.0,
            signal: signal,
            currentOpenPositions: 0,
            expectedConcurrent: 8.0,
            reserveBuffer: 1.0  // No buffer
        );

        var position2 = _reserveManager.CalculateWithReserve(
            portfolioValue: 100000,
            historicalTrades: trades,
            spreadCost: 2.0,
            signal: signal,
            currentOpenPositions: 0,
            expectedConcurrent: 8.0,
            reserveBuffer: 2.0  // Double buffer
        );

        // Assert: Higher buffer = lower allocation
        position2.AllocationPercent.Should().BeLessThanOrEqualTo(position1.AllocationPercent);
    }

    [Theory]
    [InlineData(-1000)]   // Negative portfolio
    [InlineData(0)]       // Zero portfolio
    public void CalculateWithReserve_WithInvalidPortfolio_ThrowsArgumentOutOfRangeException(double portfolio)
    {
        // Arrange
        var trades = GenerateHistoricalTrades(30, 5);
        var signal = CreateSignal("AAPL", STCR004AStrength.Recommended);

        // Act & Assert
        var act = () => _reserveManager.CalculateWithReserve(
            portfolioValue: portfolio,
            historicalTrades: trades,
            spreadCost: 2.0,
            signal: signal,
            currentOpenPositions: 0,
            expectedConcurrent: 8.0
        );
        act.Should().Throw<ArgumentOutOfRangeException>();
    }


    private static List<Trade> GenerateHistoricalTrades(int count, int avgHoldingDays)
    {
        var trades = new List<Trade>();
        var baseDate = DateTime.Today.AddDays(-count * 2.5); // Space out entries

        for (int i = 0; i < count; i++)
        {
            var entryDate = baseDate.AddDays(i * 2.5);
            var holdingPeriod = avgHoldingDays + (i % 3 - 1); // Vary ±1 day

            trades.Add(new Trade
            {
                Symbol = i % 2 == 0 ? "AAPL" : "MSFT",
                EntryDate = entryDate,
                ExitDate = entryDate.AddDays(holdingPeriod),
                ProfitLoss = (i % 3 == 0) ? -100 : 150  // 2/3 winners
            });
        }

        return trades;
    }

    private static STCR004A CreateSignal(string symbol, STCR004AStrength strength)
    {
        return new STCR004A
        {
            Symbol = symbol,
            Strength = strength,
            IVRVRatio = 1.30,
            STTM001ASlope = -0.005,
            AverageVolume = 2_000_000,
            EarningsDate = DateTime.Today.AddDays(5),
            STCR004ADate = DateTime.Today
        };
    }

}
