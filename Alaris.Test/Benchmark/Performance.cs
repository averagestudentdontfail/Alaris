using System;
using System.Diagnostics;
using System.Linq;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Benchmark;

/// <summary>
/// Performance benchmark tests for the double boundary solver.
/// Validates computation speed and scalability.
/// </summary>
public class DoubleBoundaryBenchmarkTests
{
    [Theory]
    [InlineData(10, 100)]   // 10 points, expect < 100ms
    [InlineData(50, 500)]   // 50 points, expect < 500ms
    [InlineData(100, 2000)] // 100 points, expect < 2000ms
    public void QdPlusApproximation_PerformanceScaling(int iterations, int maxMilliseconds)
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
        
        // Act: Run multiple iterations for timing
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var (upper, lower) = approximation.CalculateBoundaries();
        }
        stopwatch.Stop();
        
        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxMilliseconds,
            $"QD+ should complete {iterations} iterations within {maxMilliseconds}ms");
        
        // Calculate average time per iteration
        double avgMs = stopwatch.ElapsedMilliseconds / (double)iterations;
        avgMs.Should().BeLessThan(20.0, "average time per QD+ calculation should be under 20ms");
    }
    
    [Theory]
    [InlineData(20, 1000)]   // 20 collocation points
    [InlineData(50, 3000)]   // 50 collocation points
    [InlineData(100, 8000)]  // 100 collocation points
    public void KimSolverRefinement_ScalesWithCollocation(int collocationPoints, int maxMilliseconds)
    {
        // Arrange
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
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
        
        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxMilliseconds,
            $"Kim refinement with {collocationPoints} points should complete within {maxMilliseconds}ms");
        
        result.IsValid.Should().BeTrue("result should be valid regardless of performance");
    }
    
    [Fact]
    public void BatchProcessing_MultipleOptions_Throughput()
    {
        // Test throughput for batch processing scenario
        var testCases = GenerateTestPortfolio(100);  // 100 options
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var results = testCases.Select(tc =>
        {
            var solver = new DoubleBoundarySolver(
                tc.Spot, tc.Strike, tc.Maturity,
                tc.Rate, tc.Dividend, tc.Volatility,
                tc.IsCall, 20, false  // Quick QD+ only
            );
            return solver.Solve();
        }).ToList();
        stopwatch.Stop();
        
        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            "should process 100 options within 5 seconds");
        
        double throughput = 100.0 / (stopwatch.ElapsedMilliseconds / 1000.0);
        throughput.Should().BeGreaterThan(20.0, "should process at least 20 options per second");
        
        results.All(r => r.IsValid).Should().BeTrue("all results should be valid");
    }
    
    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    [InlineData(20.0)]
    public void MaturityScaling_PerformanceCharacteristics(double maturity)
    {
        // Test how performance scales with maturity
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: maturity,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: 50,
            useRefinement: true
        );
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = solver.Solve();
        stopwatch.Stop();
        
        // Assert: Performance should not degrade significantly with maturity
        double expectedMaxMs = 1000 + maturity * 100;  // Linear scaling expectation
        stopwatch.ElapsedMilliseconds.Should().BeLessThan((int)expectedMaxMs,
            $"computation time should scale reasonably with maturity T={maturity}");
        
        result.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void MemoryAllocation_MinimalGarbageCollection()
    {
        // Test that solver doesn't cause excessive garbage collection
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false,
            collocationPoints: 100,
            useRefinement: true
        );
        
        // Force garbage collection before test
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        
        // Act: Run multiple iterations
        for (int i = 0; i < 10; i++)
        {
            var result = solver.Solve();
        }
        
        int gen0After = GC.CollectionCount(0);
        int gen1After = GC.CollectionCount(1);
        int gen2After = GC.CollectionCount(2);
        
        // Assert: Minimal garbage collection
        (gen2After - gen2Before).Should().Be(0, "should not trigger Gen 2 GC");
        (gen1After - gen1Before).Should().BeLessOrEqualTo(1, "should minimize Gen 1 GC");
        // Gen 0 collections are acceptable but should be reasonable
        (gen0After - gen0Before).Should().BeLessThan(20, "should have reasonable Gen 0 collections");
    }
    
    [Theory]
    [InlineData(1)]    // Single thread baseline
    [InlineData(2)]    // 2 threads
    [InlineData(4)]    // 4 threads
    [InlineData(8)]    // 8 threads
    public void ParallelExecution_Scalability(int threadCount)
    {
        // Test parallel execution scalability
        var testCases = GenerateTestPortfolio(100);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var results = testCases
            .AsParallel()
            .WithDegreeOfParallelism(threadCount)
            .Select(tc =>
            {
                var solver = new DoubleBoundarySolver(
                    tc.Spot, tc.Strike, tc.Maturity,
                    tc.Rate, tc.Dividend, tc.Volatility,
                    tc.IsCall, 30, false
                );
                return solver.Solve();
            })
            .ToList();
        stopwatch.Stop();
        
        // Assert
        results.Count.Should().Be(100);
        results.All(r => r.IsValid).Should().BeTrue();
        
        // Performance should improve with more threads (up to a point)
        if (threadCount > 1)
        {
            double expectedSpeedup = Math.Min(threadCount * 0.7, 4.0);  // Account for overhead
            double maxExpectedTime = 10000.0 / expectedSpeedup;
            stopwatch.ElapsedMilliseconds.Should().BeLessThan((int)maxExpectedTime,
                $"parallel execution with {threadCount} threads should show speedup");
        }
    }
    
    [Fact]
    public void CacheEfficiency_RepeatedCalculations()
    {
        // Test if repeated calculations benefit from any caching
        var solver = new DoubleBoundarySolver(
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
        
        // Warm-up
        _ = solver.Solve();
        
        // Act: Time multiple iterations
        var timings = new double[10];
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = solver.Solve();
            sw.Stop();
            timings[i] = sw.Elapsed.TotalMilliseconds;
        }
        
        // Assert: Later iterations might be faster due to JIT and cache
        double firstAvg = timings.Take(3).Average();
        double lastAvg = timings.Skip(7).Average();
        
        // Last iterations should be at least as fast as first (accounting for variance)
        lastAvg.Should().BeLessOrEqualTo(firstAvg * 1.2,
            "repeated calculations should not degrade in performance");
    }
    
    [Fact]
    public void WorstCasePerformance_BoundaryNearCrossing()
    {
        // Test performance when boundaries are very close (worst case for convergence)
        var solver = new DoubleBoundarySolver(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.20,  // Higher volatility for near-crossing
            isCall: false,
            collocationPoints: 100,
            useRefinement: true
        );
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = solver.Solve();
        stopwatch.Stop();
        
        // Assert: Even worst case should complete in reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000,
            "worst case (near crossing) should still complete within 15 seconds");
        
        result.IsValid.Should().BeTrue("result should be valid even in worst case");
    }
    
    private class TestCase
    {
        public double Spot { get; set; }
        public double Strike { get; set; }
        public double Maturity { get; set; }
        public double Rate { get; set; }
        public double Dividend { get; set; }
        public double Volatility { get; set; }
        public bool IsCall { get; set; }
    }
    
    private TestCase[] GenerateTestPortfolio(int count)
    {
        var random = new Random(42);  // Fixed seed for reproducibility
        var testCases = new TestCase[count];
        
        for (int i = 0; i < count; i++)
        {
            testCases[i] = new TestCase
            {
                Spot = 100.0,
                Strike = 90.0 + random.NextDouble() * 20.0,  // 90-110
                Maturity = 0.25 + random.NextDouble() * 4.75, // 0.25-5 years
                Rate = -0.01 + random.NextDouble() * 0.008,   // -1% to -0.2%
                Dividend = -0.02 + random.NextDouble() * 0.01, // -2% to -1%
                Volatility = 0.10 + random.NextDouble() * 0.30, // 10%-40%
                IsCall = random.NextDouble() > 0.5
            };
        }
        
        return testCases;
    }
}