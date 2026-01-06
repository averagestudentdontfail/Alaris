using System;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;
using Alaris.Strategy.Bridge;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STIV005A.
/// Tests calibration of sigma_e from historical earnings data.
///
/// Reference: "Accounting for Earnings Announcements in the Pricing of Equity Options"
/// Tim Leung & Marco Santoli (2014), Section 5.2
/// </summary>
public class STIV005ATests
{
    private readonly STIV005A _calibrator;

    public STIV005ATests()
    {
        _calibrator = new STIV005A();
    }

    // CalibrateFromMoves Tests

    [Fact]
    public void CalibrateFromMoves_WithValidData_ReturnsCorrectSigmaE()
    {
        // Arrange - historical log-returns with known standard deviation
        double[] moves = { 0.05, -0.03, 0.08, -0.04, 0.06, -0.02, 0.04, -0.05 };

        // Act
        double? sigmaE = _calibrator.CalibrateFromMoves(moves);

        // Assert
        sigmaE.Should().NotBeNull();
        sigmaE.Should().BeGreaterThan(0);
        sigmaE.Should().BeLessThan(0.10); // Reasonable range for typical earnings moves
    }

    [Fact]
    public void CalibrateFromMoves_WithInsufficientData_ReturnsNull()
    {
        // Arrange - less than 4 samples (minimum required)
        double[] moves = { 0.05, -0.03, 0.08 };

        // Act
        double? sigmaE = _calibrator.CalibrateFromMoves(moves);

        // Assert
        sigmaE.Should().BeNull();
    }

    [Fact]
    public void CalibrateFromMoves_WithNullArray_ReturnsNull()
    {
        // Act
        double? sigmaE = _calibrator.CalibrateFromMoves(null!);

        // Assert
        sigmaE.Should().BeNull();
    }

    [Fact]
    public void CalibrateFromMoves_WithConstantMoves_ReturnsMinSigmaE()
    {
        // Arrange - all same values, variance = 0
        double[] moves = { 0.05, 0.05, 0.05, 0.05 };

        // Act
        double? sigmaE = _calibrator.CalibrateFromMoves(moves);

        // Assert - should return minimum sigma_e (0.001)
        sigmaE.Should().NotBeNull();
        sigmaE.Should().Be(0.001);
    }

    [Fact]
    public void CalibrateFromMoves_WithLargeMoves_ClampsToMax()
    {
        // Arrange - extreme moves that would exceed max
        double[] moves = { 2.0, -2.0, 1.5, -1.5, 2.5, -2.5, 3.0, -3.0 };

        // Act
        double? sigmaE = _calibrator.CalibrateFromMoves(moves);

        // Assert - should be clamped to max (1.0)
        sigmaE.Should().NotBeNull();
        sigmaE.Should().BeLessOrEqualTo(1.0);
    }

    // Calibrate Tests (with price data)

    [Fact]
    public void Calibrate_WithValidPricesAndDates_ReturnsValidCalibration()
    {
        // Arrange
        string symbol = "TEST";
        List<PriceBar> prices = CreateSamplePriceData();
        List<DateTime> earningsDates = CreateSampleEarningsDates();

        // Act
        EarningsJumpCalibration result = _calibrator.Calibrate(symbol, prices, earningsDates);

        // Assert
        result.Should().NotBeNull();
        result.Symbol.Should().Be(symbol);
        result.IsValid.Should().BeTrue();
        result.SigmaE.Should().NotBeNull();
        result.SampleCount.Should().BeGreaterOrEqualTo(4);
    }

    [Fact]
    public void Calibrate_WithInsufficientEarningsData_ReturnsInvalidCalibration()
    {
        // Arrange
        string symbol = "TEST";
        List<PriceBar> prices = CreateSamplePriceData();
        List<DateTime> earningsDates = new List<DateTime> { DateTime.Today.AddDays(-90) }; // Only 1 earnings

        // Act
        EarningsJumpCalibration result = _calibrator.Calibrate(symbol, prices, earningsDates);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.SigmaE.Should().BeNull();
    }

