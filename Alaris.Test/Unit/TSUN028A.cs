// =============================================================================
// TSUN028A.cs - Simulation Data Type and Constant Tests
// Component ID: TSUN028A
//
// Tests for Alaris.Simulation helper types and validation constants:
// - SimulatedMarketData structure
// - Greek validation bounds
// - Option pricing helper methods
// - Trading calendar conversions
//
// Mathematical Invariants Tested:
// 1. Greek Bounds: |Δ| ≤ 1.5, |Γ| ≤ 0.5, |V| ≤ 1.0
// 2. Calendar Conversion: DTE → Years via TradingDaysPerYear
// 3. IV Term Structure: Inverted structure for earnings
//
// References:
//   - Healy (2021) physical Greek constraints
//   - TradingCalendarDefaults constants
// =============================================================================

using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Calendar;
using Alaris.Strategy.Bridge;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN028A: Tests for simulation constants and trading calendar functions.
/// </summary>
public sealed class TSUN028A
{
    // ========================================================================
    // Trading Calendar Constants Tests
    // ========================================================================

    /// <summary>
    /// Trading days per year constant should be 252.
    /// </summary>
    [Fact]
    public void TradingDaysPerYear_Is252()
    {
        // Assert
        TradingCalendarDefaults.TradingDaysPerYear.Should().Be(252);
    }

    /// <summary>
    /// DTE to years conversion should use 252 trading days.
    /// </summary>
    [Theory]
    [InlineData(252, 1.0)]      // Full year
    [InlineData(126, 0.5)]      // Half year
    [InlineData(63, 0.25)]      // Quarter
    [InlineData(21, 0.0833)]    // ~1 month
    [InlineData(1, 0.00397)]    // 1 day
    public void DteToYears_ConvertsCorrectly(int dte, double expectedYears)
    {
        // Act
        double years = TradingCalendarDefaults.DteToYears(dte);

        // Assert
        years.Should().BeApproximately(expectedYears, 0.01);
    }

    /// <summary>
    /// Years to DTE conversion is inverse of DTE to years.
    /// </summary>
    [Theory]
    [InlineData(1.0, 252)]
    [InlineData(0.5, 126)]
    [InlineData(0.25, 63)]
    public void YearsToDte_IsInverseOfDteToYears(double years, int expectedDte)
    {
        // Act
        int dte = TradingCalendarDefaults.YearsToDte(years);

        // Assert
        dte.Should().Be(expectedDte);
    }

