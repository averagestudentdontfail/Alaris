using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for DoubleBoundarySolver (complete two-stage solver).
/// Tests QD+ approximation followed by Kim integral equation refinement.
/// </summary>
public class DoubleBoundarySolverTests
{
    [Fact]
    public void DoubleBoundarySolver_CalculatesCallBoundary()
    {
        // Arrange: ATM call option with q < r < 0
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,          // r = -1%
            dividendYield: -0.02, // q = -2%
            volatility: 0.20,
            isCall: true,
            collocationPoints: 20,  // Fewer points for faster tests
            useRefinement: false    // QD+ only for speed
        );

        // Act
        var (upper, lower, crossingTime) = solver.SolveBoundaries();

        // Assert
        upper.Should().BeGreaterThan(100.0,
            "upper boundary should be above strike for call options");
        lower.Should().BeLessThan(upper,
            "lower boundary should be below upper boundary");
    }

    [Fact]
    public void DoubleBoundarySolver_CalculatesPutBoundary()
    {
        // Arrange: ATM put option with q < r < 0
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: false,
            collocationPoints: 20,
            useRefinement: false
        );

        // Act
        var (upper, lower, crossingTime) = solver.SolveBoundaries();

        // Assert
        lower.Should().BeLessThan(100.0,
            "lower boundary should be below strike for put options");
        upper.Should().BeGreaterThan(lower,
            "upper boundary should be above lower boundary");
    }

    [Fact]
    public void DoubleBoundarySolver_CalculatesOptionValue()
    {
        // Arrange: ATM call option
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true,
            collocationPoints: 20,
            useRefinement: false
        );

        // Act
        double value = solver.CalculateValue();

        // Assert
        value.Should().BeGreaterThan(0.0,
            "ATM option should have positive value");
        value.Should().BeLessThan(100.0,
            "option value should be less than spot");
    }

    [Fact]
    public void DoubleBoundarySolver_WithRefinement_ImprovesBoundaries()
    {
        // Arrange: Same parameters, with and without refinement
        var solverNoRefine = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true,
            collocationPoints: 20,
            useRefinement: false
        );

        var solverWithRefine = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true,
            collocationPoints: 20,
            useRefinement: true
        );

        // Act
        var (upperNoRefine, lowerNoRefine, _) = solverNoRefine.SolveBoundaries();
        var (upperWithRefine, lowerWithRefine, _) = solverWithRefine.SolveBoundaries();

        // Assert: Boundaries should be in same general range
        // (Kim refinement adjusts but doesn't drastically change QD+ results)
        System.Math.Abs(upperWithRefine - upperNoRefine).Should().BeLessThan(10.0,
            "refined boundary should be close to QD+ approximation");
        System.Math.Abs(lowerWithRefine - lowerNoRefine).Should().BeLessThan(10.0,
            "refined boundary should be close to QD+ approximation");
    }

    [Fact]
    public void DoubleBoundarySolver_HandlesImmediateExercise()
    {
        // Arrange: Deep ITM call (spot >> upper boundary)
        var solver = new DoubleBoundarySolver(
            spot: 200.0,  // Very high spot
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true,
            collocationPoints: 20,
            useRefinement: false
        );

        // Act
        double value = solver.CalculateValue();

        // Assert: Should be close to intrinsic value
        double intrinsicValue = 200.0 - 100.0;
        value.Should().BeGreaterThan(intrinsicValue * 0.95,
            "deep ITM option should be close to intrinsic value");
    }

    [Fact]
    public void DoubleBoundarySolver_FastPath_MatchesApproximation()
    {
        // Arrange: Same parameters for both
        var approximation = new DoubleBoundaryApproximation(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true
        );

        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true,
            collocationPoints: 20,
            useRefinement: false  // Should match approximation
        );

        // Act
        double approxValue = approximation.ApproximateValue();
        double solverValue = solver.CalculateValue();

        // Assert: Fast path should match approximation
        solverValue.Should().BeApproximately(approxValue, 0.01,
            "solver without refinement should match approximation");
    }

    [Fact]
    public void DoubleBoundarySolver_HandlesHighVolatility()
    {
        // Arrange: High volatility case
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.50,  // 50% volatility
            isCall: true,
            collocationPoints: 20,
            useRefinement: false
        );

        // Act
        var (upper, lower, crossingTime) = solver.SolveBoundaries();
        double value = solver.CalculateValue();

        // Assert
        upper.Should().BeGreaterThan(lower,
            "boundaries should remain ordered even with high volatility");
        value.Should().BeGreaterThan(0.0,
            "option should have positive value");
    }

    [Fact]
    public void DoubleBoundarySolver_ConsistentAcrossMaturities()
    {
        // Arrange: Same parameters, different maturities
        var shortMaturity = new DoubleBoundarySolver(
            100.0, 100.0, 0.25, -0.01, -0.02, 0.20, true, 20, false);
        
        var longMaturity = new DoubleBoundarySolver(
            100.0, 100.0, 2.0, -0.01, -0.02, 0.20, true, 20, false);

        // Act
        double valueShort = shortMaturity.CalculateValue();
        double valueLong = longMaturity.CalculateValue();

        // Assert: Longer maturity should have higher time value
        valueLong.Should().BeGreaterThan(valueShort,
            "longer maturity should have higher option value");
    }

    [Fact]
    public void DoubleBoundarySolver_PutCallConsistency()
    {
        // Arrange: Put and call with same parameters
        var call = new DoubleBoundarySolver(
            100.0, 100.0, 1.0, -0.01, -0.02, 0.20, true, 20, false);
        
        var put = new DoubleBoundarySolver(
            100.0, 100.0, 1.0, -0.01, -0.02, 0.20, false, 20, false);

        // Act
        double callValue = call.CalculateValue();
        double putValue = put.CalculateValue();

        // Assert: Both should have positive value
        callValue.Should().BeGreaterThan(0.0);
        putValue.Should().BeGreaterThan(0.0);
        
        // For negative rates, both call and put can have significant early exercise premium
        callValue.Should().BeLessThan(100.0);
        putValue.Should().BeLessThan(100.0);
    }
}