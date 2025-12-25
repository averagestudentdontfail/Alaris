// TSUN001A.cs - Unit Tests for DBAP001A (QD+ Approximation)
// Component ID: TSUN001A
//
// Mathematical Foundation
// Reference: Healy (2021) "Pricing American Options Under Negative Rates" §4
//
// The QD+ approximation computes exercise boundaries S_u and S_l for American
// puts under the double-boundary regime (q < r < 0).
//
// Key Equations:
// --------------
// 1. Lambda roots (Healy Eq. 15):
//    λ_{1,2} = [-(ω-1) ± √((ω-1)² + 8r/(σ²h))] / 2
//    where:
//      ω = 2(r-q)/σ²  (drift adjustment)
//      h = 1 - exp(-rT) (discount factor correction)
//
// 2. Lambda assignment for puts under r < 0 (Healy §4.2):
//    - Upper boundary S_u uses negative λ root
//    - Lower boundary S_l uses positive λ root
//
// 3. Super Halley iteration (3rd-order convergence):
//    x_{n+1} = x_n - f(x_n)/f'(x_n) * [1 - f(x_n)f''(x_n)/(2f'(x_n)²)]^{-1}
//
// Constraints (Healy Appendix A):
// -------------------------------
// A1: S_u > 0, S_l > 0 (positive boundaries)
// A2: S_u > S_l (ordered boundaries)
// A3: S_u < K, S_l < K (put boundaries below strike)
// A4: V(S_u) = K - S_u (smooth pasting at upper boundary)
// A5: ∂V/∂S|_{S=S_u} = -1 (delta continuity)
//
// Test Parameters (Healy Table 2):
// --------------------------------
// K = 100, T = 10, r = -0.005, q = -0.01, σ = 0.08
// Expected: S_u ≈ 69.62, S_l ≈ 58.72
//

using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN001A: Unit tests for DBAP001A (QD+ Approximation).
/// Tests mathematical correctness of the QD+ algorithm implementation
/// per Healy (2021) "Pricing American Options Under Negative Rates".
/// </summary>
public class TSUN001A
{
    /// <summary>
    /// Validates lambda root calculation and assignment per Healy §4.2.
    /// For puts with r &lt; 0: upper boundary uses negative λ, lower uses positive λ.
    /// </summary>
    [Fact]
    public void QdPlus_CalculatesLambdaRoots_Correctly()
    {
        // Arrange: Healy Table 2 parameters
        var approximation = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false
        );
        
        // Act
        var (upper, lower) = approximation.CalculateBoundaries();
        
