// TSUN032A.cs - Unit Tests for CRVL001A (AlgorithmBounds Validation)
// Component ID: TSUN032A
//
// Coverage:
// - AlgorithmBounds.ValidateDoubleBoundaryInputs boundary conditions
// - BoundsViolationException generation for invalid inputs
// - TryValidateDoubleBoundaryInputs non-throwing validation
//
// Mathematical Invariants:
// - σ ∈ [0.001, 5.0] (volatility bounds)
// - τ ∈ [1/252, 30] years (time to expiry bounds)
// - S, K > 0 (positive prices)
// - |ln(K/S)| ≤ 3 (moneyness bounds)
// - r, q finite (rate validity)
//

using Xunit;
using FluentAssertions;
using Alaris.Core.Validation;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN032A: Unit tests for AlgorithmBounds validation (CRVL001A).
/// </summary>
public class TSUN032A
{

    /// <summary>
    /// Valid inputs should not throw.
    /// </summary>
    [Fact]
    public void ValidateDoubleBoundaryInputs_ValidInputs_DoesNotThrow()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Standard valid parameters
        // ═══════════════════════════════════════════════════════════
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 0.5;
        double rate = 0.05;
        double dividend = 0.02;
        double volatility = 0.20;

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Should not throw
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot, strike, maturity, rate, dividend, volatility);

        act.Should().NotThrow();
    }

    /// <summary>
    /// Negative volatility should throw BoundsViolationException.
    /// </summary>
    [Theory]
    [InlineData(0.0)]       // Zero
    [InlineData(-0.1)]      // Negative
    [InlineData(0.0005)]    // Below minimum (0.001)
    [InlineData(5.1)]       // Above maximum (5.0)
    [InlineData(double.NaN)]
    public void ValidateDoubleBoundaryInputs_InvalidVolatility_ThrowsBoundsViolation(double volatility)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Should throw for invalid volatility
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            100.0, 100.0, 0.5, 0.05, 0.02, volatility);

        act.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Zero or negative maturity should throw.
    /// </summary>
    [Theory]
    [InlineData(0.0)]                   // Zero
    [InlineData(-0.1)]                  // Negative
    [InlineData(0.001)]                 // Below 1/252 trading days
    [InlineData(31.0)]                  // Above 30 years
    [InlineData(double.NaN)]
    public void ValidateDoubleBoundaryInputs_InvalidMaturity_ThrowsBoundsViolation(double maturity)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Should throw for invalid maturity
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            100.0, 100.0, maturity, 0.05, 0.02, 0.20);

        act.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Non-positive spot price should throw.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void ValidateDoubleBoundaryInputs_InvalidSpot_ThrowsBoundsViolation(double spot)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Should throw for invalid spot
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot, 100.0, 0.5, 0.05, 0.02, 0.20);

        act.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Extreme moneyness (|ln(K/S)| > 3) should throw.
    /// </summary>
    [Theory]
    [InlineData(100.0, 0.05)]   // ln(0.05/100) = -7.6 > 3
    [InlineData(100.0, 2500.0)] // ln(2500/100) = 3.2 > 3
    public void ValidateDoubleBoundaryInputs_ExtremeMoneynessS_ThrowsBoundsViolation(double spot, double strike)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Extreme OTM/ITM should fail
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot, strike, 0.5, 0.05, 0.02, 0.20);

        act.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Negative rates should be allowed (double boundary regime).
    /// </summary>
    [Fact]
    public void ValidateDoubleBoundaryInputs_NegativeRates_DoesNotThrow()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Double boundary regime: q < r < 0
        // ═══════════════════════════════════════════════════════════
        double rate = -0.01;
        double dividend = -0.02;

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Negative rates are valid
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            100.0, 100.0, 0.5, rate, dividend, 0.20);

        act.Should().NotThrow();
    }

    /// <summary>
    /// NaN/Infinity rates should throw.
    /// </summary>
    [Theory]
    [InlineData(double.NaN, 0.02)]
    [InlineData(0.05, double.NaN)]
    [InlineData(double.PositiveInfinity, 0.02)]
    [InlineData(0.05, double.NegativeInfinity)]
    public void ValidateDoubleBoundaryInputs_InvalidRates_ThrowsBoundsViolation(double rate, double dividend)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: NaN/Infinity rates should fail
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            100.0, 100.0, 0.5, rate, dividend, 0.20);

        act.Should().Throw<BoundsViolationException>();
    }



    /// <summary>
    /// TryValidate returns true for valid inputs.
    /// </summary>
    [Fact]
    public void TryValidateDoubleBoundaryInputs_ValidInputs_ReturnsTrue()
    {
        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        bool result = AlgorithmBounds.TryValidateDoubleBoundaryInputs(
            100.0, 100.0, 0.5, 0.05, 0.02, 0.20, out string? error);

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        result.Should().BeTrue();
        error.Should().BeNull();
    }

    /// <summary>
    /// TryValidate returns false with error message for invalid inputs.
    /// </summary>
    [Fact]
    public void TryValidateDoubleBoundaryInputs_InvalidInputs_ReturnsFalseWithError()
    {
        // ═══════════════════════════════════════════════════════════
        // ACT: Invalid volatility
        // ═══════════════════════════════════════════════════════════
        bool result = AlgorithmBounds.TryValidateDoubleBoundaryInputs(
            100.0, 100.0, 0.5, 0.05, 0.02, -0.20, out string? error);

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        result.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("volatility");
    }



    /// <summary>
    /// Validates individual volatility bounds.
    /// </summary>
    [Theory]
    [InlineData(0.001, true)]   // Minimum valid
    [InlineData(0.20, true)]    // Typical
    [InlineData(5.0, true)]     // Maximum valid
    [InlineData(0.0009, false)] // Below minimum
    [InlineData(5.1, false)]    // Above maximum
    public void ValidateVolatility_BoundsCheck(double sigma, bool shouldPass)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateVolatility(sigma);

        if (shouldPass)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<BoundsViolationException>();
        }
    }

    /// <summary>
    /// Validates individual time to expiry bounds.
    /// </summary>
    [Theory]
    [InlineData(0.004, true)]   // ~1 trading day (1/252 = 0.00397)
    [InlineData(1.0, true)]     // 1 year
    [InlineData(30.0, true)]    // Maximum valid
    [InlineData(0.003, false)]  // Below 1 trading day
    [InlineData(31.0, false)]   // Above 30 years
    public void ValidateTimeToExpiry_BoundsCheck(double tau, bool shouldPass)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action act = () => AlgorithmBounds.ValidateTimeToExpiry(tau);

        if (shouldPass)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<BoundsViolationException>();
        }
    }

}
