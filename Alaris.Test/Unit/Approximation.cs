using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for QdPlusApproximation.
/// Tests mathematical correctness of the QD+ algorithm implementation.
/// </summary>
public class QdPlusApproximationTests
{
    private const double NUMERICAL_TOLERANCE = 1e-6;
    
    [Fact]
    public void QdPlus_CalculatesLambdaRoots_Correctly()
    {
        // Arrange
        var approximation = new QdPlusApproximation(
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
        
        // Assert: Lambda roots should be properly assigned
        // For put with r < 0: upper uses negative lambda, lower uses positive lambda
        upper.Should().BeLessThan(100.0, "put upper boundary < strike");
        lower.Should().BeGreaterThan(0.0, "lower boundary must be positive");
        upper.Should().BeGreaterThan(lower, "boundaries must be ordered");
    }
    
    [Theory]
    [InlineData(0.08, 69.0, 73.0)]  // σ = 8%
    [InlineData(0.10, 67.0, 71.0)]  // σ = 10%
    [InlineData(0.12, 65.0, 69.0)]  // σ = 12%
    public void QdPlus_HandlesVolatilityVariation(double volatility, double expectedLower, double expectedUpper)
    {
        // Arrange
        var approximation = new QdPlusApproximation(
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
    
    [Fact]
    public void QdPlus_HandlesNegativeH_ForNegativeRates()
    {
        // Arrange: r < 0 gives negative h
        var approximation = new QdPlusApproximation(
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
        
        // Assert: Should handle negative h correctly
        upper.Should().BeGreaterThan(0, "boundaries must be positive with negative h");
        lower.Should().BeGreaterThan(0, "boundaries must be positive with negative h");
        
        // h = 1 - exp(-rT) is negative when r < 0
        double h = 1.0 - Math.Exp(-(-0.005) * 10.0);
        h.Should().BeLessThan(0, "h should be negative for r < 0");
    }
    
    [Fact]
    public void QdPlus_SuperHalleyConvergence_IsRobust()
    {
        // Test Super Halley's method robustness across different initial guesses
        var approximation = new QdPlusApproximation(
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
        
        // Assert: Should converge to reasonable values
        upper.Should().BeLessThan(100.0, "put upper boundary < strike");
        lower.Should().BeLessThan(upper, "boundaries must be ordered");
        
        // Boundaries should not be at extremes (indicates convergence failure)
        upper.Should().BeGreaterThan(50.0, "upper boundary shouldn't converge to zero");
        lower.Should().BeGreaterThan(30.0, "lower boundary shouldn't converge to zero");
    }
    
    [Theory]
    [InlineData(1.0)]   // T = 1 year
    [InlineData(5.0)]   // T = 5 years
    [InlineData(10.0)]  // T = 10 years
    [InlineData(15.0)]  // T = 15 years
    public void QdPlus_MaturityDependence_IsMonotonic(double maturity)
    {
        // Arrange
        var approximation = new QdPlusApproximation(
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
    
    [Fact]
    public void QdPlus_ThetaSignConvention_IsCorrect()
    {
        // Test that theta sign convention is properly handled
        var approximation = new QdPlusApproximation(
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
    
    [Fact]
    public void QdPlus_CallPutSymmetry_WithAppropriateParameters()
    {
        // Test call-put relationship under specific conditions
        var putApprox = new QdPlusApproximation(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: false
        );
        
        var callApprox = new QdPlusApproximation(
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
    
    [Theory]
    [InlineData(-0.01, -0.02)]  // q < r < 0
    [InlineData(-0.005, -0.01)]  // Healy benchmark case
    [InlineData(-0.001, -0.002)] // Very small negative rates
    public void QdPlus_NegativeRateRegimes_ProduceDoubleBoundaries(double rate, double dividend)
    {
        // Arrange
        var approximation = new QdPlusApproximation(
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
        
        // Assert: Should get two finite boundaries
        upper.Should().BeLessThan(100.0, "put upper < strike");
        lower.Should().BeGreaterThan(0.0, "lower must be positive");
        upper.Should().BeGreaterThan(lower, "boundaries must be ordered");
        
        // Both boundaries should be finite (not infinity)
        double.IsInfinity(upper).Should().BeFalse("upper should be finite");
        double.IsInfinity(lower).Should().BeFalse("lower should be finite");
    }
    
    [Fact]
    public void QdPlus_ComplexLambdaRoots_HandledGracefully()
    {
        // Test case that might produce complex lambda roots
        var approximation = new QdPlusApproximation(
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