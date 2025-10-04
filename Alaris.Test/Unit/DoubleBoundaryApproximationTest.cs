using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit
{
    public class DoubleBoundaryApproximationTest
    {
        [Fact]
        public void ComputeInitialBoundaries_WithNegativeRates_ReturnsValidBoundaries()
        {
            // Arrange
            var (process, K, T) = CreateTestProcess(
                S: 100.0, r: -0.005, q: -0.01, sigma: 0.08);
            var approximation = new DoubleBoundaryApproximation(process, K, T, -0.005, -0.01, 0.08);
            
            // Act
            var result = approximation.ComputeInitialBoundaries(m: 50);
            
            // Assert
            result.UpperBoundary.Should().NotBeNull();
            result.LowerBoundary.Should().NotBeNull();
            result.UpperBoundary.Length.Should().Be(50);
            result.LowerBoundary.Length.Should().Be(50);
            
            // At maturity (last point)
            result.UpperBoundary[49].Should().BeApproximately(K, 0.01);
            result.LowerBoundary[49].Should().BeApproximately(K * 0.5, 0.5); // r/q = 0.5
        }
        
        [Fact]
        public void ComputeInitialBoundaries_NoCrossing_CrossingTimeIsZero()
        {
            // Arrange - low volatility, should not cross
            var (process, K, T) = CreateTestProcess(
                S: 100.0, r: -0.005, q: -0.01, sigma: 0.04);
            var approximation = new DoubleBoundaryApproximation(process, K, T, -0.005, -0.01, 0.04);
            
            // Act
            var result = approximation.ComputeInitialBoundaries(m: 100);
            
            // Assert
            result.CrossingTime.Should().Be(0, "boundaries should not cross at low volatility");
            
            // Upper should be above lower throughout
            for (int i = 0; i < result.UpperBoundary.Length; i++)
            {
                result.UpperBoundary[i].Should().BeGreaterThanOrEqualTo(
                    result.LowerBoundary[i],
                    $"at index {i}");
            }
        }
        
        [Fact]
        public void ComputeInitialBoundaries_HighVolatility_BoundariesCross()
        {
            // Arrange - high volatility should cause crossing
            var (process, K, T) = CreateTestProcess(
                S: 100.0, r: -0.005, q: -0.01, sigma: 0.15);
            var approximation = new DoubleBoundaryApproximation(process, K, T, -0.005, -0.01, 0.15);
            
            // Act
            var result = approximation.ComputeInitialBoundaries(m: 100);
            
            // Assert
            result.CrossingTime.Should().BeGreaterThan(0, "boundaries should cross at high volatility");
            result.CrossingTime.Should().BeLessThan(T, "crossing should occur before maturity");
            
            Console.WriteLine($"Boundaries crossed at t = {result.CrossingTime:F4} years");
        }
        
        [Theory]
        [InlineData(0.04, false)] // Low vol - no crossing
        [InlineData(0.08, false)] // Medium vol - no crossing
        [InlineData(0.15, true)]  // High vol - crossing expected
        public void ComputeInitialBoundaries_VariousVolatilities_BehavesAsExpected(
            double sigma, bool shouldCross)
        {
            // Arrange
            var (process, K, T) = CreateTestProcess(
                S: 100.0, r: -0.005, q: -0.01, sigma: sigma);
            var approximation = new DoubleBoundaryApproximation(process, K, T, -0.005, -0.01, sigma);
            
            // Act
            var result = approximation.ComputeInitialBoundaries(m: 100);
            
            // Assert
            if (shouldCross)
            {
                result.CrossingTime.Should().BeGreaterThan(0);
            }
            else
            {
                result.CrossingTime.Should().Be(0);
                // Verify monotonicity: upper >= lower throughout
                for (int i = 0; i < result.UpperBoundary.Length; i++)
                {
                    result.UpperBoundary[i].Should().BeGreaterThanOrEqualTo(result.LowerBoundary[i]);
                }
            }
        }
        
        [Fact]
        public void ComputeInitialBoundaries_MonotonicityCheck_UpperDecreases()
        {
            // Arrange
            var (process, K, T) = CreateTestProcess(
                S: 100.0, r: -0.005, q: -0.01, sigma: 0.08);
            var approximation = new DoubleBoundaryApproximation(process, K, T, -0.005, -0.01, 0.08);
            
            // Act
            var result = approximation.ComputeInitialBoundaries(m: 100);
            
            // Assert - Upper boundary should decrease over time (Healy Section 3)
            for (int i = 1; i < result.UpperBoundary.Length; i++)
            {
                result.UpperBoundary[i].Should().BeLessOrEqualTo(
                    result.UpperBoundary[i - 1] + 0.01, // Allow small numerical tolerance
                    $"upper boundary should decrease from index {i-1} to {i}");
            }
        }
        
        private (GeneralizedBlackScholesProcess process, double K, double T) CreateTestProcess(
            double S, double r, double q, double sigma)
        {
            var valuationDate = new Date(15, Month.March, 2024);
            Settings.instance().setEvaluationDate(valuationDate);
            
            var spot = new SimpleQuote(S);
            var spotHandle = new QuoteHandle(spot);
            
            var riskFreeRate = new FlatForward(valuationDate, r, new Actual365Fixed());
            var dividendYield = new FlatForward(valuationDate, q, new Actual365Fixed());
            var volatility = new BlackConstantVol(valuationDate, new TARGET(), sigma, new Actual365Fixed());
            
            var process = new GeneralizedBlackScholesProcess(
                spotHandle,
                new YieldTermStructureHandle(dividendYield),
                new YieldTermStructureHandle(riskFreeRate),
                new BlackVolTermStructureHandle(volatility));
            
            double K = 100.0;
            double T = 5.0;
            
            return (process, K, T);
        }
    }
}
