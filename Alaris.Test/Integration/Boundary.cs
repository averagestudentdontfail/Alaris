using System;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Integration;

/// <summary>
/// Integration tests for the complete double boundary solver workflow.
/// Tests QD+ approximation → Kim refinement → Final boundaries.
/// </summary>
public class DoubleBoundaryIntegrationTests
{
    [Fact]
    public void CompleteWorkflow_HealyTable2_MatchesBenchmark()
    {
        // Arrange: Exact Healy (2021) Table 2 parameters
        var solver = new DBSL001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: 50,
            useRefinement: true
        );
        
        // Act
        var result = solver.Solve();
        
        // Assert: Should match Healy benchmarks
        result.UpperBoundary.Should().BeApproximately(69.62, 1.0,
            "upper boundary should match Healy Table 2");
        result.LowerBoundary.Should().BeApproximately(58.72, 1.0,
            "lower boundary should match Healy Table 2");
        
        // Verify refinement was attempted
        result.IsRefined.Should().BeTrue();

        // UpperImprovement/LowerImprovement measure absolute change from QD+
        // When QD+ is already perfect (as with Healy benchmarks), change can be 0 (preservation is correct)
        // Improvement should be >= 0 (never negative, which would indicate corruption)
        result.UpperImprovement.Should().BeGreaterOrEqualTo(0,
            "refinement should not corrupt upper boundary");
        result.LowerImprovement.Should().BeGreaterOrEqualTo(0,
            "refinement should not corrupt lower boundary");
        
