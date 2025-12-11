// =============================================================================
// TSUN023A.cs - Alaris.Double Component Unit Tests
// Component ID: TSUN023A
//
// Tests for Double Boundary Numerical Components:
// - DBEX001A: Near-expiry numerical stability handler
//
// Mathematical Invariants Tested:
// 1. Intrinsic Value: max(S-K, 0) for calls, max(K-S, 0) for puts
// 2. Blending Weight: w ∈ [0, 1] with continuity
// 3. Value Bound: BlendedValue ≥ IntrinsicValue (no arbitrage)
// 4. Greeks Limits: Δ→{0,±1}, Γ→0 (except ATM), V→0 as τ→0
//
// References:
//   - Hull (2018) "Options, Futures, and Other Derivatives", Ch. 19
//   - Black & Scholes (1973) limiting behaviour analysis
// =============================================================================

using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN023A: Unit tests for Alaris.Double near-expiry handler.
/// Tests numerical stability properties as time-to-expiry approaches zero.
/// </summary>
public sealed class TSUN023A
{
    #region Test Fixtures

    private static DBEX001A CreateDefaultHandler() => new DBEX001A();

    private static readonly double[] s_spots = { 80, 90, 95, 100, 105, 110, 120 };
    private const double Strike = 100.0;

    #endregion

    // ========================================================================
    // Intrinsic Value Tests
    // ========================================================================

    /// <summary>
    /// INVARIANT: Call intrinsic = max(S - K, 0).
    /// </summary>
    [Theory]
    [InlineData(120, 100, 20)]   // ITM call
    [InlineData(100, 100, 0)]    // ATM call
    [InlineData(80, 100, 0)]     // OTM call
    [InlineData(100.01, 100, 0.01)]  // Just ITM
    public void CalculateCallIntrinsic_MatchesFormula(double spot, double strike, double expected)
    {
        // Act
        double intrinsic = DBEX001A.CalculateCallIntrinsic(spot, strike);

        // Assert
        intrinsic.Should().BeApproximately(expected, 1e-10);
    }

    /// <summary>
    /// INVARIANT: Put intrinsic = max(K - S, 0).
    /// </summary>
    [Theory]
    [InlineData(80, 100, 20)]    // ITM put
    [InlineData(100, 100, 0)]    // ATM put
    [InlineData(120, 100, 0)]    // OTM put
    [InlineData(99.99, 100, 0.01)]  // Just ITM
    public void CalculatePutIntrinsic_MatchesFormula(double spot, double strike, double expected)
    {
        // Act
        double intrinsic = DBEX001A.CalculatePutIntrinsic(spot, strike);

        // Assert
        intrinsic.Should().BeApproximately(expected, 1e-10);
    }

