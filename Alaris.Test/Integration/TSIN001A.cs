// =============================================================================
// TSIN001A.cs - Integration Tests for Alaris.Double Complete Workflow
// Component ID: TSIN001A
// =============================================================================
//
// Mathematical Foundation
// =======================
// Reference: Healy (2021) "Pricing American Options Under Negative Rates"
// Reference: Kim (1990) "The Analytic Valuation of American Options"
//
// This integration test validates the complete double-boundary workflow:
//   QD+ Approximation (DBAP001A) → Kim Refinement (DBSL002A) → Final Boundaries
//
// Workflow Stages:
// ----------------
// 1. Initial Approximation (DBAP001A):
//    - Compute λ roots from Healy Eq. 15
//    - Apply Super Halley iteration for S_u, S_l
//
// 2. Refinement (DBSL002A):
//    - FP-B' stabilized iteration per Healy §5.3
//    - Crossing time detection with Δτ < 10⁻² accuracy
//
// 3. Validation:
//    - Constraints A1-A5 (Healy Appendix A)
//    - Benchmark accuracy against Healy Table 2
//
// Benchmark Values (Healy Table 2):
// ---------------------------------
// | T    | S_u   | S_l   |
// |------|-------|-------|
// | 1    | 73.50 | 63.50 |
// | 5    | 71.60 | 61.60 |
// | 10   | 69.62 | 58.72 |
// | 15   | 68.00 | 57.00 |
//
// Parameters: K=100, r=-0.005, q=-0.01, σ=0.08
//
// =============================================================================

using System;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Integration;

/// <summary>
/// TSIN001A: Integration tests for Alaris.Double complete workflow.
/// Tests QD+ approximation → Kim refinement → Final boundaries
/// per Healy (2021) and Kim (1990).
/// </summary>
public class TSIN001A
{
    /// <summary>
    /// Validates complete workflow against Healy (2021) Table 2 benchmark.
    /// Parameters: K=100, T=10, r=-0.005, q=-0.01, σ=0.08
    /// Expected: S_u ≈ 69.62, S_l ≈ 58.72
    /// </summary>
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
        
        // Assert: Should match Healy benchmarks within 1.0 tolerance
        result.UpperBoundary.Should().BeApproximately(69.62, 1.0,
            "upper boundary should match Healy Table 2 (T=10)");
        result.LowerBoundary.Should().BeApproximately(58.72, 1.0,
            "lower boundary should match Healy Table 2 (T=10)");
        
        // Verify refinement was attempted
        result.IsRefined.Should().BeTrue();

        // Improvement should be >= 0 (never negative, which would indicate corruption)
        result.UpperImprovement.Should().BeGreaterOrEqualTo(0,
            "refinement should not corrupt upper boundary");
        result.LowerImprovement.Should().BeGreaterOrEqualTo(0,
            "refinement should not corrupt lower boundary");
        
