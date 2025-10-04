using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit
{
    public class DoubleBoundaryEngineTest
    {
        [Fact]
        public void Calculate_PositiveRates_UseSingleBoundaryEngine()
        {
            // Arrange
            var (process, option) = CreateTestOption(
                S: 100.0, K: 100.0, T: 1.0,
                r: 0.05, q: 0.02, sigma: 0.2);
            
            var doubleEngine = new DoubleBoundaryEngine(process);
            var singleEngine = new QdFpAmericanEngine(process);
            
            // Act
            option.setPricingEngine(doubleEngine);
            double doublePrice = option.NPV();
            
            option.setPricingEngine(singleEngine);
            double singlePrice = option.NPV();
            
            // Assert - Should give same result (using fallback)
            doublePrice.Should().BeApproximately(singlePrice, 1e-6,
                "positive rates should use single boundary engine");
        }
        
        [Fact]
        public void Calculate_NegativeRates_DoubleBoundary_ProducesReasonablePrice()
        {
            // Arrange
            var (process, option) = CreateTestOption(
                S: 100.0, K: 100.0, T: 5.0,
                r: -0.005, q: -0.01, sigma: 0.08);
            
            var engine = new DoubleBoundaryEngine(process);
            
            // Act
            option.setPricingEngine(engine);
            double price = option.NPV();
            double delta = option.delta();
            
            // Assert
            price.Should().BeGreaterThan(0, "option should have positive value");
            price.Should().BeLessThan(100.0, "put price should be less than strike");
            
            // For ATM put, delta should be negative and around -0.5
            delta.Should().BeNegative();
            delta.Should().BeGreaterThan(-1.0);
            
            Console.WriteLine($"Price: {price:F4}, Delta: {delta:F4}");
        }
        
        [Fact]
        public void Calculate_NeverExerciseRegime_PricesAsEuropean()
        {
            // Arrange - r <= 0 and r <= q means never optimal to exercise
            var (process, option) = CreateTestOption(
                S: 100.0, K: 100.0, T: 1.0,
                r: -0.02, q: -0.01, sigma: 0.2);
            
            var americanEngine = new DoubleBoundaryEngine(process);
            var europeanEngine = new AnalyticEuropeanEngine(process);
            
            // Act
            option.setPricingEngine(americanEngine);
            double americanPrice = option.NPV();
            
            var europeanOption = new VanillaOption(
                option.payoff(),
                new EuropeanExercise(option.exercise().lastDate()));
            europeanOption.setPricingEngine(europeanEngine);
            double europeanPrice = europeanOption.NPV();
            
            // Assert
            americanPrice.Should().BeApproximately(europeanPrice, 1e-4,
                "should price as European when never optimal to exercise");
        }
        
        [Theory]
        [InlineData(0.04)] // Low vol
        [InlineData(0.08)] // Medium vol
        [InlineData(0.15)] // High vol
        public void Calculate_VariousVolatilities_AllProduceValidPrices(double sigma)
        {
            // Arrange
            var (process, option) = CreateTestOption(
                S: 100.0, K: 100.0, T: 5.0,
                r: -0.005, q: -0.01, sigma: sigma);
            
            var engine = new DoubleBoundaryEngine(process);
            
            // Act
            option.setPricingEngine(engine);
            double price = option.NPV();
            double delta = option.delta();
            double gamma = option.gamma();
            
            // Assert
            price.Should().BeGreaterThan(0);
            price.Should().BeLessThan(100.0);
            
            delta.Should().BeInRange(-1.0, 0.0);
            gamma.Should().BeGreaterThanOrEqualTo(0, "gamma should be non-negative");
            
            Console.WriteLine($"σ={sigma:F2}: Price={price:F4}, Δ={delta:F4}, Γ={gamma:F6}");
        }
        
        [Fact]
        public void Calculate_Greeks_AllFiniteAndReasonable()
        {
            // Arrange
            var (process, option) = CreateTestOption(
                S: 100.0, K: 100.0, T: 1.0,
                r: -0.005, q: -0.01, sigma: 0.2);
            
            var engine = new DoubleBoundaryEngine(process);
            option.setPricingEngine(engine);
            
            // Act
            double price = option.NPV();
            double delta = option.delta();
            double gamma = option.gamma();
            double vega = option.vega();
            
            // Assert
            price.Should().BeGreaterThan(0);
            
            double.IsFinite(delta).Should().BeTrue();
            delta.Should().BeInRange(-1.0, 0.0, "put delta should be negative");
            
            double.IsFinite(gamma).Should().BeTrue();
            gamma.Should().BeGreaterThanOrEqualTo(0);
            
            double.IsFinite(vega).Should().BeTrue();
            vega.Should().BeGreaterThan(0, "vega should be positive");
        }
        
        private (GeneralizedBlackScholesProcess process, VanillaOption option) CreateTestOption(
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