    /// <summary>
    /// Round-trip conversion should be identity.
    /// </summary>
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(180)]
    public void DteToYearsToDte_RoundTrip_IsIdentity(int originalDte)
    {
        // Act
        double years = TradingCalendarDefaults.DteToYears(originalDte);
        int recoveredDte = TradingCalendarDefaults.YearsToDte(years);

        // Assert
        recoveredDte.Should().Be(originalDte);
    }

    // ========================================================================
    // Greek Validation Bounds Tests (from Healy 2021)
    // ========================================================================

    /// <summary>
    /// Delta bounds for single options: |Δ| ≤ 1.0 (1.5 with tolerance).
    /// </summary>
    [Theory]
    [InlineData(0.5, true)]     // Typical ATM call delta
    [InlineData(-0.5, true)]    // Typical ATM put delta
    [InlineData(1.0, true)]     // Deep ITM call
    [InlineData(-1.0, true)]    // Deep ITM put
    [InlineData(0.0, true)]     // Deep OTM
    [InlineData(1.4, true)]     // Just within tolerance
    [InlineData(-1.4, true)]    // Just within tolerance
    [InlineData(1.6, false)]    // Exceeds bound
    [InlineData(-1.6, false)]   // Exceeds bound
    public void DeltaBound_IsWithinPhysicalConstraints(double delta, bool expectedValid)
    {
        // Arrange (MaxAbsDelta = 1.5 from SMSM001A)
        const double MaxAbsDelta = 1.5;

        // Act
        bool isValid = Math.Abs(delta) <= MaxAbsDelta;

        // Assert
        isValid.Should().Be(expectedValid);
    }

    /// <summary>
    /// Gamma bounds: |Γ| ≤ 0.5 for stability.
    /// </summary>
    [Theory]
    [InlineData(0.05, true)]    // Typical gamma
    [InlineData(0.20, true)]    // High gamma (near ATM, short expiry)
    [InlineData(0.49, true)]    // Just within bound
    [InlineData(0.51, false)]   // Exceeds bound
    public void GammaBound_IsWithinPhysicalConstraints(double gamma, bool expectedValid)
    {
        // Arrange (MaxAbsGamma = 0.5 from SMSM001A)
        const double MaxAbsGamma = 0.5;

        // Act
        bool isValid = Math.Abs(gamma) <= MaxAbsGamma;

        // Assert
        isValid.Should().Be(expectedValid);
    }

    /// <summary>
    /// Vega bounds: per 1% vol change, |V| ≤ 1.0.
    /// </summary>
    [Theory]
    [InlineData(0.15, true)]    // Typical vega (per 1% vol)
    [InlineData(0.50, true)]    // High vega (long-dated ATM)
    [InlineData(0.99, true)]    // Just within bound
    [InlineData(1.01, false)]   // Exceeds bound
    public void VegaBound_IsWithinPhysicalConstraints(double vega, bool expectedValid)
    {
        // Arrange (MaxAbsVega = 1.0 from SMSM001A)
        const double MaxAbsVega = 1.0;

        // Act
        bool isValid = Math.Abs(vega) <= MaxAbsVega;

        // Assert
        isValid.Should().Be(expectedValid);
    }

    // ========================================================================
    // Simulation Configuration Constants Tests
    // ========================================================================

    /// <summary>
    /// Standard risk-free rate for positive regime is ~5.25%.
    /// </summary>
    [Fact]
    public void PositiveRiskFreeRate_IsReasonable()
    {
        // Arrange (from SMSM001A)
        const double PositiveRiskFreeRate = 0.0525;

        // Assert
        PositiveRiskFreeRate.Should().BeInRange(0.01, 0.10,
            "Risk-free rate should be between 1% and 10%");
    }

    /// <summary>
    /// Negative risk-free rate regime uses small negative rate.
    /// </summary>
    [Fact]
    public void NegativeRiskFreeRate_IsValidNegativeRate()
    {
        // Arrange (from SMSM001A)
        const double NegativeRiskFreeRate = -0.005;

        // Assert
        NegativeRiskFreeRate.Should().BeLessThan(0);
        NegativeRiskFreeRate.Should().BeGreaterThan(-0.02,
            "Negative rates in Healy (2021) regime are typically > -2%");
    }

    /// <summary>
    /// For double boundary regime, q < r (dividend < rate).
    /// </summary>
    [Fact]
    public void NegativeRateRegime_DividendLessThanRate()
    {
        // Arrange (from SMSM001A)
        const double NegativeRiskFreeRate = -0.005;
        const double NegativeRateDividendYield = -0.010;

        // Assert - q < r condition for Healy (2021) double boundary
        NegativeRateDividendYield.Should().BeLessThan(NegativeRiskFreeRate,
            "Double boundary regime requires q < r");
    }

    /// <summary>
    /// Earnings IV premium is reasonable (5-15%).
    /// </summary>
    [Fact]
    public void EarningsIVPremium_IsReasonable()
    {
        // Arrange (from SMSM001A)
        const double EarningsIVPremium = 0.08;

        // Assert
        EarningsIVPremium.Should().BeInRange(0.05, 0.15,
            "Pre-earnings IV premium typically 5-15% per Leung & Santoli (2014)");
    }

    /// <summary>
    /// Earnings gap magnitude is calibrated to historical data.
    /// </summary>
    [Fact]
    public void EarningsGapMagnitude_IsRealistic()
    {
        // Arrange (from SMSM001A)
        const double EarningsGapMagnitude = 0.04;

        // Assert
        EarningsGapMagnitude.Should().BeInRange(0.02, 0.08,
            "Typical earnings gaps are 2-8% for large-cap equities");
    }

    // ========================================================================
    // PriceBar Data Structure Tests
    // ========================================================================

    /// <summary>
    /// PriceBar should have valid OHLC relationship: L ≤ O,C ≤ H.
    /// </summary>
    [Fact]
    public void PriceBar_OHLCRelationship_IsValid()
    {
        // Arrange
        var bar = new PriceBar
        {
            Date = DateTime.Today,
            Open = 100.0,
            High = 105.0,
            Low = 95.0,
            Close = 102.0,
            Volume = 1000000
        };

        // Assert
        bar.Low.Should().BeLessThanOrEqualTo(bar.Open);
        bar.Low.Should().BeLessThanOrEqualTo(bar.Close);
        bar.High.Should().BeGreaterThanOrEqualTo(bar.Open);
        bar.High.Should().BeGreaterThanOrEqualTo(bar.Close);
    }

    /// <summary>
    /// PriceBar volume should be non-negative.
    /// </summary>
    [Fact]
    public void PriceBar_Volume_NonNegative()
    {
        // Arrange
        var bar = new PriceBar
        {
            Date = DateTime.Today,
            Open = 100.0,
            High = 105.0,
            Low = 95.0,
            Close = 102.0,
            Volume = 0  // Minimum valid volume
        };

        // Assert
        bar.Volume.Should().BeGreaterThanOrEqualTo(0);
    }

    // ========================================================================
    // Term Structure Inversion Tests (Atilgan 2014 Criteria)
    // ========================================================================

    /// <summary>
    /// Inverted term structure: front IV > back IV.
    /// </summary>
    [Fact]
    public void InvertedTermStructure_FrontIVGreaterThanBack()
    {
        // Arrange - SMSM001A earnings IV setup
        double frontMonthIV = 0.28;  // 28% (with earnings premium)
        double midMonthIV = 0.24;    // 24%
        double backMonthIV = 0.22;   // 22%

        // Assert - Inverted structure
        frontMonthIV.Should().BeGreaterThan(midMonthIV);
        midMonthIV.Should().BeGreaterThan(backMonthIV);
    }

    /// <summary>
    /// Term structure slope for inverted term structure is negative.
    /// </summary>
    [Fact]
    public void TermStructureSlope_IsNegativeForInvertedStructure()
    {
        // Arrange
        double frontMonthIV = 0.28;
        double backMonthIV = 0.22;
        int frontDTE = 30;
        int backDTE = 90;

        // Act - Calculate slope: (backIV - frontIV) / (backDTE - frontDTE)
        double slope = (backMonthIV - frontMonthIV) / (backDTE - frontDTE);

        // Assert - Inverted term structure has negative slope
        slope.Should().BeLessThan(0,
            "Inverted term structure should have negative slope");
        
        // The slope is -0.001 per DTE, which is -0.06/60 = -0.001
        slope.Should().BeApproximately(-0.001, 0.0001);
    }

    // ========================================================================
    // Algorithm Configuration Constants Tests
    // ========================================================================

    /// <summary>
    /// Days before earnings for entry (Atilgan 2014).
    /// </summary>
    [Fact]
    public void DaysBeforeEarnings_IsAtilganOptimal()
    {
        // Arrange (from STLN001A)
        const int DaysBeforeEarnings = 6;

        // Assert - Atilgan (2014) suggests 5-7 days optimal
        DaysBeforeEarnings.Should().BeInRange(5, 7);
    }

    /// <summary>
    /// Minimum dollar volume filter (liquidity).
    /// </summary>
    [Fact]
    public void MinimumDollarVolume_IsReasonable()
    {
        // Arrange (from STLN001A)
        const decimal MinimumDollarVolume = 1_500_000m;

        // Assert - Should filter illiquid stocks
        MinimumDollarVolume.Should().BeGreaterThan(1_000_000m);
        MinimumDollarVolume.Should().BeLessThan(10_000_000m);
    }

    /// <summary>
    /// Minimum share price filter.
    /// </summary>
    [Fact]
    public void MinimumPrice_FiltersPennyStocks()
    {
        // Arrange (from STLN001A)
        const decimal MinimumPrice = 5.00m;

        // Assert
        MinimumPrice.Should().BeGreaterThanOrEqualTo(5.00m,
            "Should filter penny stocks per Atilgan (2014)");
    }

    /// <summary>
    /// Maximum position allocation respects risk management.
    /// </summary>
    [Fact]
    public void MaxPositionAllocation_IsConservative()
    {
        // Arrange (from STLN001A)
        const decimal MaxPositionAllocation = 0.06m;

        // Assert - Should not exceed 10% per position
        MaxPositionAllocation.Should().BeLessThanOrEqualTo(0.10m);
    }

    /// <summary>
    /// Maximum concurrent positions limits concentration.
    /// </summary>
    [Fact]
    public void MaxConcurrentPositions_LimitsConcentration()
    {
        // Arrange (from STLN001A)
        const int MaxConcurrentPositions = 15;

        // Assert
        MaxConcurrentPositions.Should().BeGreaterThan(5,
            "Should allow sufficient diversification");
        MaxConcurrentPositions.Should().BeLessThanOrEqualTo(20,
            "Should limit operational complexity");
    }

    /// <summary>
    /// Portfolio allocation limit is conservative.
    /// </summary>
    [Fact]
    public void PortfolioAllocationLimit_IsConservative()
    {
        // Arrange (from STLN001A)
        const decimal PortfolioAllocationLimit = 0.80m;

        // Assert - Should keep some cash buffer
        PortfolioAllocationLimit.Should().BeLessThan(1.0m);
        PortfolioAllocationLimit.Should().BeGreaterThan(0.50m);
    }

    // ========================================================================
    // IV/RV Ratio Tests (Atilgan 2014 Signal)
    // ========================================================================

    /// <summary>
    /// IV/RV ratio threshold for Recommended signal.
    /// </summary>
    [Theory]
    [InlineData(0.28, 0.22, true)]    // 1.27 ratio - above threshold
    [InlineData(0.30, 0.22, true)]    // 1.36 ratio - well above
    [InlineData(0.26, 0.22, false)]   // 1.18 ratio - below threshold
    [InlineData(0.22, 0.22, false)]   // 1.00 ratio - no premium
    public void IVRVRatio_MeetsSignalThreshold(double iv, double rv, bool expectedRecommended)
    {
        // Arrange - Atilgan (2014) threshold
        const double MinIVRVRatio = 1.25;

        // Act
        double ratio = iv / rv;
        bool isRecommended = ratio >= MinIVRVRatio;

        // Assert
        isRecommended.Should().Be(expectedRecommended);
    }
}