    [Fact]
    public void Calibrate_ThrowsOnNullSymbol()
    {
        // Arrange
        List<PriceBar> prices = CreateSamplePriceData();
        List<DateTime> earningsDates = CreateSampleEarningsDates();

        // Act & Assert
        Action act = () => _calibrator.Calibrate(null!, prices, earningsDates);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Calibrate_ThrowsOnNullPrices()
    {
        // Arrange
        List<DateTime> earningsDates = CreateSampleEarningsDates();

        // Act & Assert
        Action act = () => _calibrator.Calibrate("TEST", null!, earningsDates);
        act.Should().Throw<ArgumentNullException>();
    }

    // STTM001AEstimator Tests

    [Fact]
    public void STTM001AEstimator_WithValidInvertedSTTM001A_ReturnsSigmaE()
    {
        // Arrange - inverted term structure (short-dated IV > long-dated IV)
        double iv1 = 0.35; // 7-day IV
        int dte1 = 7;
        double iv2 = 0.25; // 30-day IV
        int dte2 = 30;

        // Act
        double? sigmaE = STIV005A.STTM001AEstimator(iv1, dte1, iv2, dte2);

        // Assert
        sigmaE.Should().NotBeNull();
        sigmaE.Should().BeGreaterThan(0);
    }

    [Fact]
    public void STTM001AEstimator_WithFlatSTTM001A_ReturnsNull()
    {
        // Arrange - flat term structure (iv1 <= iv2)
        double iv1 = 0.25;
        int dte1 = 7;
        double iv2 = 0.25;
        int dte2 = 30;

        // Act
        double? sigmaE = STIV005A.STTM001AEstimator(iv1, dte1, iv2, dte2);

        // Assert
        sigmaE.Should().BeNull();
    }

    [Fact]
    public void STTM001AEstimator_WithInvalidDTE_ReturnsNull()
    {
        // Arrange - dte1 >= dte2 is invalid
        double iv1 = 0.35;
        int dte1 = 30;
        double iv2 = 0.25;
        int dte2 = 7;

        // Act
        double? sigmaE = STIV005A.STTM001AEstimator(iv1, dte1, iv2, dte2);

        // Assert
        sigmaE.Should().BeNull();
    }

    [Fact]
    public void STTM001AEstimator_WithZeroDTE_ReturnsNull()
    {
        // Arrange
        double iv1 = 0.35;
        int dte1 = 0;
        double iv2 = 0.25;
        int dte2 = 30;

        // Act
        double? sigmaE = STIV005A.STTM001AEstimator(iv1, dte1, iv2, dte2);

        // Assert
        sigmaE.Should().BeNull();
    }

    // BaseVolatilityEstimator Tests

    [Fact]
    public void BaseVolatilityEstimator_WithValidInputs_ReturnsBaseVolatility()
    {
        // Arrange
        double iv1 = 0.35;
        int dte1 = 7;
        double iv2 = 0.25;
        int dte2 = 30;

        // Act
        double? sigma = STIV005A.BaseVolatilityEstimator(iv1, dte1, iv2, dte2);

        // Assert
        sigma.Should().NotBeNull();
        sigma.Should().BeGreaterThan(0);
        sigma.Should().BeLessThan(1.0);
    }

    [Fact]
    public void BaseVolatilityEstimator_WithSameDTE_ReturnsNull()
    {
        // Arrange
        double iv1 = 0.35;
        int dte1 = 30;
        double iv2 = 0.25;
        int dte2 = 30;

        // Act
        double? sigma = STIV005A.BaseVolatilityEstimator(iv1, dte1, iv2, dte2);

        // Assert
        sigma.Should().BeNull();
    }

    [Fact]
    public void BaseVolatilityEstimator_WithZeroDTE_ReturnsNull()
    {
        // Arrange
        double iv1 = 0.35;
        int dte1 = 0;
        double iv2 = 0.25;
        int dte2 = 30;

        // Act
        double? sigma = STIV005A.BaseVolatilityEstimator(iv1, dte1, iv2, dte2);

        // Assert
        sigma.Should().BeNull();
    }

    // Helper Methods

    private static List<PriceBar> CreateSamplePriceData()
    {
        List<PriceBar> prices = new List<PriceBar>();
        DateTime baseDate = DateTime.Today.AddDays(-400);
        double basePrice = 100.0;

        for (int i = 0; i < 400; i++)
        {
            // Skip weekends
            DateTime date = baseDate.AddDays(i);
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            // Simulate price with random walk
            double dailyReturn = (i % 90 < 2) ? 0.05 * (i % 2 == 0 ? 1 : -1) : 0.01 * ((i % 3) - 1);
            double close = basePrice * (1 + dailyReturn);

            prices.Add(new PriceBar
            {
                Date = date,
                Open = basePrice,
                High = Math.Max(basePrice, close) * 1.005,
                Low = Math.Min(basePrice, close) * 0.995,
                Close = close,
                Volume = 1000000
            });

            basePrice = close;
        }

        return prices;
    }

    private static List<DateTime> CreateSampleEarningsDates()
    {
        List<DateTime> dates = new List<DateTime>();
        DateTime baseDate = DateTime.Today;

        // Create quarterly earnings dates going back 3 years
        for (int q = 1; q <= 12; q++)
        {
            DateTime earningsDate = baseDate.AddDays(-90 * q);
            // Ensure not weekend
            if (earningsDate.DayOfWeek == DayOfWeek.Saturday)
            {
                earningsDate = earningsDate.AddDays(-1);
            }
            else if (earningsDate.DayOfWeek == DayOfWeek.Sunday)
            {
                earningsDate = earningsDate.AddDays(-2);
            }
            dates.Add(earningsDate);
        }

        return dates;
    }
}
