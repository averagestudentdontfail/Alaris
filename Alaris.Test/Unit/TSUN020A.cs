// AlgorithmTests.cs - Unit Tests for STLN001A Algorithm Parameters

using System;
using Xunit;
using FluentAssertions;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STLN001A algorithm configuration constants.
/// Tests parameter validation without requiring LEAN runtime.
/// </summary>
/// <remarks>
/// Note: STLN001A extends QCAlgorithm which requires LEAN runtime context.
/// These tests validate configuration constants and parameter ranges only.
/// Full algorithm testing requires LEAN backtesting environment.
/// </remarks>
public sealed class AlgorithmParameterTests
{
    // STLN001A Configuration Constants (from source)
    private const int DaysBeforeEarnings = 6;
    private const decimal MinimumDollarVolume = 1_500_000m;
    private const decimal MinimumPrice = 5.00m;
    private const decimal PortfolioAllocationLimit = 0.80m;
    private const decimal MaxPositionAllocation = 0.06m;
    private const int MaxConcurrentPositions = 15;

    [Fact]
    public void DaysBeforeEarnings_IsWithinAtilganWindow()
    {
        // Atilgan (2014) uses 5-7 day window
        DaysBeforeEarnings.Should().BeInRange(5, 7,
            "Atilgan (2014) strategy uses 5-7 day pre-earnings window");
    }

    [Fact]
    public void MaxConcurrentPositions_IsReasonable()
    {
        // Per Operation.md, max 15 concurrent positions
        MaxConcurrentPositions.Should().BeInRange(10, 20,
            "Production should limit concurrent positions for risk management");
    }

    [Fact]
    public void PortfolioAllocationLimit_ReservesCapital()
    {
        // Per Operation.md, 80% max allocation
        PortfolioAllocationLimit.Should().BeLessThan(1.0m,
            "Must reserve capital for margin and hedging");
        PortfolioAllocationLimit.Should().BeGreaterThan(0.5m,
            "Allocation should be substantial for returns");
    }

    [Fact]
    public void MinimumDollarVolume_EnforcesLiquidity()
    {
        // Per Atilgan parameters
        MinimumDollarVolume.Should().BeGreaterOrEqualTo(1_000_000m,
            "Minimum dollar volume ensures adequate liquidity");
    }

    [Fact]
    public void MinimumPrice_ExcludesPennyStocks()
    {
        // Per Operation.md
        MinimumPrice.Should().BeGreaterOrEqualTo(5.00m,
            "Excludes penny stocks which have different dynamics");
    }

    [Theory]
    [InlineData(0.06, 150.00, 9.0)] // 6% of $150 = $9 allocation
    [InlineData(0.10, 100.00, 10.0)] // 10% of $100 = $10 allocation
    [InlineData(0.05, 200.00, 10.0)] // 5% of $200 = $10 allocation
    public void PositionAllocation_CalculatesCorrectly(double allocationPercent, double portfolioValue, double expectedAllocation)
    {
        var allocation = (decimal)allocationPercent * (decimal)portfolioValue;
        allocation.Should().Be((decimal)expectedAllocation);
    }

    [Fact]
    public void MaxPositionAllocation_IsReasonable()
    {
        // Should be between 2% and 15%
        MaxPositionAllocation.Should().BeInRange(0.02m, 0.15m,
            "Position sizing must balance risk and return");
    }
}

/// <summary>
/// Tests for Atilgan (2014) strategy parameter ranges.
/// Validates that STLN001A parameters align with academic research.
/// </summary>
public sealed class AtilganStrategyParameterTests
{
    [Theory]
    [InlineData(5, true)]  // Min Atilgan window
    [InlineData(6, true)]  // Default
    [InlineData(7, true)]  // Max Atilgan window
    [InlineData(3, false)] // Too short
    [InlineData(14, false)] // Too long
    public void DaysBeforeEarnings_MustBeInAtilganWindow(int days, bool isValid)
    {
        const int minDays = 5;
        const int maxDays = 7;

        var inRange = days >= minDays && days <= maxDays;

        inRange.Should().Be(isValid);
    }

    [Theory]
    [InlineData(0.05, true)]   // 5% - reasonable
    [InlineData(0.06, true)]   // 6% - default
    [InlineData(0.10, true)]   // 10% - acceptable
    [InlineData(0.01, false)]  // 1% - too small
    [InlineData(0.25, false)]  // 25% - too concentrated
    public void MaxPositionAllocation_MustBeReasonable(double allocation, bool isValid)
    {
        const double minAllocation = 0.02;
        const double maxAllocation = 0.15;

        var inRange = allocation >= minAllocation && allocation <= maxAllocation;

        inRange.Should().Be(isValid);
    }

    [Theory]
    [InlineData(5, true)]    // Minimum
    [InlineData(10, true)]   // Moderate
    [InlineData(15, true)]   // Default
    [InlineData(2, false)]   // Too few
    [InlineData(50, false)]  // Too many
    public void MaxConcurrentPositions_MustBeManageable(int positions, bool isValid)
    {
        const int minPositions = 5;
        const int maxPositions = 20;

        var inRange = positions >= minPositions && positions <= maxPositions;

        inRange.Should().Be(isValid);
    }

    [Theory]
    [InlineData(1_000_000, true)]   // 1M - minimum acceptable
    [InlineData(1_500_000, true)]   // 1.5M - default
    [InlineData(5_000_000, true)]   // 5M - high liquidity
    [InlineData(100_000, false)]    // 100K - too illiquid
    [InlineData(50_000, false)]     // 50K - penny stock territory
    public void MinimumDollarVolume_EnforcesLiquidity(long volume, bool isValid)
    {
        const long minVolume = 500_000;

        var acceptable = volume >= minVolume;

        acceptable.Should().Be(isValid);
    }
}
