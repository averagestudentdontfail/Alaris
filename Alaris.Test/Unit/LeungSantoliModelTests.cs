using System;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for LeungSantoliModel.
/// Tests mathematical correctness of the L&amp;S (2014) pre-EA implied volatility model.
///
/// Reference: "Accounting for Earnings Announcements in the Pricing of Equity Options"
/// Tim Leung &amp; Marco Santoli (2014)
/// </summary>
public class LeungSantoliModelTests
{
    // ========================================================================
    // ComputeTheoreticalIV Tests
    // ========================================================================

    [Fact]
    public void ComputeTheoreticalIV_WhenNoEarningsJump_ReturnsBaseVolatility()
    {
        // Arrange
        double baseVol = 0.20;
        double sigmaE = 0.0;
        double timeToExpiry = 30.0 / 252.0; // 30 days

        // Act
        double theoreticalIV = LeungSantoliModel.ComputeTheoreticalIV(baseVol, sigmaE, timeToExpiry);

        // Assert - should be approximately base volatility
        theoreticalIV.Should().BeApproximately(baseVol, 0.001,
            "with zero earnings jump, IV should equal base volatility");
    }

    [Fact]
    public void ComputeTheoreticalIV_WhenEarningsJumpPresent_IVIsHigher()
    {
        // Arrange
        double baseVol = 0.20;
        double sigmaE = 0.05; // 5% earnings jump volatility
        double timeToExpiry = 7.0 / 252.0; // 7 days

        // Act
        double theoreticalIV = LeungSantoliModel.ComputeTheoreticalIV(baseVol, sigmaE, timeToExpiry);

        // Assert - IV should be elevated due to earnings jump
        theoreticalIV.Should().BeGreaterThan(baseVol,
            "earnings jump should elevate implied volatility");
    }

    [Theory]
    [InlineData(30.0 / 252.0)] // 30 days
    [InlineData(14.0 / 252.0)] // 14 days
    [InlineData(7.0 / 252.0)]  // 7 days
    [InlineData(1.0 / 252.0)]  // 1 day
    public void ComputeTheoreticalIV_DecreasesWithTimeToExpiry(double timeToExpiry)
    {
        // Arrange
        double baseVol = 0.20;
        double sigmaE = 0.05;

        double timeToExpiry2 = timeToExpiry * 2; // Double the time

        // Act
        double iv1 = LeungSantoliModel.ComputeTheoreticalIV(baseVol, sigmaE, timeToExpiry);
        double iv2 = LeungSantoliModel.ComputeTheoreticalIV(baseVol, sigmaE, timeToExpiry2);

        // Assert - IV should decrease as time to expiry increases (term structure inverts)
        iv1.Should().BeGreaterThan(iv2,
            "IV should decrease as time to expiry increases (inverted term structure)");
    }

    [Fact]
    public void ComputeTheoreticalIV_MatchesLeungSantoliFormula()
    {
        // Arrange - use example from paper (Figure 3)
        double sigma = 0.1912;  // Base volatility
        double sigmaE = 0.0429; // Earnings jump volatility
        double timeToExpiry = 5.0 / 252.0; // 5 days to expiry

        // Manual calculation: I(t) = sqrt(sigma^2 + sigma_e^2/(T-t))
        double expectedIV = Math.Sqrt(
            (sigma * sigma) + (sigmaE * sigmaE / timeToExpiry));

        // Act
        double theoreticalIV = LeungSantoliModel.ComputeTheoreticalIV(sigma, sigmaE, timeToExpiry);

        // Assert
        theoreticalIV.Should().BeApproximately(expectedIV, 0.0001,
            "should match manual L&S formula calculation");
    }

    [Fact]
    public void ComputeTheoreticalIV_ThrowsOnNegativeBaseVolatility()
    {
        // Arrange & Act & Assert
        Action act = () => LeungSantoliModel.ComputeTheoreticalIV(-0.1, 0.05, 0.1);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("baseVolatility");
    }

    [Fact]
    public void ComputeTheoreticalIV_ThrowsOnNegativeEarningsJump()
    {
        // Arrange & Act & Assert
        Action act = () => LeungSantoliModel.ComputeTheoreticalIV(0.2, -0.05, 0.1);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("earningsJumpVolatility");
    }

    [Fact]
    public void ComputeTheoreticalIV_ThrowsOnZeroOrNegativeTimeToExpiry()
    {
        // Arrange & Act & Assert
        Action act1 = () => LeungSantoliModel.ComputeTheoreticalIV(0.2, 0.05, 0.0);
        act1.Should().Throw<ArgumentException>()
            .WithParameterName("timeToExpiry");

        Action act2 = () => LeungSantoliModel.ComputeTheoreticalIV(0.2, 0.05, -0.1);
        act2.Should().Throw<ArgumentException>()
            .WithParameterName("timeToExpiry");
    }

    // ========================================================================
    // ComputeMispricingSignal Tests
    // ========================================================================

    [Fact]
    public void ComputeMispricingSignal_PositiveWhenMarketIVHigher()
    {
        // Arrange
        double marketIV = 0.50;
        double baseVol = 0.20;
        double sigmaE = 0.05;
        double timeToExpiry = 7.0 / 252.0;

        // Act
        double mispricing = LeungSantoliModel.ComputeMispricingSignal(
            marketIV, baseVol, sigmaE, timeToExpiry);

        // Assert
        mispricing.Should().BePositive(
            "positive mispricing when market IV > theoretical IV");
    }

