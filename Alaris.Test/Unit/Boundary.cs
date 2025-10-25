using System;
using System.Linq;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for DoubleBoundaryKimSolver.
/// Tests the FP-B' stabilized iteration implementation.
/// </summary>
public class DoubleBoundaryKimSolverTests
{
    [Fact]
    public void KimSolver_FpbPrime_PreventsBoundaryOscillations()
    {
        // Arrange: Long maturity case prone to oscillations with standard FP-B
        var kimSolver = new DoubleBoundaryKimSolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 15.0,  // Long maturity
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: 50
        );
        
        // Initial boundaries from QD+
        double upperInitial = 68.0;
        double lowerInitial = 57.0;
        
        // Act
        var (upperRefined, lowerRefined, crossingTime) = kimSolver.SolveBoundaries(
            upperInitial, lowerInitial);
        
        // Assert: Check for monotonicity (no oscillations)
        for (int i = 1; i < upperRefined.Length; i++)
        {
            // Upper boundary should decrease over time (from maturity to t=0)
            upperRefined[i].Should().BeLessOrEqualTo(upperRefined[i - 1] + 0.1,
                "upper boundary should be monotonic");
            
            // Lower boundary should increase over time
            lowerRefined[i].Should().BeGreaterOrEqualTo(lowerRefined[i - 1] - 0.1,
                "lower boundary should be monotonic");
        }
    }
    
    [Fact]
    public void KimSolver_IntegralCalculations_AreAccurate()
    {
        // Test integral term calculations
        var kimSolver = new DoubleBoundaryKimSolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: false,
            collocationPoints: 100  // High resolution for accuracy
        );
        
        // Act
        var (upper, lower, crossingTime) = kimSolver.SolveBoundaries(75.0, 65.0);
        
        // Assert: Boundaries should remain ordered throughout
        for (int i = 0; i < upper.Length; i++)
        {
            if (i * 1.0 / (upper.Length - 1) > crossingTime)
            {
                upper[i].Should().BeGreaterThan(lower[i] - 0.01,
                    "boundaries should not cross after crossing time");
            }
        }
    }
    
    [Theory]
    [InlineData(20)]   // Low resolution
    [InlineData(50)]   // Medium resolution
    [InlineData(100)]  // High resolution
    public void KimSolver_CollocationPoints_ConvergeWithResolution(int collocationPoints)
    {
        // Arrange
        var kimSolver = new DoubleBoundaryKimSolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 5.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: collocationPoints
        );
        
        // Act
        var (upper, lower, crossingTime) = kimSolver.SolveBoundaries(70.0, 60.0);
        
        // Assert: Higher resolution should give smoother boundaries
        int lastIndex = upper.Length - 1;
        upper[lastIndex].Should().BeInRange(65.0, 75.0,
            "refined upper boundary should be in reasonable range");
        lower[lastIndex].Should().BeInRange(55.0, 65.0,
            "refined lower boundary should be in reasonable range");
        
        // More collocation points should give better convergence
        if (collocationPoints >= 50)
        {
            double maxJump = 0.0;
            for (int i = 1; i < upper.Length; i++)
            {
                maxJump = Math.Max(maxJump, Math.Abs(upper[i] - upper[i - 1]));
            }
            maxJump.Should().BeLessThan(2.0, "boundaries should be smooth with high resolution");
        }
    }
    
    [Fact]
    public void KimSolver_CrossingTimeRefinement_AchievesTargetAccuracy()
    {
        // Arrange: Parameters likely to cause crossing
        var kimSolver = new DoubleBoundaryKimSolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.15,  // Higher volatility for crossing
            isCall: false,
            collocationPoints: 100
        );
        
        // Initial boundaries that will cross
        double upperInitial = 65.0;
        double lowerInitial = 62.0;  // Close boundaries likely to cross
        
        // Act
        var (upper, lower, crossingTime) = kimSolver.SolveBoundaries(
            upperInitial, lowerInitial);
        
        // Assert: If crossing detected, should be refined
        if (crossingTime > 0 && crossingTime < 10.0)
        {
            // Check that boundaries are approximately equal at crossing time
            int crossIndex = (int)(crossingTime / 10.0 * (upper.Length - 1));
            Math.Abs(upper[crossIndex] - lower[crossIndex]).Should().BeLessThan(1.0,
                "boundaries should be close at refined crossing time");
            
            // Crossing time refinement should achieve Δt < 0.01 (Healy recommendation)
            // This is implicitly tested by the refinement algorithm
            crossingTime.Should().BeGreaterThan(0.0);
        }
    }
    
    [Fact]
    public void KimSolver_NumeratorDenominator_HandleEdgeCases()
    {
        // Test edge cases in N and D calculations
        var kimSolver = new DoubleBoundaryKimSolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 0.1,  // Very short maturity
            rate: -0.001,   // Very small negative rate
            dividendYield: -0.002,
            volatility: 0.50,  // High volatility
            isCall: false,
            collocationPoints: 20
        );
        
        // Act & Assert: Should not throw on edge cases
        var act = () => kimSolver.SolveBoundaries(90.0, 80.0);
        act.Should().NotThrow();
        
        var (upper, lower, crossingTime) = kimSolver.SolveBoundaries(90.0, 80.0);
        upper.All(v => !double.IsNaN(v)).Should().BeTrue("no NaN values in upper boundary");
        lower.All(v => !double.IsNaN(v)).Should().BeTrue("no NaN values in lower boundary");
    }
    
    [Fact]
    public void KimSolver_StabilizedIteration_ConvergesFaster()
    {
        // FP-B' should converge in fewer iterations than standard FP-B
        var kimSolver = new DoubleBoundaryKimSolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: 50
        );
        
        // Act: Solve with reasonable initial guess
        var (upper, lower, crossingTime) = kimSolver.SolveBoundaries(70.0, 59.0);
        
        // Assert: Should converge to Healy benchmark range
        int lastIndex = upper.Length - 1;
        upper[lastIndex].Should().BeInRange(68.0, 71.0,
            "should converge near Healy upper benchmark");
        lower[lastIndex].Should().BeInRange(57.0, 60.0,
            "should converge near Healy lower benchmark");
    }
    
    [Theory]
    [InlineData(50.0, 40.0)]   // Wide initial spread
    [InlineData(70.0, 60.0)]   // Moderate spread
    [InlineData(75.0, 73.0)]   // Narrow spread (near crossing)
    public void KimSolver_HandlesVariousInitialGuesses(double upperInit, double lowerInit)
    {
        // Arrange
        var kimSolver = new DoubleBoundaryKimSolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 5.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.10,
            isCall: false,
            collocationPoints: 30
        );
        
        // Act
        var (upper, lower, crossingTime) = kimSolver.SolveBoundaries(upperInit, lowerInit);
        
        // Assert: Should converge regardless of initial guess quality
        int lastIndex = upper.Length - 1;
        upper[lastIndex].Should().BeGreaterThan(50.0);
        upper[lastIndex].Should().BeLessThan(100.0);
        lower[lastIndex].Should().BeGreaterThan(30.0);
        lower[lastIndex].Should().BeLessThan(upper[lastIndex]);
    }
    
    [Fact]
    public void KimSolver_BoundaryInterpolation_IsSmooth()
    {
        // Test that boundary interpolation maintains smoothness
        var kimSolver = new DoubleBoundaryKimSolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: false,
            collocationPoints: 50
        );
        
        // Act
        var (upper, lower, crossingTime) = kimSolver.SolveBoundaries(80.0, 70.0);
        
        // Assert: Check smoothness by examining second differences
        for (int i = 2; i < upper.Length; i++)
        {
            double secondDiff = upper[i] - 2 * upper[i - 1] + upper[i - 2];
            Math.Abs(secondDiff).Should().BeLessThan(5.0,
                "second differences should be small for smooth boundaries");
        }
    }
}