    /// <summary>
    /// INVARIANT: Intrinsic value ≥ 0.
    /// </summary>
    [Fact]
    public void CalculateIntrinsic_AlwaysNonNegative()
    {
        foreach (double spot in s_spots)
        {
            double callIntrinsic = DBEX001A.CalculateCallIntrinsic(spot, Strike);
            double putIntrinsic = DBEX001A.CalculatePutIntrinsic(spot, Strike);

            callIntrinsic.Should().BeGreaterThanOrEqualTo(0);
            putIntrinsic.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    /// <summary>
    /// Generic intrinsic method should match specific methods.
    /// </summary>
    [Theory]
    [InlineData(110, 100, true)]
    [InlineData(90, 100, true)]
    [InlineData(110, 100, false)]
    [InlineData(90, 100, false)]
    public void CalculateIntrinsic_MatchesSpecificMethods(double spot, double strike, bool isCall)
    {
        // Arrange
        double expected = isCall
            ? DBEX001A.CalculateCallIntrinsic(spot, strike)
            : DBEX001A.CalculatePutIntrinsic(spot, strike);

        // Act
        double actual = DBEX001A.CalculateIntrinsic(spot, strike, isCall);

        // Assert
        actual.Should().BeApproximately(expected, 1e-10);
    }

    /// <summary>
    /// Invalid inputs should throw.
    /// </summary>
    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    public void CalculateIntrinsic_InvalidInputs_Throws(double spot, double strike)
    {
        // Act & Assert
        Action act = () => DBEX001A.CalculateCallIntrinsic(spot, strike);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ========================================================================
    // Blending Weight Tests
    // ========================================================================

    /// <summary>
    /// INVARIANT: Blending weight ∈ [0, 1].
    /// </summary>
    [Theory]
    [InlineData(0.0)]                       // At expiry
    [InlineData(0.5 / 252)]                 // Half day
    [InlineData(1.0 / 252)]                 // At threshold
    [InlineData(1.5 / 252)]                 // In blend zone
    [InlineData(3.0 / 252)]                 // At edge of blend zone
    [InlineData(5.0 / 252)]                 // Outside blend zone
    [InlineData(30.0 / 252)]                // Far from expiry
    public void CalculateBlendingWeight_AlwaysInUnitInterval(double tte)
    {
        // Arrange
        var handler = CreateDefaultHandler();

        // Act
        double weight = handler.CalculateBlendingWeight(tte);

        // Assert
        weight.Should().BeInRange(0, 1);
    }

    /// <summary>
    /// Below threshold: weight = 0 (pure intrinsic).
    /// </summary>
    [Fact]
    public void CalculateBlendingWeight_BelowThreshold_ReturnsZero()
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double tte = 0.5 / 252;  // Half a day

        // Act
        double weight = handler.CalculateBlendingWeight(tte);

        // Assert
        weight.Should().Be(0);
    }

    /// <summary>
    /// Above blend zone: weight = 1 (pure model).
    /// </summary>
    [Fact]
    public void CalculateBlendingWeight_AboveBlendZone_ReturnsOne()
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double tte = 10.0 / 252;  // 10 trading days

        // Act
        double weight = handler.CalculateBlendingWeight(tte);

        // Assert
        weight.Should().Be(1);
    }

    /// <summary>
    /// Weight should be monotonically increasing in time-to-expiry.
    /// </summary>
    [Fact]
    public void CalculateBlendingWeight_MonotonicallyIncreasing()
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double[] ttes = { 0.5 / 252, 1.0 / 252, 1.5 / 252, 2.0 / 252, 2.5 / 252, 3.0 / 252, 5.0 / 252 };

        // Act
        double previousWeight = -1;
        foreach (double tte in ttes)
        {
            double weight = handler.CalculateBlendingWeight(tte);
            weight.Should().BeGreaterThanOrEqualTo(previousWeight);
            previousWeight = weight;
        }
    }

    // ========================================================================
    // Near-Expiry Detection Tests
    // ========================================================================

    /// <summary>
    /// IsNearExpiry should detect near-expiry regime.
    /// </summary>
    [Theory]
    [InlineData(0.5 / 252, true)]    // Half day - near
    [InlineData(1.0 / 252, true)]    // 1 day - near
    [InlineData(2.0 / 252, true)]    // 2 days - in blend zone, still "near"
    [InlineData(3.0 / 252, true)]    // 3 days - edge of blend zone
    [InlineData(5.0 / 252, false)]   // 5 days - outside
    [InlineData(30.0 / 252, false)]  // 30 days - far
    public void IsNearExpiry_DetectsNearExpiryRegime(double tte, bool expectedNear)
    {
        // Arrange
        var handler = CreateDefaultHandler();

        // Act
        bool isNear = handler.IsNearExpiry(tte);

        // Assert
        isNear.Should().Be(expectedNear);
    }

    /// <summary>
    /// IsIntrinsicOnly should detect pure intrinsic regime.
    /// </summary>
    [Theory]
    [InlineData(0.5 / 252, true)]    // Half day - intrinsic only
    [InlineData(1.0 / 252, true)]    // 1 day - at threshold, still intrinsic
    [InlineData(1.5 / 252, false)]   // 1.5 days - in blend zone
    [InlineData(5.0 / 252, false)]   // 5 days - model pricing
    public void IsIntrinsicOnly_DetectsIntrinsicRegime(double tte, bool expectedIntrinsic)
    {
        // Arrange
        var handler = CreateDefaultHandler();

        // Act
        bool isIntrinsic = handler.IsIntrinsicOnly(tte);

        // Assert
        isIntrinsic.Should().Be(expectedIntrinsic);
    }

