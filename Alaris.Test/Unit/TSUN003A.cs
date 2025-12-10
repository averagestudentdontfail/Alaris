// =============================================================================
// TSUN003A.cs - Unit Tests for DBEX001A (Near-Expiry Stability Handler)
// Component ID: TSUN003A
// =============================================================================
//
// Mathematical Foundation
// =======================
// Near-expiry stability handles the T→0 numerical singularity in option pricing.
//
// Key Equations:
// --------------
// 1. Blending function: w(τ) = (τ - τ_min) / (τ_blend - τ_min)
//    where τ_min = 1 day, τ_blend = 3 days
//
// 2. Blended price: V_blended = w(τ) * V_model + (1 - w(τ)) * V_intrinsic
//
// 3. Intrinsic values:
//    - Call: max(S - K, 0)
//    - Put:  max(K - S, 0)
//
// 4. Limiting Greeks as τ → 0:
//    - Delta → sign(S - K) for ITM, 0 for OTM, 0.5 for ATM
//    - Vega → 0 (no time value sensitivity)
//    - Gamma → ∞ at the strike (modeled as spike)
//
// Arbitrage Constraint:
// ---------------------
// V >= max(intrinsic, 0) always (no-arbitrage)
//
// =============================================================================

using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN003A: Unit tests for DBEX001A (Near-Expiry Stability Handler).
/// Tests smooth blending, intrinsic value, and limiting Greeks behavior
/// for numerical stability as τ → 0.
/// </summary>
public sealed class TSUN003A
{
    private readonly DBEX001A _handler;

    public TSUN003A()
    {
        _handler = new DBEX001A();
    }

