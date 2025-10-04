using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using Alaris.Double;

namespace Alaris.Test.Benchmarks
{
    /// <summary>
    /// Performance benchmarks comparing single vs double boundary engines.
    /// </summary>
    public class PerformanceTests
    {
        private readonly ITestOutputHelper _output;
        
        public PerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }
        
        [Fact]
        public void Benchmark_SingleVsDoubleBoundary_Throughput()
        {
            // Arrange
            var testCases = new[]
            {
                new { S = 100.0, K = 100.0, T = 1.0, r = 0.05, q = 0.02, sigma = 0.2, Name = "Positive Rates" },
                new { S = 100.0, K = 100.0, T = 5.0, r = -0.005, q = -0.01, sigma = 0.08, Name = "Negative Rates (Low Vol)" },
                new { S = 100.0, K = 100.0, T = 5.0, r = -0.005, q = -0.01, sigma = 0.15, Name = "Negative Rates (High Vol)" }
            };
            
            _output.WriteLine("=== Performance Benchmark ===\n");
            _output.WriteLine($"{"Scenario",-30} {"Engine",-20} {"Time (ms)",-15} {"Options/sec",-15}");
            _output.WriteLine(new string('-', 80));
            
            foreach (var tc in testCases)
            {
                // Single boundary engine
                var singleTime = BenchmarkEngine(
                    () => CreateSingleEngine(tc.S, tc.K, tc.T, tc.r, tc.q, tc.sigma),
                    iterations: 100);
                
                // Double boundary engine
                var doubleTime = BenchmarkEngine(
                    () => CreateDoubleEngine(tc.S, tc.K, tc.T, tc.r, tc.q, tc.sigma),
                    iterations: 100);
                
                _output.WriteLine($"{tc.Name,-30} {"Single",-20} {singleTime,-15:F2} {100000.0 / singleTime,-15:F0}");
                _output.WriteLine($"{tc.Name,-30} {"Double",-20} {doubleTime,-15:F2} {100000.0 / doubleTime,-15:F0}");
                _output.WriteLine($"{tc.Name,-30} {"Ratio",-20} {doubleTime / singleTime,-15:F2}x");
                _output.WriteLine("");
            }
        }
        
        [Fact]
        public void Benchmark_VariousSchemes_Accuracy()
        {
            // Arrange
            var schemes = new[]
            {
                new { Scheme = QdFpAmericanEngine.fastScheme(), Name = "Fast" },
                new { Scheme = QdFpAmericanEngine.accurateScheme(), Name = "Accurate" },
                new { Scheme = QdFpAmericanEngine.highPrecisionScheme(), Name = "High Precision" }
            };
            
            _output.WriteLine("=== Accuracy vs Performance ===\n");
            _output.WriteLine($"{"Scheme",-20} {"Time (ms)",-15} {"Price",-15} {"Delta",-15}");
            _output.WriteLine(new string('-', 65));
            
            foreach (var scheme in schemes)
            {
                var (time, price, delta) = BenchmarkScheme(
                    S: 100.0, K: 100.0, T: 5.0,
                    r: -0.005, q: -0.01, sigma: 0.08,
                    scheme: scheme.Scheme,
                    iterations: 50);
                
                _output.WriteLine($"{scheme.Name,-20} {time,-15:F2} {price,-15:F4} {delta,-15:F4}");
            }
        }
        
        [Fact]
        public void Benchmark_CollocationPoints_Convergence()
        {
            // Test convergence with different numbers of collocation points
            _output.WriteLine("=== Convergence Analysis ===\n");
            _output.WriteLine($"{"Points",-15} {"Time (ms)",-15} {"Price",-15} {"Δ from prev",-15}");
            _output.WriteLine(new string('-', 60));
            
            double prevPrice = 0;
            var pointCounts = new[] { 25, 50, 100, 150, 200 };
            
            foreach (var m in pointCounts)
            {
                var sw = Stopwatch.StartNew();
                
                var (process, option) = CreateTestOption(
                    S: 100.0, K: 100.0, T: 5.0,
                    r: -0.005, q: -0.01, sigma: 0.08);
                
                var approximation = new DoubleBoundaryApproximation(
                    process, 100.0, 5.0, -0.005, -0.01, 0.08);
                
                var result = approximation.ComputeInitialBoundaries(m);
                
                var engine = new DoubleBoundaryEngine(process);
                option.setPricingEngine(engine);
                double price = option.NPV();
                
                sw.Stop();
                
                double delta = prevPrice == 0 ? 0 : Math.Abs(price - prevPrice);
                
                _output.WriteLine($"{m,-15} {sw.Elapsed.TotalMilliseconds,-15:F2} {price,-15:F4} {delta,-15:F6}");
                
                prevPrice = price;
            }
        }
        
        private double BenchmarkEngine(Func<VanillaOption> createOption, int iterations)
        {
            // Warmup
            for (int i = 0; i < 10; i++)
            {
                var warmup = createOption();
                _ = warmup.NPV();
            }
            
            // Actual benchmark
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var option = createOption();
                _ = option.NPV();
            }
            
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
        
        private (double time, double price, double delta) BenchmarkScheme(
            double S, double K, double T, double r, double q, double sigma,
            QdFpIterationScheme scheme, int iterations)
        {
            var sw = Stopwatch.StartNew();
            
            double price = 0, delta = 0;
            
            for (int i = 0; i < iterations; i++)
            {
                var (process, option) = CreateTestOption(S, K, T, r, q, sigma);
                var engine = new DoubleBoundaryEngine(process, scheme);
                option.setPricingEngine(engine);
                
                price = option.NPV();
                delta = option.delta();
            }
            
            sw.Stop();
            return (sw.Elapsed.TotalMilliseconds, price, delta);
        }
        
        private VanillaOption CreateSingleEngine(
            double S, double K, double T, double r, double q, double sigma)
        {
            var (process, option) = CreateTestOption(S, K, T, r, q, sigma);
            var engine = new QdFpAmericanEngine(process);
            option.setPricingEngine(engine);
            return option;
        }
        
        private VanillaOption CreateDoubleEngine(
            double S, double K, double T, double r, double q, double sigma)
        {
            var (process, option) = CreateTestOption(S, K, T, r, q, sigma);
            var engine = new DoubleBoundaryEngine(process);
            option.setPricingEngine(engine);
            return option;
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
            
            var maturityDate = new Date(valuationDate.serialNumber() + (int)(T * 365));
            var exercise = new AmericanExercise(valuationDate, maturityDate);
            var payoff = new PlainVanillaPayoff(Option.Type.Put, K);
            var option = new VanillaOption(payoff, exercise);
            
            return (process, option);
        }
    }
}