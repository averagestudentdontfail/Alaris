// CRMF002ATests.cs - Tests for Characteristic Equation Solver
// Tests mathematical properties and Healy (2021) benchmarks

using Alaris.Core.Math;
using Xunit;

namespace Alaris.Test.Unit.Core.Math;

/// <summary>
/// Unit tests for CRMF002A characteristic equation solver.
/// Validates mathematical invariants and numerical stability.
/// </summary>
public class CRMF002ATests
{
    private const double Tolerance = 1e-10;

    #region Positive Rate Tests

    [Fact]
    public void SolveCharacteristic_PositiveRates_Lambda1GreaterThanOne()
    {
        // Arrange: Standard positive rate case
        double r = 0.05;
        double q = 0.02;
        double σ = 0.30;

        // Act
        var (λ1, λ2) = CRMF002A.SolveCharacteristic(r, q, σ);

        // Assert: Under positive rates, λ1 > 1 and λ2 < 0
        Assert.True(λ1 > 1.0, $"λ1 should be > 1 for positive rates, but was {λ1}");
        Assert.True(λ2 < 0.0, $"λ2 should be < 0 for positive rates, but was {λ2}");
    }

    [Theory]
    [InlineData(0.05, 0.02, 0.30)]
    [InlineData(0.10, 0.05, 0.25)]
    [InlineData(0.03, 0.00, 0.40)]
    public void SolveCharacteristic_PositiveRates_RootsSatisfyEquation(double r, double q, double σ)
    {
        // Act
        var (λ1, λ2) = CRMF002A.SolveCharacteristic(r, q, σ);

        // Assert: Both roots should satisfy the characteristic equation
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ1, Tolerance), $"λ1={λ1} does not satisfy equation");
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ2, Tolerance), $"λ2={λ2} does not satisfy equation");
    }

    #endregion

    #region Negative Rate Tests

    [Fact]
    public void SolveCharacteristic_NegativeRates_DoubleBoundaryRegime()
    {
        // Arrange: Healy (2021) double boundary case: q < r < 0
        double r = -0.004;
        double q = -0.006;
        double σ = 0.30;

        // Act
        var (λ1, λ2) = CRMF002A.SolveCharacteristic(r, q, σ);

        // Assert: Characteristic equation has two real roots
        // λ1 > λ2 by convention (as per SolveCharacteristic implementation)
        Assert.True(λ1 > λ2, $"λ1={λ1} should be > λ2={λ2}");
        
        // Note: For negative r, the product of roots is λ1*λ2 = -2r/σ² = 2|r|/σ² > 0
        // This means both roots have the same sign. Combined with sum being positive
        // (for typical σ), both roots can be positive in negative rate regimes.

        // Validate roots satisfy characteristic equation
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ1, Tolerance));
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ2, Tolerance));
    }

    [Theory]
    [InlineData(-0.004, -0.006, 0.30)]  // Healy regime: q < r < 0
    [InlineData(-0.01, -0.02, 0.40)]    // Deeper negative: use higher σ for real roots
    [InlineData(-0.002, -0.004, 0.25)]  // Mild negative: q < r < 0
    public void SolveCharacteristic_NegativeRates_RootsSatisfyEquation(double r, double q, double σ)
    {
        // For real roots, discriminant must be >= 0:
        // disc = b² - 4ac = (r-q-σ²/2)² + 2σ²r
        // With r < 0, need |r-q-σ²/2|² > 2σ²|r| which is satisfied for larger σ
        
        // Act
        var (λ1, λ2) = CRMF002A.SolveCharacteristic(r, q, σ);

        // Assert - use slightly relaxed tolerance for negative rate edge cases
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ1, 1e-8), $"λ1={λ1} does not satisfy equation");
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ2, 1e-8), $"λ2={λ2} does not satisfy equation");
    }

    #endregion

    #region Vieta's Formulas (Product and Sum of Roots)

    [Theory]
    [InlineData(0.05, 0.02, 0.30)]
    [InlineData(-0.004, -0.006, 0.30)]  // Use σ=0.30 to ensure real roots
    [InlineData(0.00, 0.02, 0.30)]
    public void SolveCharacteristic_VietasFormulas_SumOfRoots(double r, double q, double σ)
    {
        // Vieta's formula for quadratic aλ² + bλ + c = 0:
        // λ1 + λ2 = -b/a = -(r - q - σ²/2) / (σ²/2) = (q - r + σ²/2) / (σ²/2)
        double σ2 = σ * σ;
        double expectedSum = (q - r + σ2 / 2.0) / (σ2 / 2.0);

        // Act
        var (λ1, λ2) = CRMF002A.SolveCharacteristic(r, q, σ);
        double actualSum = λ1 + λ2;

        // Assert
        Assert.True(System.Math.Abs(actualSum - expectedSum) < 1e-8, 
            $"Sum of roots {actualSum} should equal {expectedSum}");
    }

    [Theory]
    [InlineData(0.05, 0.02, 0.30)]
    [InlineData(-0.004, -0.006, 0.30)]  // Use σ=0.30 to ensure real roots  
    [InlineData(0.10, 0.00, 0.40)]
    public void SolveCharacteristic_VietasFormulas_ProductOfRoots(double r, double q, double σ)
    {
        // Vieta's formula: λ1 * λ2 = c/a = -r / (σ²/2) = -2r/σ²
        double σ2 = σ * σ;
        double expectedProduct = -2.0 * r / σ2;

        // Act
        var (λ1, λ2) = CRMF002A.SolveCharacteristic(r, q, σ);
        double actualProduct = λ1 * λ2;

        // Assert
        Assert.True(System.Math.Abs(actualProduct - expectedProduct) < 1e-8,
            $"Product of roots {actualProduct} should equal {expectedProduct}");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SolveCharacteristic_ZeroRate_HandlesCor()
    {
        // Arrange: r = 0 edge case
        double r = 0.0;
        double q = 0.02;
        double σ = 0.30;

        // Act
        var (λ1, λ2) = CRMF002A.SolveCharacteristic(r, q, σ);

        // Assert: Should still produce valid roots
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ1, Tolerance));
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ2, Tolerance));
    }

    [Fact]
    public void SolveCharacteristic_NearZeroRate_NumericalStability()
    {
        // Arrange: Near-zero rate (stress test for Super-Halley)
        double r = 1e-10;
        double q = 0.02;
        double σ = 0.30;

        // Act
        var (λ1, λ2) = CRMF002A.SolveCharacteristic(r, q, σ);

        // Assert: Roots should still be valid
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ1, 1e-8));
        Assert.True(CRMF002A.ValidateRoot(r, q, σ, λ2, 1e-8));
    }

    [Fact]
    public void SolveCharacteristic_InvalidVolatility_Throws()
    {
        // Arrange: Zero volatility is invalid
        double r = 0.05;
        double q = 0.02;
        double σ = 0.0;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CRMF002A.SolveCharacteristic(r, q, σ));
    }

    [Fact]
    public void SolveCharacteristic_NegativeVolatility_Throws()
    {
        // Arrange
        double r = 0.05;
        double q = 0.02;
        double σ = -0.30;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CRMF002A.SolveCharacteristic(r, q, σ));
    }

    #endregion

    #region Super-Halley Refinement Tests

    [Fact]
    public void SuperHalleyRefine_ConvergesToRoot()
    {
        // Arrange: Known quadratic x² - 5x + 6 = 0, roots at x=2 and x=3
        double a = 1.0;
        double b = -5.0;
        double c = 6.0;
        double initialGuess = 2.9; // Closer to root at 3

        // Act
        double refined = CRMF002A.SuperHalleyRefine(initialGuess, a, b, c);

        // Assert: Should converge to root at 3
        double residual = a * refined * refined + b * refined + c;
        Assert.True(System.Math.Abs(residual) < 1e-10, $"Residual {residual} should be near zero");
        Assert.True(System.Math.Abs(refined - 3.0) < 1e-8, $"Should converge to root 3, got {refined}");
    }

    #endregion
}
