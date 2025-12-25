// =============================================================================
// TSDG003A.cs - Algorithm Bounds and Constraint Validation
// Component ID: TSDG003A
// =============================================================================
//
// Mathematical bounds validation tests for double boundary algorithms.
// Tests domain bounds, range bounds, monotonicity, and limiting behaviour.
//
// Coverage:
// - Volatility bounds: σ ∈ [0.001, 5.0]
// - Time bounds: τ ∈ [1/252, 30]
// - Rate regime: q < r < 0 (double boundary regime)
// - Spot/strike ratios and extreme moneyness
//
// =============================================================================

using Xunit;
using FluentAssertions;
using Alaris.Core.Validation;
using Alaris.Double;

namespace Alaris.Test.Diagnostic;

/// <summary>
/// TSDG003A: Algorithm bounds and constraint validation.
/// Tests domain bounds, range bounds, monotonicity, and limiting behaviour.
/// </summary>
public sealed class TSDG003A
{
    #region Volatility Bounds Tests

    /// <summary>
    /// Minimum volatility bound is accepted.
    /// </summary>
    [Fact]
    public void VolatilityBounds_MinimumValue_Accepted()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Minimum volatility (0.1%)
        // ═══════════════════════════════════════════════════════════
        double volatility = AlgorithmBounds.MinVolatility;

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: volatility);

        validate.Should().NotThrow();
    }

    /// <summary>
    /// Maximum volatility bound is accepted.
    /// </summary>
    [Fact]
    public void VolatilityBounds_MaximumValue_Accepted()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Maximum volatility (500%)
        // ═══════════════════════════════════════════════════════════
        double volatility = AlgorithmBounds.MaxVolatility;

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: volatility);

        validate.Should().NotThrow();
    }

    /// <summary>
    /// Volatility below minimum is rejected.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.0001)]
    [InlineData(-0.1)]
    public void VolatilityBounds_BelowMinimum_Rejected(double volatility)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: volatility);

        validate.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Volatility above maximum is rejected.
    /// </summary>
    [Theory]
    [InlineData(5.1)]
    [InlineData(10.0)]
    [InlineData(100.0)]
    public void VolatilityBounds_AboveMaximum_Rejected(double volatility)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: volatility);

        validate.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Typical volatility values are accepted.
    /// </summary>
    [Theory]
    [InlineData(0.10)]  // 10% low vol stock
    [InlineData(0.20)]  // 20% typical
    [InlineData(0.30)]  // 30% moderate
    [InlineData(0.50)]  // 50% high vol
    [InlineData(1.00)]  // 100% very high
    [InlineData(2.00)]  // 200% extreme
    public void VolatilityBounds_TypicalValues_Accepted(double volatility)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: volatility);

        validate.Should().NotThrow();
    }

    #endregion

    #region Time to Expiry Bounds Tests

    /// <summary>
    /// Minimum time to expiry is accepted.
    /// </summary>
    [Fact]
    public void TimeBounds_MinimumValue_Accepted()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: ~1 trading day
        // ═══════════════════════════════════════════════════════════
        double maturity = AlgorithmBounds.MinTimeToExpiry;

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: maturity,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25);

        validate.Should().NotThrow();
    }

    /// <summary>
    /// Maximum time to expiry is accepted.
    /// </summary>
    [Fact]
    public void TimeBounds_MaximumValue_Accepted()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: 30 years
        // ═══════════════════════════════════════════════════════════
        double maturity = AlgorithmBounds.MaxTimeToExpiry;

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: maturity,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25);

        validate.Should().NotThrow();
    }

    /// <summary>
    /// Time below minimum is rejected.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.001)]
    [InlineData(-1.0)]
    public void TimeBounds_BelowMinimum_Rejected(double maturity)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: maturity,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25);

        validate.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Time above maximum is rejected.
    /// </summary>
    [Theory]
    [InlineData(31.0)]
    [InlineData(50.0)]
    [InlineData(100.0)]
    public void TimeBounds_AboveMaximum_Rejected(double maturity)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: maturity,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25);

        validate.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Typical expiry times are accepted.
    /// </summary>
    [Theory]
    [InlineData(7.0 / 252)]    // 1 week
    [InlineData(30.0 / 252)]   // 1 month
    [InlineData(90.0 / 252)]   // 3 months
    [InlineData(0.5)]          // 6 months
    [InlineData(1.0)]          // 1 year
    [InlineData(2.0)]          // 2 years
    [InlineData(5.0)]          // 5 years
    public void TimeBounds_TypicalValues_Accepted(double maturity)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: maturity,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25);

        validate.Should().NotThrow();
    }

    #endregion

    #region Rate Regime Tests

    /// <summary>
    /// Double boundary regime: q < r < 0 is valid.
    /// </summary>
    [Theory]
    [InlineData(-0.01, -0.02)]  // r=-1%, q=-2%
    [InlineData(-0.005, -0.01)] // r=-0.5%, q=-1%
    [InlineData(-0.02, -0.03)]  // r=-2%, q=-3%
    public void RateRegime_DoubleBoundary_Accepted(double rate, double dividend)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: q < r < 0 is double boundary regime
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: 0.5,
            rate: rate, dividendYield: dividend, volatility: 0.25);

        validate.Should().NotThrow();
        (dividend < rate && rate < 0).Should().BeTrue("Double boundary regime requires q < r < 0");
    }

    #endregion

    #region Spot/Strike Bounds Tests

    /// <summary>
    /// Valid spot prices are accepted.
    /// </summary>
    [Theory]
    [InlineData(50.0, 50.0)]
    [InlineData(100.0, 100.0)]
    [InlineData(200.0, 200.0)]
    [InlineData(500.0, 500.0)]
    [InlineData(1000.0, 1000.0)]
    public void SpotBounds_ValidValues_Accepted(double spot, double strike)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: spot, strike: strike, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25);

        validate.Should().NotThrow();
    }

    /// <summary>
    /// Invalid spot prices are rejected.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-100.0)]
    public void SpotBounds_InvalidValues_Rejected(double spot)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: spot, strike: 100, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25);

        validate.Should().Throw<BoundsViolationException>();
    }

    /// <summary>
    /// Invalid strike prices are rejected.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-100.0)]
    public void StrikeBounds_InvalidValues_Rejected(double strike)
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot: 100, strike: strike, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25);

        validate.Should().Throw<BoundsViolationException>();
    }

    #endregion

    #region TryValidate Tests

    /// <summary>
    /// TryValidate returns false for invalid inputs without throwing.
    /// </summary>
    [Fact]
    public void TryValidate_InvalidInputs_ReturnsFalse()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Invalid volatility
        // ═══════════════════════════════════════════════════════════
        double invalidVol = -1.0;

        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        bool isValid = AlgorithmBounds.TryValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: invalidVol,
            out string? validationError);

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        isValid.Should().BeFalse();
        validationError.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// TryValidate returns true for valid inputs.
    /// </summary>
    [Fact]
    public void TryValidate_ValidInputs_ReturnsTrue()
    {
        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        bool isValid = AlgorithmBounds.TryValidateDoubleBoundaryInputs(
            spot: 100, strike: 100, maturity: 0.5,
            rate: -0.01, dividendYield: -0.02, volatility: 0.25,
            out string? validationError);

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        isValid.Should().BeTrue();
        validationError.Should().BeNull();
    }

    #endregion
}
