// CRRE001ATests.cs - Tests for Rate Regime Classifier
// Tests regime classification logic based on Healy (2021)

using Alaris.Core.Pricing;
using Xunit;

namespace Alaris.Test.Unit.Core.Pricing;

/// <summary>
/// Unit tests for CRRE001A rate regime classifier.
/// Validates correct regime detection for all three Healy (2021) cases.
/// </summary>
public class CRRE001ATests
{
    #region Standard Regime Tests

    [Theory]
    [InlineData(0.05, 0.02, false)]   // Put, r > 0, r > q: Standard
    [InlineData(0.05, 0.02, true)]    // Call, r > 0, r > q: Standard
    [InlineData(0.00, 0.00, false)]   // Zero rates: Standard
    [InlineData(0.10, 0.05, true)]    // Large positive rates: Standard
    public void Classify_PositiveRates_ReturnsStandard(double r, double q, bool isCall)
    {
        // Act
        RateRegime regime = CRRE001A.Classify(r, q, isCall);

        // Assert
        Assert.Equal(RateRegime.Standard, regime);
    }

    [Theory]
    [InlineData(-0.02, -0.01, false)]  // Put: r < q < 0 (r=-0.02 < q=-0.01), Standard
    [InlineData(-0.02, -0.01, true)]   // Call: both negative, r < q, call Standard
    public void Classify_NegativeRates_WellBehaved_ReturnsStandard(double r, double q, bool isCall)
    {
        // Arrange: Cases where r < q < 0 (put) OR call with negative r but no double boundary condition
        
        // Act
        RateRegime regime = CRRE001A.Classify(r, q, isCall);

        // Assert
        Assert.Equal(RateRegime.Standard, regime);
    }

    #endregion

    #region Double Boundary Regime Tests

    [Theory]
    [InlineData(-0.005, -0.010, false)]  // Put: q < r < 0 → Double boundary
    [InlineData(-0.01, -0.015, false)]   // Put: q < r < 0 → Double boundary
    [InlineData(-0.002, -0.008, false)]  // Put: q < r < 0 → Double boundary
    public void Classify_NegativeRates_DoubleBoundary_Put(double r, double q, bool isCall)
    {
        // Arrange: q < r < 0 for puts triggers double boundary
        Assert.True(r < 0, "r should be negative");
        Assert.True(q < r, "q should be less than r");

        // Act
        RateRegime regime = CRRE001A.Classify(r, q, isCall);

        // Assert
        Assert.Equal(RateRegime.DoubleBoundary, regime);
    }

    [Theory]
    [InlineData(0.03, 0.05, true)]   // Call: 0 < r < q → Double boundary
    [InlineData(0.02, 0.08, true)]   // Call: 0 < r < q → Double boundary
    [InlineData(0.01, 0.03, true)]   // Call: 0 < r < q → Double boundary
    public void Classify_PositiveRates_DoubleBoundary_Call(double r, double q, bool isCall)
    {
        // Arrange: 0 < r < q for calls triggers double boundary
        Assert.True(r > 0, "r should be positive");
        Assert.True(r < q, "r should be less than q");

        // Act
        RateRegime regime = CRRE001A.Classify(r, q, isCall);

        // Assert
        Assert.Equal(RateRegime.DoubleBoundary, regime);
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void RequiresDoubleBoundary_DoubleBoundaryCase_ReturnsTrue()
    {
        // Arrange: Put with q < r < 0
        double r = -0.005;
        double q = -0.010;
        bool isCall = false;

        // Act
        bool requiresDouble = CRRE001A.RequiresDoubleBoundary(r, q, isCall);

        // Assert
        Assert.True(requiresDouble);
    }

    [Fact]
    public void RequiresDoubleBoundary_StandardCase_ReturnsFalse()
    {
        // Arrange: Standard positive rate case
        double r = 0.05;
        double q = 0.02;
        bool isCall = true;

        // Act
        bool requiresDouble = CRRE001A.RequiresDoubleBoundary(r, q, isCall);

        // Assert
        Assert.False(requiresDouble);
    }

    [Fact]
    public void ValidateParameters_ValidRates_ReturnsTrue()
    {
        Assert.True(CRRE001A.ValidateParameters(0.05, 0.02));
        Assert.True(CRRE001A.ValidateParameters(-0.005, -0.010));
        Assert.True(CRRE001A.ValidateParameters(0.0, 0.0));
    }

    [Fact]
    public void ValidateParameters_NaN_ReturnsFalse()
    {
        Assert.False(CRRE001A.ValidateParameters(double.NaN, 0.02));
        Assert.False(CRRE001A.ValidateParameters(0.05, double.NaN));
    }

    [Fact]
    public void ValidateParameters_Infinity_ReturnsFalse()
    {
        Assert.False(CRRE001A.ValidateParameters(double.PositiveInfinity, 0.02));
        Assert.False(CRRE001A.ValidateParameters(0.05, double.NegativeInfinity));
    }

    [Fact]
    public void ValidateParameters_ExtremeRates_ReturnsFalse()
    {
        // Rates > 50% should be rejected as unreasonable
        Assert.False(CRRE001A.ValidateParameters(0.60, 0.02));
        Assert.False(CRRE001A.ValidateParameters(0.05, -0.60));
    }

    #endregion

    #region Regime Description Tests

    [Fact]
    public void GetDescription_Standard_ReturnsCorrectString()
    {
        string desc = CRRE001A.GetDescription(RateRegime.Standard);
        Assert.Contains("single-boundary", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDescription_DoubleBoundary_ReturnsCorrectString()
    {
        string desc = CRRE001A.GetDescription(RateRegime.DoubleBoundary);
        Assert.Contains("double-boundary", desc, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
