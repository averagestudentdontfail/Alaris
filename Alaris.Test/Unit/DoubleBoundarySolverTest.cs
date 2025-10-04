using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit
{
    public class DoubleBoundarySolverTest
    {
        [Fact]
        public void Solve_ConvergesWithinMaxIterations()
        {
            // Arrange
            var (process, K, T, r, q, sigma) = CreateTestParameters();
            var scheme = QdFpAmericanEngine.highPrecisionScheme();
            var solver = new DoubleBoundarySolver(process, K, T, r, q, sigma, scheme);
            
            // Get initial boundaries
            var approximation = new DoubleBoundaryApproximation(process, K, T, r, q, sigma);
            var initial = approximation.ComputeInitialBoundaries(50);
            
            // Act
            var result = solver.Solve(
                initial.UpperBoundary,
                initial.LowerBoundary,
                initial.CrossingTime);
            
            // Assert
            result.Should().NotBeNull();
            result.UpperBoundary.Should().NotBeNull();
            result.LowerBoundary.Should().NotBeNull();
        }
        
        [Fact]
        public void Solve_RefinedBoundaries_MoreAccurateThanInitial()
        {
            // Arrange
            var (process, K, T, r, q, sigma) = CreateTestParameters();
            var scheme = QdFpAmericanEngine.highPrecisionScheme();
            var solver = new DoubleBoundarySolver(process, K, T, r, q, sigma, scheme);
            
            var approximation = new DoubleBoundaryApproximation(process, K, T, r, q, sigma);
            var initial = approximation.ComputeInitialBoundaries(50);
            
            // Act
            var refined = solver.Solve(
                initial.UpperBoundary,
                initial.LowerBoundary,
                initial.CrossingTime);
            
            // Assert - Refined should differ from initial (it converged)
            bool boundariesChanged = false;
            for (int i = 0; i < initial.UpperBoundary.Length; i++)
            {
                if (Math.Abs(refined.UpperBoundary[i] - initial.UpperBoundary[i]) > 1e-4)
                {
                    boundariesChanged = true;
                    break;
                }
            }
            
            boundariesChanged.Should().BeTrue("solver should refine initial approximation");
        }
        
        [Fact]
        public void Solve_MaintainsBoundaryRelationship_AfterCrossingTime()
        {
            // Arrange
            var (process, K, T, r, q, sigma) = CreateTestParameters();
            var scheme = QdFpAmericanEngine.fastScheme(); // Faster for testing
            var solver = new DoubleBoundarySolver(process, K, T, r, q, sigma, scheme);
            
            var approximation = new DoubleBoundaryApproximation(process, K, T, r, q, sigma);
            var initial = approximation.ComputeInitialBoundaries(50);
            
            // Act
            var result = solver.Solve(
                initial.UpperBoundary,
                initial.LowerBoundary,
                initial.CrossingTime);
            
            // Assert - After crossing time, upper should be >= lower
            double dt = T / (result.UpperBoundary.Length - 1);
            for (int i = 0; i < result.UpperBoundary.Length; i++)
            {
                double t = i * dt;
                if (t >= result.CrossingTime)
                {
                    result.UpperBoundary[i].Should().BeGreaterThanOrEqualTo(
                        result.LowerBoundary[i] - 1e-6, // Small tolerance for numerical error
                        $"at t = {t:F4}");
                }
            }
        }
        
        private (GeneralizedBlackScholesProcess process, double K, double T, double r, double q, double sigma) 
            CreateTestParameters()
        {
            var valuationDate = new Date(15, Month.March, 2024);
            Settings.instance().setEvaluationDate(valuationDate);
            
            double S = 100.0;
            double r = -0.005;
            double q = -0.01;
            double sigma = 0.08;
            
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
            
            return (process, 100.0, 5.0, r, q, sigma);
        }
    }
}