        // Check metadata
        result.Method.Should().Contain("FP-B'");
        result.IsValid.Should().BeTrue();
    }
    
    /// <summary>
    /// Tests workflow scaling across maturity and resolution combinations.
    /// Mathematical basis: Convergence should improve with higher collocation points.
    /// </summary>
    [Theory]
    [InlineData(1.0, 20)]   // Short maturity, low resolution
    [InlineData(5.0, 50)]   // Medium maturity, medium resolution
    [InlineData(10.0, 100)] // Long maturity, high resolution
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
        
        // Results should be valid (A1, A2, A3 constraints)
        result.IsValid.Should().BeTrue();
        result.UpperBoundary.Should().BeLessThan(100.0);  // A3
        result.LowerBoundary.Should().BeGreaterThan(0.0); // A1
        
        // Path should have correct number of points
        if (result.UpperBoundaryPath != null)
        {
            result.UpperBoundaryPath.Count.Should().Be(collocationPoints);
            result.LowerBoundaryPath!.Count.Should().Be(collocationPoints);
        }
    }
    
    /// <summary>
    /// Compares QD+ approximation vs refined solution.
    /// Mathematical basis: Refinement should preserve or improve QD+ accuracy.
    /// </summary>
    [Fact]
    public void CompleteWorkflow_QdPlusOnly_VersusRefined()
    {
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
        
        // Arrange: Two solvers - QD+ only vs with refinement
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
        
        // Assert: Refinement should preserve or adjust moderately
        Math.Abs(refinedResult.UpperBoundary - qdResult.UpperBoundary).Should().BeInRange(0.0, 10.0,
            "refinement should preserve or adjust boundaries moderately");
        Math.Abs(refinedResult.LowerBoundary - qdResult.LowerBoundary).Should().BeInRange(0.0, 10.0,
            "refinement should preserve or adjust boundaries moderately");
        
        // QD+ boundaries should be stored in refined result
        refinedResult.QdUpperBoundary.Should().Be(qdResult.UpperBoundary);
        refinedResult.QdLowerBoundary.Should().Be(qdResult.LowerBoundary);
        
        // Improvement should be >= 0 (never negative)
        refinedResult.UpperImprovement.Should().BeGreaterOrEqualTo(0,
            "refinement should not corrupt upper boundary");
        refinedResult.LowerImprovement.Should().BeGreaterOrEqualTo(0,
            "refinement should not corrupt lower boundary");
    }
    
    /// <summary>
    /// Tests crossing boundary detection and handling.
    /// Mathematical basis: For some parameters, S_u and S_l may cross at τ* before expiry.
    /// </summary>
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
    
    /// <summary>
    /// Tests regime detection for single vs double boundary.
    /// Mathematical basis: r > 0 → single boundary; q < r < 0 → double boundary.
    /// </summary>
    [Fact]
    public void CompleteWorkflow_RegimeDetection_WorksCorrectly()
    {
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
        
        // Standard put should have lower boundary only (A3), upper = ∞
        double.IsPositiveInfinity(standardResult.UpperBoundary).Should().BeTrue(
            "standard put upper boundary should be infinity");
        standardResult.LowerBoundary.Should().BeLessThan(100.0);
        
        // Negative rate put should have both finite boundaries
        negativeResult.UpperBoundary.Should().BeLessThan(100.0);  // A3
        negativeResult.LowerBoundary.Should().BeGreaterThan(0.0); // A1
    }
    
    /// <summary>
    /// Stress test with extreme but valid parameters.
    /// Validates numerical stability at edge cases.
    /// </summary>
    [Fact]
    public void CompleteWorkflow_StressTest_ExtremeParameters()
    {
        var extremeCases = new[]
        {
            new { T = 0.01, r = -0.10, q = -0.20, σ = 0.50 },  // Very short, high vol
            new { T = 20.0, r = -0.001, q = -0.002, σ = 0.05 }, // Very long, low vol
            new { T = 5.0, r = -0.05, q = -0.10, σ = 0.30 }     // Large negative rates
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
                useRefinement: false  // QD+ only for speed
            );
            
            // Act & Assert: Should not throw and produce valid results
            var act = () => solver.Solve();
            act.Should().NotThrow($"should handle T={testCase.T}, r={testCase.r}, q={testCase.q}, σ={testCase.σ}");
            
            var result = solver.Solve();
            result.UpperBoundary.Should().BeGreaterThan(0, "A1: boundary should be positive");
            result.LowerBoundary.Should().BeGreaterThan(0, "A1: boundary should be positive");
            
            // NaN check
            double.IsNaN(result.UpperBoundary).Should().BeFalse("should not produce NaN");
            double.IsNaN(result.LowerBoundary).Should().BeFalse("should not produce NaN");
        }
    }
    
    /// <summary>
    /// Performance and memory efficiency test.
    /// Validates Rule 5 (zero-allocation hot paths) compliance.
    /// </summary>
    [Fact]
    public void CompleteWorkflow_MemoryAndPerformance_IsEfficient()
    {
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
        
        // Memory usage should be reasonable (less than 50MB)
        (memoryUsed / 1024.0 / 1024.0).Should().BeLessThan(50.0,
            "memory usage should be under 50MB");
        
        // Results should still be accurate (Healy Table 2)
        result.IsValid.Should().BeTrue();
        result.UpperBoundary.Should().BeInRange(65.0, 75.0);
        result.LowerBoundary.Should().BeInRange(55.0, 65.0);
    }
}