    [Fact]
    public void IsNearExpiry_WhenFarFromExpiry_ReturnsFalse()
    {
        // Arrange: 30 days to expiry
        double timeToExpiry = 30.0 / 252.0;

        // Act
        bool result = _handler.IsNearExpiry(timeToExpiry);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsNearExpiry_WhenInBlendingZone_ReturnsTrue()
    {
        // Arrange: 2 days to expiry (in blending zone)
        double timeToExpiry = 2.0 / 252.0;

        // Act
        bool result = _handler.IsNearExpiry(timeToExpiry);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsIntrinsicOnly_WhenBelowThreshold_ReturnsTrue()
    {
        // Arrange: 0.5 days to expiry (below 1 day threshold)
        double timeToExpiry = 0.5 / 252.0;

        // Act
        bool result = _handler.IsIntrinsicOnly(timeToExpiry);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CalculateCallIntrinsic_WhenITM_ReturnsPositive()
    {
        // Arrange
        double spot = 105.0;
        double strike = 100.0;

        // Act
        double intrinsic = DBEX001A.CalculateCallIntrinsic(spot, strike);

        // Assert
        intrinsic.Should().BeApproximately(5.0, 0.001);
    }

    [Fact]
    public void CalculateCallIntrinsic_WhenOTM_ReturnsZero()
    {
        // Arrange
        double spot = 95.0;
        double strike = 100.0;

        // Act
        double intrinsic = DBEX001A.CalculateCallIntrinsic(spot, strike);

        // Assert
        intrinsic.Should().Be(0.0);
    }

    [Fact]
    public void CalculatePutIntrinsic_WhenITM_ReturnsPositive()
    {
        // Arrange
        double spot = 95.0;
        double strike = 100.0;

        // Act
        double intrinsic = DBEX001A.CalculatePutIntrinsic(spot, strike);

        // Assert
        intrinsic.Should().BeApproximately(5.0, 0.001);
    }

    [Fact]
    public void CalculateBlendingWeight_WhenBelowThreshold_ReturnsZero()
    {
        // Arrange: 0.5 days (below 1 day minimum)
        double timeToExpiry = 0.5 / 252.0;

        // Act
        double weight = _handler.CalculateBlendingWeight(timeToExpiry);

        // Assert
        weight.Should().Be(0.0);
    }

    [Fact]
    public void CalculateBlendingWeight_WhenAboveBlendZone_ReturnsOne()
    {
        // Arrange: 10 days (well above blending zone)
        double timeToExpiry = 10.0 / 252.0;

        // Act
        double weight = _handler.CalculateBlendingWeight(timeToExpiry);

        // Assert
        weight.Should().Be(1.0);
    }

    [Fact]
    public void CalculateBlendingWeight_WhenInMiddleOfBlend_ReturnsInterpolated()
    {
        // Arrange: 2 days (middle of 1-3 day blending zone)
        double timeToExpiry = 2.0 / 252.0;

        // Act
        double weight = _handler.CalculateBlendingWeight(timeToExpiry);

        // Assert: Should be approximately 0.5 (halfway through blend zone)
        weight.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void BlendWithIntrinsic_WhenFarFromExpiry_ReturnsModelValue()
    {
        // Arrange
        double modelValue = 5.50;
        double spot = 102.0;
        double strike = 100.0;
        double timeToExpiry = 30.0 / 252.0;

        // Act
        double blended = _handler.BlendWithIntrinsic(modelValue, spot, strike, isCall: true, timeToExpiry);

        // Assert: Should return model value
        blended.Should().BeApproximately(modelValue, 0.001);
    }

    [Fact]
    public void BlendWithIntrinsic_WhenAtExpiry_ReturnsIntrinsic()
    {
        // Arrange
        double modelValue = 5.50;
        double spot = 102.0;
        double strike = 100.0;
        double timeToExpiry = 0.5 / 252.0;

        // Act
        double blended = _handler.BlendWithIntrinsic(modelValue, spot, strike, isCall: true, timeToExpiry);

        // Assert: Should return intrinsic value (2.0)
        blended.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void BlendWithIntrinsic_NeverBelowIntrinsic()
    {
        // Arrange: Model value below intrinsic (shouldn't happen but we enforce no-arbitrage)
        double modelValue = 1.0;
        double spot = 105.0;
        double strike = 100.0;
        double timeToExpiry = 2.0 / 252.0;

        // Act
        double blended = _handler.BlendWithIntrinsic(modelValue, spot, strike, isCall: true, timeToExpiry);

        // Assert: Should be at least intrinsic (5.0)
        blended.Should().BeGreaterThanOrEqualTo(5.0);
    }

    [Fact]
    public void CalculateNearExpiryGreeks_CallITM_DeltaIsOne()
    {
        // Arrange
        double spot = 110.0;
        double strike = 100.0;
        double timeToExpiry = 0.5 / 252.0;

        // Act
        NearExpiryGreeks greeks = _handler.CalculateNearExpiryGreeks(spot, strike, isCall: true, timeToExpiry);

        // Assert
        greeks.Delta.Should().Be(1.0);
        greeks.Vega.Should().Be(0.0);
        greeks.IsNearExpiry.Should().BeTrue();
    }

    [Fact]
    public void CalculateNearExpiryGreeks_CallOTM_DeltaIsZero()
    {
        // Arrange
        double spot = 90.0;
        double strike = 100.0;
        double timeToExpiry = 0.5 / 252.0;

        // Act
        NearExpiryGreeks greeks = _handler.CalculateNearExpiryGreeks(spot, strike, isCall: true, timeToExpiry);

        // Assert
        greeks.Delta.Should().Be(0.0);
    }

    [Fact]
    public void CalculateNearExpiryGreeks_ATM_DeltaIsHalf()
    {
        // Arrange
        double spot = 100.0;
        double strike = 100.0;
        double timeToExpiry = 0.5 / 252.0;

        // Act
        NearExpiryGreeks greeks = _handler.CalculateNearExpiryGreeks(spot, strike, isCall: true, timeToExpiry);

        // Assert
        greeks.Delta.Should().Be(0.5);
        greeks.IsAtMoney.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenExpired_ReturnsInvalid()
    {
        // Act
        NearExpiryValidation result = _handler.Validate(-0.001);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Recommendation.Should().Be(NearExpiryRecommendation.UseIntrinsic);
    }

    [Fact]
    public void Validate_WhenNormalTime_ReturnsUseModel()
    {
        // Act
        NearExpiryValidation result = _handler.Validate(30.0 / 252.0);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Recommendation.Should().Be(NearExpiryRecommendation.UseModel);
    }

    [Fact]
    public void Validate_WhenInBlendZone_ReturnsUseBlended()
    {
        // Act
        NearExpiryValidation result = _handler.Validate(2.0 / 252.0);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Recommendation.Should().Be(NearExpiryRecommendation.UseBlended);
        result.BlendingWeight.Should().BeApproximately(0.5, 0.01);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidMinTime_ThrowsArgumentOutOfRangeException(double minTime)
    {
        // Act & Assert
        Action act = () => _ = new DBEX001A(minTime);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