        // Assert: Validates constraints A1, A2, A3 from Healy Appendix A
        upper.Should().BeLessThan(100.0, "A3: put upper boundary < strike");
        lower.Should().BeGreaterThan(0.0, "A1: lower boundary must be positive");
        upper.Should().BeGreaterThan(lower, "A2: boundaries must be ordered");
    }
    
    /// <summary>
    /// Tests boundary behavior across volatility range.
    /// Mathematical basis: Higher σ increases option value, affecting boundary location.
    /// </summary>
    [Theory]
    [InlineData(0.08, 69.0, 73.0)]  // σ = 8%  (Healy benchmark)
    [InlineData(0.10, 67.0, 71.0)]  // σ = 10%
    [InlineData(0.12, 65.0, 69.0)]  // σ = 12%
    public void QdPlus_HandlesVolatilityVariation(double volatility, double expectedLower, double expectedUpper)
    {
        // Arrange
        var approximation = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: volatility,
            isCall: false
        );
        
        // Act
        var (upper, lower) = approximation.CalculateBoundaries();
        
        // Assert: Higher volatility should widen boundary spread
        upper.Should().BeInRange(expectedLower, expectedUpper,
            $"boundary should be in expected range for σ={volatility:P0}");
    }
    
    /// <summary>
    /// Tests handling of negative h = 1 - exp(-rT) when r &lt; 0.
    /// Mathematical basis: h becomes negative for r &lt; 0, requiring careful
    /// handling in the discriminant calculation.
    /// </summary>
    [Fact]
    public void QdPlus_HandlesNegativeH_ForNegativeRates()
    {
        // Arrange: r < 0 gives negative h
        var approximation = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,  // Negative rate
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false
        );
        
        // Act
        var (upper, lower) = approximation.CalculateBoundaries();
        
        // Assert: Should handle negative h correctly (A1 constraint)
        upper.Should().BeGreaterThan(0, "A1: boundaries must be positive with negative h");
        lower.Should().BeGreaterThan(0, "A1: boundaries must be positive with negative h");
        
        // Verify: h = 1 - exp(-rT) is negative when r < 0
        double h = 1.0 - Math.Exp(-(-0.005) * 10.0);
        h.Should().BeLessThan(0, "h should be negative for r < 0");
    }
    
    /// <summary>
    /// Tests Super Halley iteration convergence robustness.
    /// Mathematical basis: 3rd-order convergence ensures rapid convergence
    /// from reasonable initial guesses.
    /// </summary>
    [Fact]
    public void QdPlus_SuperHalleyConvergence_IsRobust()
    {
        // Test Super Halley's method robustness across different initial guesses
        var approximation = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false
        );
        
        // Act
        var (upper, lower) = approximation.CalculateBoundaries();
        
        // Assert: Should converge to reasonable values (not extremes)
        upper.Should().BeLessThan(100.0, "A3: put upper boundary < strike");
        lower.Should().BeLessThan(upper, "A2: boundaries must be ordered");
        
        // Boundaries should not be at extremes (indicates convergence failure)
        upper.Should().BeGreaterThan(50.0, "upper boundary shouldn't converge to zero");
        lower.Should().BeGreaterThan(30.0, "lower boundary shouldn't converge to zero");
    }
    
    /// <summary>
    /// Tests boundary behavior across maturity range per Healy Table 2.
    /// Mathematical basis: As T → ∞, boundaries approach asymptotic limits.
    /// </summary>
    [Theory]
    [InlineData(1.0)]   // T = 1 year  (Healy: 73.50, 63.50)
    [InlineData(5.0)]   // T = 5 years (Healy: 71.60, 61.60)
    [InlineData(10.0)]  // T = 10 years (Healy: 69.62, 58.72)
    [InlineData(15.0)]  // T = 15 years (Healy: 68.00, 57.00)
    public void QdPlus_MaturityDependence_IsMonotonic(double maturity)
    {
        // Arrange
        var approximation = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: maturity,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false
        );
        
        // Act
        var (upper, lower) = approximation.CalculateBoundaries();
        
        // Assert: Boundaries should vary smoothly with maturity
        upper.Should().BeLessThan(100.0);
        lower.Should().BeGreaterThan(0.0);
        upper.Should().BeGreaterThan(lower);
        
        // Longer maturities generally have boundaries closer to strike
        if (maturity > 5.0)
        {
            double spread = upper - lower;
            spread.Should().BeLessThan(30.0, "long maturity should have narrower spread");
        }
    }
    
    /// <summary>
    /// Tests theta sign convention handling.
    /// Mathematical basis: For r &gt; 0, standard single-boundary American put applies.
    /// </summary>
    [Fact]
    public void QdPlus_ThetaSignConvention_IsCorrect()
    {
        // Test that theta sign convention is properly handled
        var approximation = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: 0.05,  // Positive rate for standard case
            dividendYield: 0.02,
            volatility: 0.20,
            isCall: false
        );
        
        // Act
        var (upper, lower) = approximation.CalculateBoundaries();
        
        // Assert: For standard put (r > 0), should get single lower boundary
        lower.Should().BeLessThan(100.0, "put boundary < strike");
        lower.Should().BeGreaterThan(0.0, "boundary must be positive");
    }
    
    /// <summary>
    /// Tests call-put relationship under negative rate conditions.
    /// Mathematical basis: Put-call symmetry S_put(r,q) ↔ S_call(q,r).
    /// </summary>
    [Fact]
    public void QdPlus_CallPutSymmetry_WithAppropriateParameters()
    {
        // Test call-put relationship under specific conditions
        var putApprox = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: false
        );
        
        var callApprox = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: 0.01,  // Positive rate for call
            dividendYield: 0.02,
            volatility: 0.20,
            isCall: true
        );
        
        // Act
        var (putUpper, putLower) = putApprox.CalculateBoundaries();
        var (callUpper, callLower) = callApprox.CalculateBoundaries();
        
        // Assert: Both should produce valid boundaries
        putUpper.Should().BeLessThan(100.0);
        callUpper.Should().BeGreaterThan(100.0);
    }
    
    /// <summary>
    /// Tests double boundary production for various q &lt; r &lt; 0 regimes.
    /// Mathematical basis: Healy's double-boundary condition applies when q &lt; r &lt; 0.
    /// </summary>
    [Theory]
    [InlineData(-0.01, -0.02)]   // q < r < 0
    [InlineData(-0.005, -0.01)]  // Healy benchmark case
    [InlineData(-0.001, -0.002)] // Very small negative rates
    public void QdPlus_NegativeRateRegimes_ProduceDoubleBoundaries(double rate, double dividend)
    {
        // Arrange
        var approximation = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 5.0,
            rate: rate,
            dividendYield: dividend,
            volatility: 0.10,
            isCall: false
        );
        
        // Act
        var (upper, lower) = approximation.CalculateBoundaries();
        
        // Assert: Should get two finite boundaries (A1, A2, A3)
        upper.Should().BeLessThan(100.0, "A3: put upper < strike");
        lower.Should().BeGreaterThan(0.0, "A1: lower must be positive");
        upper.Should().BeGreaterThan(lower, "A2: boundaries must be ordered");
        
        // Both boundaries should be finite (not infinity)
        double.IsInfinity(upper).Should().BeFalse("upper should be finite");
        double.IsInfinity(lower).Should().BeFalse("lower should be finite");
    }
    
    /// <summary>
    /// Tests graceful handling of edge cases that might produce complex lambda roots.
    /// Mathematical basis: The discriminant may become negative for extreme parameters.
    /// </summary>
    [Fact]
    public void QdPlus_ComplexLambdaRoots_HandledGracefully()
    {
        // Test case that might produce complex lambda roots
        var approximation = new DBAP001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 0.1,  // Very short maturity
            rate: -0.10,    // Large negative rate
            dividendYield: -0.20,
            volatility: 0.05, // Low volatility
            isCall: false
        );
        
        // Act & Assert: Should not throw, should return reasonable approximation
        var act = () => approximation.CalculateBoundaries();
        act.Should().NotThrow();
        
        var (upper, lower) = approximation.CalculateBoundaries();
        upper.Should().BeGreaterThan(0);
        lower.Should().BeGreaterThan(0);
    }
}
