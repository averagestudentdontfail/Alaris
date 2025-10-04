using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Integration
{
    /// <summary>
    /// Validates implementation against examples from Healy (2021) paper.
    /// Reference: Table 2, Table 3, Figure 1
    /// </summary>
    public class Healy
    {
        [Fact]
        public void Table2_ShortMaturity_MatchesHealyResults()
        {
            // Healy Table 2: T=10, K=100, σ=8%, r=-0.5%, q=-1%
            // Expected prices from paper (TR-BDF2 reference)
            
            var testCases = new[]
            {
                new { S = 100.0, T = 10.0, Expected = 8.598, Tolerance = 0.020 },
                new { S = 120.0, T = 10.0, Expected = 2.952, Tolerance = 0.010 }
            };
            
            foreach (var tc in testCases)
            {
                // Arrange
                var (process, option) = CreateHealyTestOption(
                    S: tc.S, K: 100.0, T: tc.T,
                    r: -0.005, q: -0.01, sigma: 0.08);
                
                var engine = new DoubleBoundaryEngine(
                    process,
                    QdFpAmericanEngine.highPrecisionScheme());
                
                // Act
                option.setPricingEngine(engine);
                double price = option.NPV();
                
                // Assert
                price.Should().BeApproximately(tc.Expected, tc.Tolerance,
                    $"S={tc.S}, T={tc.T} should match Healy Table 2");
                
                Console.WriteLine($"S={tc.S}, T={tc.T}: Price={price:F4} (Expected={tc.Expected:F3})");
            }
        }
        
        [Fact]
        public void Table2_LongMaturity_DetectsCrossing()
        {
            // Healy Table 2: T=20, K=100, σ=8%, r=-0.5%, q=-1%
            // Boundaries should cross before maturity
            
            var testCases = new[]
            {
                new { S = 100.0, T = 20.0, Expected = 11.684, Tolerance = 0.030 },
                new { S = 120.0, T = 20.0, Expected = 5.687, Tolerance = 0.015 }
            };
            
            foreach (var tc in testCases)
            {
                // Arrange
                var (process, option) = CreateHealyTestOption(
                    S: tc.S, K: 100.0, T: tc.T,
                    r: -0.005, q: -0.01, sigma: 0.08);
                
                var engine = new DoubleBoundaryEngine(process);
                
                // Act
                option.setPricingEngine(engine);
                double price = option.NPV();
                
                // Assert
                price.Should().BeApproximately(tc.Expected, tc.Tolerance);
                
                Console.WriteLine($"S={tc.S}, T={tc.T}: Price={price:F4} (Expected={tc.Expected:F3})");
            }
        }
        
        [Fact]
        public void Table3_HigherNegativeRates_MatchesHealyResults()
        {
            // Healy Table 3: K=100, σ=22%, r=-1%, q=-3%
            
            var testCases = new[]
            {
                new { S = 100.0, T = 3.0, Expected = 13.321, Tolerance = 0.030 },
                new { S = 120.0, T = 3.0, Expected = 7.102, Tolerance = 0.015 },
                new { S = 100.0, T = 5.0, Expected = 16.763, Tolerance = 0.040 },
                new { S = 120.0, T = 5.0, Expected = 10.525, Tolerance = 0.025 }
            };
            
            foreach (var tc in testCases)
            {
                // Arrange
                var (process, option) = CreateHealyTestOption(
                    S: tc.S, K: 100.0, T: tc.T,
                    r: -0.01, q: -0.03, sigma: 0.22);
                
                var engine = new DoubleBoundaryEngine(
                    process,
                    QdFpAmericanEngine.highPrecisionScheme());
                
                // Act
                option.setPricingEngine(engine);
                double price = option.NPV();
                
                // Assert
                price.Should().BeApproximately(tc.Expected, tc.Tolerance,
                    $"S={tc.S}, T={tc.T} should match Healy Table 3");
                
                Console.WriteLine($"r=-1%, q=-3%, S={tc.S}, T={tc.T}: Price={price:F4} (Expected={tc.Expected:F3})");
            }
        }
        
        [Theory]
        [InlineData(0.04, false)] // Figure 1(a) - no crossing
        [InlineData(0.08, false)] // Figure 1(b) - no crossing
        [InlineData(0.15, true)]  // Figure 1(c) - boundaries cross
        public void Figure1_BoundaryBehavior_MatchesHealyFigures(double sigma, bool shouldCross)
        {
            // Arrange - Healy Figure 1: T=5, K=100, r=-0.5%, q=-1%
            var (process, option) = CreateHealyTestOption(
                S: 100.0, K: 100.0, T: 5.0,
                r: -0.005, q: -0.01, sigma: sigma);
            
            var approximation = new DoubleBoundaryApproximation(
                process, 100.0, 5.0, -0.005, -0.01, sigma);
            
            // Act
            var result = approximation.ComputeInitialBoundaries(100);
            
            // Assert
            if (shouldCross)
            {
                result.CrossingTime.Should().BeGreaterThan(0,
                    $"σ={sigma} should produce crossing boundaries");
                Console.WriteLine($"σ={sigma}: Boundaries cross at t={result.CrossingTime:F4}");
            }
            else
            {
                result.CrossingTime.Should().Be(0,
                    $"σ={sigma} should not produce crossing boundaries");
                
                // Verify upper > lower throughout
                for (int i = 0; i < result.UpperBoundary.Length; i++)
                {
                    result.UpperBoundary[i].Should().BeGreaterThanOrEqualTo(
                        result.LowerBoundary[i],
                        $"σ={sigma}, index={i}");
                }
            }
        }
        
        private (GeneralizedBlackScholesProcess process, VanillaOption option) CreateHealyTestOption(
            double S, double K, double T, double r, double q, double sigma)
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
            
            var maturityDate = new Date(
                valuationDate.serialNumber() + (int)(T * 365));
            var exercise = new AmericanExercise(valuationDate, maturityDate);
            var payoff = new PlainVanillaPayoff(Option.Type.Put, K);
            var option = new VanillaOption(payoff, exercise);
            
            return (process, option);
        }
    }
}