    // ========================================================================
    // BlendWithIntrinsic Tests
    // ========================================================================

    /// <summary>
    /// INVARIANT: Blended value ≥ intrinsic value (no arbitrage).
    /// </summary>
    [Fact]
    public void BlendWithIntrinsic_AlwaysAtLeastIntrinsic()
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double[] ttes = { 0.5 / 252, 1.5 / 252, 5.0 / 252, 30.0 / 252 };
        double modelValue = 5.0;

        foreach (double tte in ttes)
        {
            foreach (double spot in s_spots)
            {
                // Act
                double blendedCall = handler.BlendWithIntrinsic(modelValue, spot, Strike, true, tte);
                double blendedPut = handler.BlendWithIntrinsic(modelValue, spot, Strike, false, tte);

                double intrinsicCall = DBEX001A.CalculateCallIntrinsic(spot, Strike);
                double intrinsicPut = DBEX001A.CalculatePutIntrinsic(spot, Strike);

                // Assert
                blendedCall.Should().BeGreaterThanOrEqualTo(intrinsicCall,
                    $"Call at S={spot}, τ={tte:F4} should be >= intrinsic");
                blendedPut.Should().BeGreaterThanOrEqualTo(intrinsicPut,
                    $"Put at S={spot}, τ={tte:F4} should be >= intrinsic");
            }
        }
    }

    /// <summary>
    /// Near expiry: blended value approaches intrinsic.
    /// </summary>
    [Fact]
    public void BlendWithIntrinsic_NearExpiry_ApproachesIntrinsic()
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double spot = 110;
        double modelValue = 15.0;
        double tte = 0.5 / 252;  // Very near expiry

        double intrinsic = DBEX001A.CalculateCallIntrinsic(spot, Strike);

        // Act
        double blended = handler.BlendWithIntrinsic(modelValue, spot, Strike, true, tte);

        // Assert - Should be at intrinsic since weight = 0
        blended.Should().BeApproximately(intrinsic, 0.01);
    }

    /// <summary>
    /// Far from expiry: blended value equals model value.
    /// </summary>
    [Fact]
    public void BlendWithIntrinsic_FarFromExpiry_EqualsModelValue()
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double spot = 100;
        double modelValue = 5.0;
        double tte = 30.0 / 252;  // Far from expiry

        // Act
        double blended = handler.BlendWithIntrinsic(modelValue, spot, Strike, true, tte);

        // Assert
        blended.Should().BeApproximately(modelValue, 1e-10);
    }

    /// <summary>
    /// Negative model value should throw.
    /// </summary>
    [Fact]
    public void BlendWithIntrinsic_NegativeModelValue_Throws()
    {
        // Arrange
        var handler = CreateDefaultHandler();

        // Act & Assert
        Action act = () => handler.BlendWithIntrinsic(-1.0, 100, 100, true, 0.1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ========================================================================
    // Near-Expiry Greeks Tests
    // ========================================================================

    /// <summary>
    /// Delta approaches step function at expiry.
    /// </summary>
    [Theory]
    [InlineData(110, true, 1.0)]    // ITM call: Δ → 1
    [InlineData(90, true, 0.0)]     // OTM call: Δ → 0
    [InlineData(100, true, 0.5)]    // ATM call: Δ → 0.5
    [InlineData(90, false, -1.0)]   // ITM put: Δ → -1
    [InlineData(110, false, 0.0)]   // OTM put: Δ → 0
    [InlineData(100, false, -0.5)]  // ATM put: Δ → -0.5
    public void CalculateNearExpiryGreeks_DeltaApproachesLimit(double spot, bool isCall, double expectedDelta)
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double tte = 0.5 / 252;

        // Act
        var greeks = handler.CalculateNearExpiryGreeks(spot, Strike, isCall, tte);

        // Assert
        greeks.Delta.Should().BeApproximately(expectedDelta, 0.01);
    }

    /// <summary>
    /// Gamma approaches zero for OTM/ITM options near expiry.
    /// </summary>
    [Theory]
    [InlineData(80)]   // Deep OTM
    [InlineData(120)]  // Deep ITM
    public void CalculateNearExpiryGreeks_Gamma_ApproachesZeroAwayFromATM(double spot)
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double tte = 0.5 / 252;

        // Act
        var greeks = handler.CalculateNearExpiryGreeks(spot, Strike, true, tte);

        // Assert
        greeks.Gamma.Should().BeApproximately(0, 0.01);
    }

    /// <summary>
    /// Gamma is positive at ATM near expiry (though capped for stability).
    /// </summary>
    [Fact]
    public void CalculateNearExpiryGreeks_Gamma_PositiveAtATM()
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double spot = 100;  // ATM
        double tte = 1.0 / 252;

        // Act
        var greeks = handler.CalculateNearExpiryGreeks(spot, Strike, true, tte);

        // Assert
        greeks.Gamma.Should().BeGreaterThan(0);
        greeks.IsAtMoney.Should().BeTrue();
    }

    /// <summary>
    /// Vega approaches zero near expiry.
    /// </summary>
    [Fact]
    public void CalculateNearExpiryGreeks_Vega_ApproachesZero()
    {
        // Arrange
        var handler = CreateDefaultHandler();
        double tte = 0.5 / 252;

        foreach (double spot in s_spots)
        {
            // Act
            var greeks = handler.CalculateNearExpiryGreeks(spot, Strike, true, tte);

            // Assert
            greeks.Vega.Should().Be(0);
        }
    }

    // ========================================================================
    // Validation Tests
    // ========================================================================

    /// <summary>
    /// Validation should detect expired options.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ExpiredOrInvalid_ReturnsInvalid(double tte)
    {
        // Arrange
        var handler = CreateDefaultHandler();

        // Act
        var result = handler.Validate(tte);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Recommendation.Should().Be(NearExpiryRecommendation.UseIntrinsic);
    }

    /// <summary>
    /// Validation returns appropriate recommendation for each regime.
    /// </summary>
    [Theory]
    [InlineData(0.5 / 252, NearExpiryRecommendation.UseIntrinsic)]
    [InlineData(1.5 / 252, NearExpiryRecommendation.UseBlended)]
    [InlineData(10.0 / 252, NearExpiryRecommendation.UseModel)]
    public void Validate_ReturnsCorrectRecommendation(double tte, NearExpiryRecommendation expected)
    {
        // Arrange
        var handler = CreateDefaultHandler();

        // Act
        var result = handler.Validate(tte);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Recommendation.Should().Be(expected);
    }

    // ========================================================================
    // Constructor Validation Tests
    // ========================================================================

    /// <summary>
    /// Invalid thresholds should throw.
    /// </summary>
    [Theory]
    [InlineData(0, 0.01)]
    [InlineData(-0.01, 0.01)]
    [InlineData(0.01, 0)]
    [InlineData(0.01, -0.01)]
    public void Constructor_InvalidThresholds_Throws(double minTte, double blendWidth)
    {
        // Act & Assert
        Action act = () => _ = new DBEX001A(minTte, blendWidth);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Default configuration should use 1/252 and 2/252.
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesStandardThresholds()
    {
        // Arrange
        var handler = CreateDefaultHandler();

        // Assert - Validate behavior at known thresholds
        handler.IsIntrinsicOnly(1.0 / 252).Should().BeTrue();
        handler.IsIntrinsicOnly(1.01 / 252).Should().BeFalse();
        handler.IsNearExpiry(3.0 / 252).Should().BeTrue();
        handler.IsNearExpiry(3.1 / 252).Should().BeFalse();
    }

    /// <summary>
    /// Custom thresholds should be respected.
    /// </summary>
    [Fact]
    public void Constructor_CustomThresholds_Respected()
    {
        // Arrange
        double customMin = 2.0 / 252;
        double customBlend = 5.0 / 252;
        var handler = new DBEX001A(customMin, customBlend);

        // Assert
        handler.IsIntrinsicOnly(2.0 / 252).Should().BeTrue();
        handler.IsIntrinsicOnly(2.1 / 252).Should().BeFalse();
        handler.IsNearExpiry(7.0 / 252).Should().BeTrue();
        handler.IsNearExpiry(7.1 / 252).Should().BeFalse();
    }
}