    [Fact]
    public void ComputeMispricingSignal_NegativeWhenMarketIVLower()
    {
        // Arrange
        double marketIV = 0.25;
        double baseVol = 0.20;
        double sigmaE = 0.08; // Higher earnings jump -> higher theoretical IV
        double timeToExpiry = 5.0 / 252.0;

        // Act
        double mispricing = LeungSantoliModel.ComputeMispricingSignal(
            marketIV, baseVol, sigmaE, timeToExpiry);

        double theoreticalIV = LeungSantoliModel.ComputeTheoreticalIV(baseVol, sigmaE, timeToExpiry);

        // Assert
        if (marketIV < theoreticalIV)
        {
            mispricing.Should().BeNegative(
                "negative mispricing when market IV < theoretical IV");
        }
    }

    // ========================================================================
    // ComputeExpectedIVCrush Tests
    // ========================================================================

    [Fact]
    public void ComputeExpectedIVCrush_IsPositiveWithEarningsJump()
    {
        // Arrange
        double baseVol = 0.20;
        double sigmaE = 0.05;
        double timeToExpiry = 7.0 / 252.0;

        // Act
        double crush = LeungSantoliModel.ComputeExpectedIVCrush(baseVol, sigmaE, timeToExpiry);

        // Assert
        crush.Should().BePositive("IV crush should be positive when earnings jump exists");
    }

    [Fact]
    public void ComputeExpectedIVCrush_IncreasesAsExpiryApproaches()
    {
        // Arrange
        double baseVol = 0.20;
        double sigmaE = 0.05;

        // Act
        double crush30d = LeungSantoliModel.ComputeExpectedIVCrush(baseVol, sigmaE, 30.0 / 252.0);
        double crush7d = LeungSantoliModel.ComputeExpectedIVCrush(baseVol, sigmaE, 7.0 / 252.0);
        double crush1d = LeungSantoliModel.ComputeExpectedIVCrush(baseVol, sigmaE, 1.0 / 252.0);

        // Assert - crush should increase as expiry approaches
        crush1d.Should().BeGreaterThan(crush7d);
        crush7d.Should().BeGreaterThan(crush30d);
    }

    // ========================================================================
    // ComputeIVCrushRatio Tests
    // ========================================================================

    [Fact]
    public void ComputeIVCrushRatio_IsBetweenZeroAndOne()
    {
        // Arrange
        double baseVol = 0.20;
        double sigmaE = 0.05;
        double timeToExpiry = 7.0 / 252.0;

        // Act
        double crushRatio = LeungSantoliModel.ComputeIVCrushRatio(baseVol, sigmaE, timeToExpiry);

        // Assert
        crushRatio.Should().BeInRange(0, 1, "IV crush ratio should be between 0 and 1");
    }

    // ========================================================================
    // ExtractEarningsJumpVolatility Tests
    // ========================================================================

    [Fact]
    public void ExtractEarningsJumpVolatility_RecoversOriginalSigmaE()
    {
        // Arrange
        double baseVol = 0.20;
        double originalSigmaE = 0.05;
        double timeToExpiry = 7.0 / 252.0;

        // Compute the theoretical IV using original sigma_e
        double marketIV = LeungSantoliModel.ComputeTheoreticalIV(baseVol, originalSigmaE, timeToExpiry);

        // Act - extract sigma_e from the computed IV
        double extractedSigmaE = LeungSantoliModel.ExtractEarningsJumpVolatility(
            marketIV, baseVol, timeToExpiry);

        // Assert
        extractedSigmaE.Should().BeApproximately(originalSigmaE, 0.0001,
            "extracted sigma_e should match original");
    }

    [Fact]
    public void ExtractEarningsJumpVolatility_ReturnsZeroWhenIVBelowBase()
    {
        // Arrange
        double marketIV = 0.15;
        double baseVol = 0.20; // Base is higher than market
        double timeToExpiry = 30.0 / 252.0;

        // Act
        double sigmaE = LeungSantoliModel.ExtractEarningsJumpVolatility(
            marketIV, baseVol, timeToExpiry);

        // Assert
        sigmaE.Should().Be(0, "sigma_e should be 0 when market IV < base volatility");
    }

    // ========================================================================
    // ComputeTermStructure Tests
    // ========================================================================

    [Fact]
    public void ComputeTermStructure_ProducesInvertedStructure()
    {
        // Arrange
        double baseVol = 0.20;
        double sigmaE = 0.05;
        int[] dtePoints = { 7, 14, 21, 30, 45, 60 };

        // Act
        var termStructure = LeungSantoliModel.ComputeTermStructure(baseVol, sigmaE, dtePoints);

        // Assert - should be inverted (shorter DTE = higher IV)
        termStructure.Should().HaveCount(dtePoints.Length);

        for (int i = 0; i < termStructure.Length - 1; i++)
        {
            termStructure[i].TheoreticalIV.Should().BeGreaterThan(
                termStructure[i + 1].TheoreticalIV,
                $"IV at DTE {termStructure[i].DTE} should be > IV at DTE {termStructure[i + 1].DTE}");
        }
    }

    [Fact]
    public void ComputeTermStructure_ConvergesToBaseVolatility()
    {
        // Arrange
        double baseVol = 0.20;
        double sigmaE = 0.05;
        int[] dtePoints = { 7, 30, 90, 180, 365 }; // Up to 1 year

        // Act
        var termStructure = LeungSantoliModel.ComputeTermStructure(baseVol, sigmaE, dtePoints);

        // Assert - longer maturities should converge toward base volatility
        var longestMaturity = termStructure[^1];
        longestMaturity.TheoreticalIV.Should().BeCloseTo(baseVol, 0.05,
            "long-dated IV should converge to base volatility");
    }
}
