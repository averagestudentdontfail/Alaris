using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STIV006A - Volatility Surface Interpolator.
/// Tests sticky-delta convention, skew calibration, and exposure evaluation.
/// </summary>
public sealed class STIV006ATests
{
    private readonly STIV006A _interpolator;

    public STIV006ATests()
    {
        _interpolator = new STIV006A();
    }

    [Fact]
    public void CalibrateFromChain_WithValidChain_ReturnsParameters()
    {
        // Arrange
        List<STIV008A> chain = CreateSampleChain(spotPrice: 100.0, atmIV: 0.30);

        // Act
        STIV007A result = _interpolator.CalibrateFromChain(
            chain, spotPrice: 100.0, timeToExpiry: 30.0 / 252.0, riskFreeRate: 0.05);

        // Assert
        result.AtmImpliedVolatility.Should().BeApproximately(0.30, 0.01);
        result.SkewCoefficient.Should().BeLessThan(0); // Equity has negative skew
        result.SpotPrice.Should().Be(100.0);
    }

    [Fact]
    public void CalibrateFromChain_WithInsufficientData_ThrowsArgumentException()
    {
        // Arrange: Only 2 quotes (need at least 3)
        List<STIV008A> chain = new List<STIV008A>
        {
            new STIV008A { Strike = 100, ImpliedVolatility = 0.30, IsCall = true },
            new STIV008A { Strike = 105, ImpliedVolatility = 0.28, IsCall = true }
        };

        // Act & Assert
        Action act = () => _interpolator.CalibrateFromChain(
            chain, spotPrice: 100.0, timeToExpiry: 30.0 / 252.0, riskFreeRate: 0.05);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InterpolateIV_AtATM_ReturnsAtmIV()
    {
        // Arrange
        STIV007A parameters = CreateSampleParameters(atmIV: 0.30, skew: -0.10, spot: 100.0);

        // Act
        double iv = _interpolator.InterpolateIV(parameters, strike: 100.0);

        // Assert: At ATM, should return ATM IV
        iv.Should().BeApproximately(0.30, 0.01);
    }

    [Fact]
    public void InterpolateIV_BelowATM_ReturnsHigherIV()
    {
        // Arrange: Negative skew means OTM puts (low strikes) have higher IV
        STIV007A parameters = CreateSampleParameters(atmIV: 0.30, skew: -0.10, spot: 100.0);

        // Act
        double atmIV = _interpolator.InterpolateIV(parameters, strike: 100.0);
        double otmPutIV = _interpolator.InterpolateIV(parameters, strike: 90.0);

        // Assert: OTM put should have higher IV due to negative skew
        otmPutIV.Should().BeGreaterThan(atmIV);
    }

    [Fact]
    public void InterpolateIV_AboveATM_ReturnsLowerIV()
    {
        // Arrange: Negative skew means OTM calls (high strikes) have lower IV
        STIV007A parameters = CreateSampleParameters(atmIV: 0.30, skew: -0.10, spot: 100.0);

        // Act
        double atmIV = _interpolator.InterpolateIV(parameters, strike: 100.0);
        double otmCallIV = _interpolator.InterpolateIV(parameters, strike: 110.0);

        // Assert: OTM call should have lower IV due to negative skew
        otmCallIV.Should().BeLessThan(atmIV);
    }

    [Fact]
    public void InterpolateIV_ClampsToReasonableRange()
    {
        // Arrange: Extreme skew that would produce unreasonable IV
        STIV007A parameters = new STIV007A
        {
            AtmImpliedVolatility = 0.30,
            SkewCoefficient = -0.50,
            SpotPrice = 100.0,
            TimeToExpiry = 30.0 / 252.0,
            RiskFreeRate = 0.05,
            CalibrationTime = DateTime.UtcNow
        };

        // Act
        double extremeIV = _interpolator.InterpolateIV(parameters, strike: 50.0);

        // Assert: Should be clamped to max 3.0 (300%)
        extremeIV.Should().BeLessThanOrEqualTo(3.0);
        extremeIV.Should().BeGreaterThanOrEqualTo(0.05);
    }

    [Fact]
    public void CalculateStickyDeltaIVChange_SpotUp_ReturnsPositiveChange()
    {
        // Arrange: Under sticky-delta with negative skew, when spot moves up,
        // the original strike becomes OTM put-like (lower strike relative to spot)
        // which has HIGHER IV due to negative skew.
        // Formula: -(-0.10) * (+0.05) / (0.30 * sqrt(T)) > 0
        STIV007A parameters = CreateSampleParameters(atmIV: 0.30, skew: -0.10, spot: 100.0);

        // Act: Spot moved from 100 to 105 (5% up)
        double ivChange = _interpolator.CalculateStickyDeltaIVChange(
            parameters, originalStrike: 100.0, originalSpot: 100.0, currentSpot: 105.0);

        // Assert: With negative skew, spot up → original strike is now OTM put → higher IV
        ivChange.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateStickyDeltaIVChange_SpotDown_ReturnsNegativeChange()
    {
        // Arrange: When spot moves down, the original strike becomes OTM call-like
        // (higher strike relative to spot) which has LOWER IV due to negative skew.
        // Formula: -(-0.10) * (-0.05) / (0.30 * sqrt(T)) < 0
        STIV007A parameters = CreateSampleParameters(atmIV: 0.30, skew: -0.10, spot: 100.0);

        // Act: Spot moved from 100 to 95 (5% down)
        double ivChange = _interpolator.CalculateStickyDeltaIVChange(
            parameters, originalStrike: 100.0, originalSpot: 100.0, currentSpot: 95.0);

        // Assert: With negative skew, spot down → original strike is now OTM call → lower IV
        ivChange.Should().BeLessThan(0);
    }

    [Fact]
    public void EvaluateSkewExposure_SmallShift_ReturnsLowRisk()
    {
        // Arrange
        STIV007A parameters = CreateSampleParameters(atmIV: 0.30, skew: -0.10, spot: 100.0);

        // Act: 1% moneyness shift
        STIV009A result = _interpolator.EvaluateSkewExposure(
            parameters, currentMoneyness: 1.01, entryMoneyness: 1.00);

        // Assert
        result.RiskLevel.Should().Be(STIV010A.Low);
        result.ShouldRecenter.Should().BeFalse();
    }

    [Fact]
    public void EvaluateSkewExposure_LargeShift_ReturnsHighRisk()
    {
        // Arrange
        STIV007A parameters = CreateSampleParameters(atmIV: 0.30, skew: -0.10, spot: 100.0);

        // Act: 12% moneyness shift
        STIV009A result = _interpolator.EvaluateSkewExposure(
            parameters, currentMoneyness: 1.12, entryMoneyness: 1.00);

        // Assert
        result.RiskLevel.Should().Be(STIV010A.High);
        result.ShouldRecenter.Should().BeTrue();
    }

    [Fact]
    public void EvaluateSkewExposure_ElevatedShift_RecommendRecenter()
    {
        // Arrange
        STIV007A parameters = CreateSampleParameters(atmIV: 0.30, skew: -0.10, spot: 100.0);

        // Act: 7% moneyness shift (elevated range 5-10%)
        STIV009A result = _interpolator.EvaluateSkewExposure(
            parameters, currentMoneyness: 1.07, entryMoneyness: 1.00);

        // Assert
        result.RiskLevel.Should().Be(STIV010A.Elevated);
        result.ShouldRecenter.Should().BeTrue();
        result.Rationale.Should().Contain("recentering");
    }

    #region Helper Methods

    private static List<STIV008A> CreateSampleChain(double spotPrice, double atmIV)
    {
        List<STIV008A> chain = new List<STIV008A>();

        // Create strikes from 85% to 115% of spot with typical equity skew
        double[] strikePcts = { 0.85, 0.90, 0.95, 1.00, 1.05, 1.10, 1.15 };
        
        foreach (double pct in strikePcts)
        {
            double strike = spotPrice * pct;
            // Equity skew: lower strikes have higher IV
            double skewAdjustment = -0.10 * (1.0 - pct);
            double iv = atmIV * (1.0 + skewAdjustment);

            chain.Add(new STIV008A
            {
                Strike = strike,
                ImpliedVolatility = iv,
                IsCall = pct >= 1.0
            });
        }

        return chain;
    }

    private static STIV007A CreateSampleParameters(double atmIV, double skew, double spot)
    {
        return new STIV007A
        {
            AtmImpliedVolatility = atmIV,
            SkewCoefficient = skew,
            SpotPrice = spot,
            TimeToExpiry = 30.0 / 252.0,
            RiskFreeRate = 0.05,
            CalibrationTime = DateTime.UtcNow
        };
    }

    #endregion
}