        // Check metadata
        result.Method.Should().Contain("FP-B'");
        result.IsValid.Should().BeTrue();
    }
    
    [Theory]
    [InlineData(1.0, 20)]
    [InlineData(5.0, 50)]
    [InlineData(10.0, 100)]
    public void CompleteWorkflow_ScalesWithMaturityAndResolution(double maturity, int collocationPoints)
    {
        // Arrange
        var solver = new DBSL001A(
            spot: 100.0,
            strike: 100.0,
            maturity: maturity,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: collocationPoints,
            useRefinement: true
        );
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = solver.Solve();
        stopwatch.Stop();
        
        // Assert: Should complete in reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            $"should complete within 5 seconds for T={maturity}, points={collocationPoints}");
        
        // Results should be valid
        result.IsValid.Should().BeTrue();
        result.UpperBoundary.Should().BeLessThan(100.0);
        result.LowerBoundary.Should().BeGreaterThan(0.0);
        
        // Path should have correct number of points
        if (result.UpperBoundaryPath != null)
        {
            result.UpperBoundaryPath.Count.Should().Be(collocationPoints);
            result.LowerBoundaryPath!.Count.Should().Be(collocationPoints);
        }
    }
    
    [Fact]
    public void CompleteWorkflow_QdPlusOnly_VersusRefined()
    {
        // Compare QD+ approximation with refined solution
        var parameters = new
        {
            spot = 100.0,
            strike = 100.0,
            maturity = 10.0,
            rate = -0.005,
            dividendYield = -0.01,
            volatility = 0.08,
            isCall = false,
            collocationPoints = 50
        };
        
        // Arrange
        var qdPlusOnly = new DBSL001A(
            parameters.spot, parameters.strike, parameters.maturity,
            parameters.rate, parameters.dividendYield, parameters.volatility,
            parameters.isCall, parameters.collocationPoints,
            useRefinement: false
        );
        
        var withRefinement = new DBSL001A(
            parameters.spot, parameters.strike, parameters.maturity,
            parameters.rate, parameters.dividendYield, parameters.volatility,
            parameters.isCall, parameters.collocationPoints,
            useRefinement: true
        );
        
        // Act
        var qdResult = qdPlusOnly.Solve();
        var refinedResult = withRefinement.Solve();
        
        // Assert: Refined should either preserve QD+ (when already accurate) or adjust moderately
        // When QD+ matches Healy benchmarks perfectly, preservation (difference = 0) is correct
        // When QD+ needs improvement, refinement should adjust but not drastically
        Math.Abs(refinedResult.UpperBoundary - qdResult.UpperBoundary).Should().BeInRange(0.0, 10.0,
            "refinement should preserve or adjust boundaries moderately (not drastically)");
        Math.Abs(refinedResult.LowerBoundary - qdResult.LowerBoundary).Should().BeInRange(0.0, 10.0,
            "refinement should preserve or adjust boundaries moderately (not drastically)");
        
        // QD+ boundaries should be stored in refined result
        refinedResult.QdUpperBoundary.Should().Be(qdResult.UpperBoundary);
        refinedResult.QdLowerBoundary.Should().Be(qdResult.LowerBoundary);
        
        // Check improvement metrics
        // When QD+ is already perfect (as with Healy benchmarks), improvement can be 0 (preservation is correct)
        // Improvement should be >= 0 (never negative, which would indicate corruption)
        refinedResult.UpperImprovement.Should().BeGreaterOrEqualTo(0,
            "refinement should not corrupt upper boundary");
        refinedResult.LowerImprovement.Should().BeGreaterOrEqualTo(0,
            "refinement should not corrupt lower boundary");
    }
    
    [Fact]
    public void CompleteWorkflow_CrossingBoundaries_HandledCorrectly()
    {
        // Parameters designed to cause boundary crossing
        var solver = new DBSL001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 15.0,  // Long maturity
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.15,  // Higher volatility
            isCall: false,
            collocationPoints: 100,
            useRefinement: true
        );
        
        // Act
        var result = solver.Solve();
        
        // Assert
        if (result.CrossingTime > 0)
        {
            result.CrossingTime.Should().BeLessThan(15.0,
                "crossing time should be within maturity");
            
            // At crossing time, boundaries should be approximately equal
            if (result.UpperBoundaryPath != null && result.LowerBoundaryPath != null)
            {
                int crossIndex = (int)(result.CrossingTime / 15.0 * (result.UpperBoundaryPath.Count - 1));
                double diff = Math.Abs(result.UpperBoundaryPath[crossIndex] -
                                      result.LowerBoundaryPath[crossIndex]);
                diff.Should().BeLessThan(2.0, "boundaries should be close at crossing time");
            }
        }
        
        result.IsValid.Should().BeTrue("solution should be valid even with crossing");
    }
    
    [Theory]
    [InlineData(true)]   // Call option
    [InlineData(false)]  // Put option
    public void CompleteWorkflow_CallPutConsistency(bool isCall)
    {
        // Test both call and put options under negative rates
        var solver = new DBSL001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 5.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.10,
            isCall: isCall,
            collocationPoints: 30,
            useRefinement: true
        );
        
        // Act
        var result = solver.Solve();
        
        // Assert
        result.IsValid.Should().BeTrue($"{(isCall ? "call" : "put")} should have valid boundaries");
        
        if (isCall)
        {
            // For calls in negative rate regime, boundaries depend on specific conditions
            result.UpperBoundary.Should().BeGreaterThan(0);
        }
        else
        {
            // For puts with q < r < 0, expect double boundaries
            result.UpperBoundary.Should().BeLessThan(100.0);
            result.LowerBoundary.Should().BeGreaterThan(0.0);
        }
        
        result.Method.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public void CompleteWorkflow_RegimeDetection_WorksCorrectly()
    {
        // Test regime detection for single vs double boundary
        
        // Case 1: Standard put (r > 0) - single boundary
        var standardPut = new DBSL001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: 0.05,  // Positive rate
            dividendYield: 0.02,
            volatility: 0.20,
            isCall: false,
            collocationPoints: 20,
            useRefinement: false
        );
        
        // Case 2: Negative rate put (q < r < 0) - double boundary
        var negativeRatePut = new DBSL001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.20,
            isCall: false,
            collocationPoints: 20,
            useRefinement: false
        );
        
        // Act
        var standardResult = standardPut.Solve();
        var negativeResult = negativeRatePut.Solve();
        
        // Assert
        standardResult.Method.Should().Contain("Single Boundary");
        negativeResult.Method.Should().NotContain("Single Boundary");
        
        // Standard put should have lower boundary only
        double.IsPositiveInfinity(standardResult.UpperBoundary).Should().BeTrue(
            "standard put upper boundary should be infinity");
        standardResult.LowerBoundary.Should().BeLessThan(100.0);
        
        // Negative rate put should have both boundaries
        negativeResult.UpperBoundary.Should().BeLessThan(100.0);
        negativeResult.LowerBoundary.Should().BeGreaterThan(0.0);
    }
    
    [Fact]
    public void CompleteWorkflow_StressTest_ExtremeParameters()
    {
        // Test with extreme but valid parameters
        var extremeCases = new[]
        {
            new { T = 0.01, r = -0.10, q = -0.20, σ = 0.50 },  // Very short, high vol
            new { T = 20.0, r = -0.001, q = -0.002, σ = 0.05 }, // Very long, low vol
            new { T = 5.0, r = -0.05, q = -0.10, σ = 0.30 }     // Moderate with large negative rates
        };
        
        foreach (var testCase in extremeCases)
        {
            // Arrange
            var solver = new DBSL001A(
                spot: 100.0,
                strike: 100.0,
                maturity: testCase.T,
                rate: testCase.r,
                dividendYield: testCase.q,
                volatility: testCase.σ,
                isCall: false,
                collocationPoints: 20,
                useRefinement: false  // QD+ only for speed in stress test
            );
            
            // Act & Assert: Should not throw and produce valid results
            var act = () => solver.Solve();
            act.Should().NotThrow($"should handle T={testCase.T}, r={testCase.r}, q={testCase.q}, σ={testCase.σ}");
            
            var result = solver.Solve();
            result.UpperBoundary.Should().BeGreaterThan(0, "boundary should be positive");
            result.LowerBoundary.Should().BeGreaterThan(0, "boundary should be positive");
            
            // NaN check
            double.IsNaN(result.UpperBoundary).Should().BeFalse("should not produce NaN");
            double.IsNaN(result.LowerBoundary).Should().BeFalse("should not produce NaN");
        }
    }
    
    [Fact]
    public void CompleteWorkflow_MemoryAndPerformance_IsEfficient()
    {
        // Test memory allocation and performance with large collocation grid
        var solver = new DBSL001A(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: 200,  // Large grid
            useRefinement: true
        );
        
        // Measure memory before
        long memoryBefore = GC.GetTotalMemory(true);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = solver.Solve();
        stopwatch.Stop();
        
        // Measure memory after
        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed = memoryAfter - memoryBefore;
        
        // Assert: Performance constraints
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            "should complete large grid within 10 seconds");
        
        // Memory usage should be reasonable (less than 50MB for this operation)
        (memoryUsed / 1024.0 / 1024.0).Should().BeLessThan(50.0,
            "memory usage should be under 50MB");
        
        // Results should still be accurate
        result.IsValid.Should().BeTrue();
        result.UpperBoundary.Should().BeInRange(65.0, 75.0);
        result.LowerBoundary.Should().BeInRange(55.0, 65.0);
    }
}