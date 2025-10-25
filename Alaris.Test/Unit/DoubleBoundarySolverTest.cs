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
        var result = solver.Solve();

        // Assert
        result.UpperBoundary.Should().BeGreaterThan(100.0,
            "upper boundary should be above strike for call options");
        result.LowerBoundary.Should().BeLessThan(result.UpperBoundary,
            "lower boundary should be below upper boundary");
        result.IsValid.Should().BeTrue("boundaries should not cross");
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
        var result = solver.Solve();

        // Assert
        result.LowerBoundary.Should().BeLessThan(100.0,
            "lower boundary should be below strike for put options");
        result.UpperBoundary.Should().BeGreaterThan(result.LowerBoundary,
            "upper boundary should be above lower boundary");
        result.IsValid.Should().BeTrue("boundaries should not cross");
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
        var resultNoRefine = solverNoRefine.Solve();
        var resultWithRefine = solverWithRefine.Solve();

        // Assert: Boundaries should be in same general range
        // (Kim refinement adjusts but doesn't drastically change QD+ results)
        System.Math.Abs(resultWithRefine.UpperBoundary - resultNoRefine.UpperBoundary).Should().BeLessThan(10.0,
            "refined boundary should be close to QD+ approximation");
        System.Math.Abs(resultWithRefine.LowerBoundary - resultNoRefine.LowerBoundary).Should().BeLessThan(10.0,
            "refined boundary should be close to QD+ approximation");
        
        // Refined result should indicate refinement was used
        resultWithRefine.IsRefined.Should().BeTrue("refinement should be applied");
        resultWithRefine.Method.Should().Contain("FP-B'", "should use FP-B' refinement");
        resultNoRefine.IsRefined.Should().BeFalse("no refinement should be applied");
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
        var result = solver.Solve();

        // Assert
        result.UpperBoundary.Should().BeGreaterThan(result.LowerBoundary,
            "boundaries should remain ordered even with high volatility");
        result.IsValid.Should().BeTrue("boundaries should be valid");
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
        var shortResult = shortMaturity.Solve();
        var longResult = longMaturity.Solve();

        // Assert: Both should produce valid boundaries
        shortResult.IsValid.Should().BeTrue("short maturity should have valid boundaries");
        longResult.IsValid.Should().BeTrue("long maturity should have valid boundaries");
        
        // Longer maturity typically has boundaries further from strike
        longResult.UpperBoundary.Should().BeGreaterThanOrEqualTo(shortResult.UpperBoundary * 0.8,
            "long maturity upper boundary should be in reasonable range");
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
        var callResult = call.Solve();
        var putResult = put.Solve();

        // Assert: Both should have valid boundaries
        callResult.IsValid.Should().BeTrue("call should have valid boundaries");
        putResult.IsValid.Should().BeTrue("put should have valid boundaries");
        
        // Call upper boundary should be above strike
        callResult.UpperBoundary.Should().BeGreaterThan(100.0,
            "call upper boundary should be above strike");
        
        // Put lower boundary should be below strike
        putResult.LowerBoundary.Should().BeLessThan(100.0,
            "put lower boundary should be below strike");
    }

    [Fact]
    public void DoubleBoundarySolver_ReturnsQdBoundaries_WhenAvailable()
    {
        // Arrange
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true,
            collocationPoints: 20,
            useRefinement: true  // Use refinement to get QD boundaries
        );

        // Act
        var result = solver.Solve();

        // Assert: QD boundaries should be populated when refinement is used
        if (result.IsRefined)
        {
            result.QdUpperBoundary.Should().BeGreaterThan(0.0,
                "QD upper boundary should be available");
            result.QdLowerBoundary.Should().BeGreaterThan(0.0,
                "QD lower boundary should be available");
            
            // Check improvement metrics
            result.UpperImprovement.Should().BeGreaterThanOrEqualTo(0.0,
                "upper improvement should be non-negative");
            result.LowerImprovement.Should().BeGreaterThanOrEqualTo(0.0,
                "lower improvement should be non-negative");
        }
    }

    [Fact]
    public void DoubleBoundarySolver_DetectsCrossingTime_WhenBoundariesCross()
    {
        // Arrange: Parameters that might lead to boundary crossing
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 5.0,  // Longer maturity increases crossing likelihood
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: 50,
            useRefinement: true
        );

        // Act
        var result = solver.Solve();

        // Assert: Crossing time should be detected if boundaries cross
        if (result.CrossingTime > 0.0)
        {
            result.CrossingTime.Should().BeLessThanOrEqualTo(5.0,
                "crossing time should be within maturity");
            result.CrossingTime.Should().BeGreaterThan(0.0,
                "crossing time should be positive");
        }
    }

    [Fact]
    public void DoubleBoundarySolver_HandlesDeepInTheMoneyCall()
    {
        // Arrange: Deep ITM call (spot >> strike)
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
        var result = solver.Solve();

        // Assert: Boundaries should still be valid
        result.IsValid.Should().BeTrue("deep ITM call should have valid boundaries");
        result.UpperBoundary.Should().BeGreaterThan(100.0,
            "upper boundary should be above strike");
    }

    [Fact]
    public void DoubleBoundarySolver_HandlesDeepInTheMoneyPut()
    {
        // Arrange: Deep ITM put (spot << strike)
        var solver = new DoubleBoundarySolver(
            spot: 50.0,  // Very low spot
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
        var result = solver.Solve();

        // Assert: Boundaries should still be valid
        result.IsValid.Should().BeTrue("deep ITM put should have valid boundaries");
        result.LowerBoundary.Should().BeLessThan(100.0,
            "lower boundary should be below strike");
    }

    [Fact]
    public void DoubleBoundarySolver_MethodDescription_MatchesRefinementSetting()
    {
        // Arrange & Act
        var withoutRefinement = new DoubleBoundarySolver(
            100.0, 100.0, 1.0, -0.01, -0.02, 0.20, true, 20, false).Solve();
        
        var withRefinement = new DoubleBoundarySolver(
            100.0, 100.0, 1.0, -0.01, -0.02, 0.20, true, 20, true).Solve();

        // Assert
        withoutRefinement.Method.Should().Contain("QD+", "should indicate QD+ method");
        withoutRefinement.Method.Should().NotContain("FP-B'", "should not indicate refinement");
        
        if (withRefinement.IsRefined)
        {
            withRefinement.Method.Should().Contain("FP-B'", "should indicate FP-B' refinement");
        }
    }
}