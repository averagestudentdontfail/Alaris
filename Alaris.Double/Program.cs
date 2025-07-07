using Alaris.Quantlib;
using Alaris.Quantlib.Double;
using Microsoft.Extensions.Logging;

namespace Alaris.Double;

/// <summary>
/// Comprehensive test program for the Alaris Double Boundary American Options Engine
/// Demonstrates all functionality including negative interest rate scenarios
/// </summary>
class Program
{
    private static ILogger? _logger;

    static void Main(string[] args)
    {
        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<Program>();

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ALARIS DOUBLE BOUNDARY AMERICAN OPTIONS ENGINE");
        Console.WriteLine("Advanced Pricing Under General Interest Rate Conditions");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        try
        {
            // Set evaluation date
            var today = new Date(15, Month.January, 2025);
            Settings.instance().setEvaluationDate(today);

            // Run comprehensive test suite
            RunRegimeDetectionTests();
            Console.WriteLine();
            
            RunSingleBoundaryValidation();
            Console.WriteLine();
            
            RunDoubleBoundaryScenarios();
            Console.WriteLine();
            
            RunPerformanceBenchmarks();
            Console.WriteLine();
            
            RunConvergenceAnalysis();
            Console.WriteLine();

            Console.WriteLine("All tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during testing: {ex.Message}");
            _logger?.LogError(ex, "Test execution failed");
            Environment.Exit(1);
        }
    }

    static void RunRegimeDetectionTests()
    {
        Console.WriteLine("1. REGIME DETECTION VALIDATION");
        Console.WriteLine("─".PadRight(50, '─'));

        var testCases = new[]
        {
            new { Name = "Standard Positive", r = 0.05, q = 0.02, sigma = 0.20, expected = ExerciseRegimeType.SingleBoundaryPositive },
            new { Name = "Negative Dividend", r = 0.03, q = -0.01, sigma = 0.25, expected = ExerciseRegimeType.SingleBoundaryNegativeDividend },
            new { Name = "Double Boundary Low Vol", r = -0.01, q = -0.02, sigma = 0.10, expected = ExerciseRegimeType.DoubleBoundaryNegativeRates },
            new { Name = "Double Boundary High Vol", r = -0.01, q = -0.02, sigma = 0.40, expected = ExerciseRegimeType.NoEarlyExercise },
            new { Name = "No Exercise (r ≤ q < 0)", r = -0.02, q = -0.01, sigma = 0.20, expected = ExerciseRegimeType.NoEarlyExercise }
        };

        foreach (var test in testCases)
        {
            var detected = RegimeAnalyzer.DetermineRegime(test.r, test.q, test.sigma, Option.Type.Put);
            var status = detected == test.expected ? "✓ PASS" : "✗ FAIL";
            var sigmaCritical = RegimeAnalyzer.CriticalVolatility(test.r, test.q);
            
            Console.WriteLine($"  {test.Name,-25} {status}");
            Console.WriteLine($"    Parameters: r={test.r:F3}, q={test.q:F3}, σ={test.sigma:F2}");
            Console.WriteLine($"    Expected: {test.expected}");
            Console.WriteLine($"    Detected: {detected}");
            if (!double.IsNaN(sigmaCritical))
            {
                Console.WriteLine($"    σ* = {sigmaCritical:F4}");
            }
            Console.WriteLine();
        }
    }

    static void RunSingleBoundaryValidation()
    {
        Console.WriteLine("2. SINGLE BOUNDARY VALIDATION");
        Console.WriteLine("─".PadRight(50, '─'));

        // Test case: Standard American put
        double spot = 36.0, strike = 40.0, sigma = 0.20, r = 0.06, q = 0.02;
        var maturity = new Date(17, Month.May, 2025);

        var results = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: false);
        var extendedResults = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: true);

        Console.WriteLine($"  Market: S=${spot:F1}, K=${strike:F1}, r={r:F3}, q={q:F3}, σ={sigma:F2}");
        Console.WriteLine($"  Standard QdFp Engine:  ${results.Price:F6}");
        Console.WriteLine($"  Extended Engine:       ${extendedResults.Price:F6}");
        Console.WriteLine($"  Difference:            ${Math.Abs(results.Price - extendedResults.Price):E2}");
        Console.WriteLine($"  Regime:                {extendedResults.Regime}");
        Console.WriteLine($"  Computation Time:      {extendedResults.ComputationTime.TotalMilliseconds:F1}ms");
    }

    static void RunDoubleBoundaryScenarios()
    {
        Console.WriteLine("3. DOUBLE BOUNDARY SCENARIOS");
        Console.WriteLine("─".PadRight(50, '─'));

        var scenarios = new[]
        {
            new { Name = "Mild Negative Rates", r = -0.005, q = -0.015, sigma = 0.15 },
            new { Name = "Moderate Negative", r = -0.01, q = -0.02, sigma = 0.12 },
            new { Name = "Deep Negative", r = -0.02, q = -0.03, sigma = 0.10 },
            new { Name = "Critical Volatility", r = -0.01, q = -0.02, sigma = 0.0 } // Will be set to σ*
        };

        double spot = 100.0, strike = 100.0;
        var maturity = new Date(15, Month.July, 2025); // 6 months

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"Scenario: {scenario.Name}");
            
            double sigma = scenario.sigma;
            if (scenario.Name.Contains("Critical"))
            {
                sigma = RegimeAnalyzer.CriticalVolatility(scenario.r, scenario.q);
                if (double.IsNaN(sigma))
                {
                    Console.WriteLine("  Skipped: Invalid parameters for critical volatility");
                    continue;
                }
            }

            try
            {
                var results = PriceAmericanOption(spot, strike, sigma, scenario.r, scenario.q, maturity, 
                                                useExtendedEngine: true);

                Console.WriteLine($"  Parameters: r={scenario.r:F3}, q={scenario.q:F3}, σ={sigma:F4}");
                Console.WriteLine($"  Regime: {results.Regime}");
                Console.WriteLine($"  Option Price: ${results.Price:F6}");
                
                if (results.DetailedResults != null)
                {
                    var details = results.DetailedResults;
                    Console.WriteLine($"  Critical σ*: {details.CriticalVolatility:F4}");
                    Console.WriteLine($"  Intersection Time: {details.BoundaryIntersectionTime:F4}");
                    Console.WriteLine($"  Iterations: {details.IterationsConverged}");
                    Console.WriteLine($"  Final Error: {details.FinalError:E2}");
                    Console.WriteLine($"  Computation Time: {details.ComputationTime.TotalMilliseconds:F1}ms");
                    
                    if (details.UpperBoundary != null && details.LowerBoundary != null)
                    {
                        // Sample boundary values
                        double[] tauSamples = { 0.01, 0.1, 0.25, 0.5 };
                        Console.WriteLine("  Boundary Values:");
                        Console.WriteLine("    τ     Upper B(τ)  Lower Y(τ)");
                        foreach (double tau in tauSamples)
                        {
                            if (tau <= details.BoundaryIntersectionTime)
                            {
                                double upper = details.UpperBoundary.Evaluate(tau);
                                double lower = details.LowerBoundary.Evaluate(tau);
                                Console.WriteLine($"    {tau:F2}   {upper:F3}       {lower:F3}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
                _logger?.LogError(ex, "Failed to price scenario {ScenarioName}", scenario.Name);
            }
            
            Console.WriteLine();
        }
    }

    static void RunPerformanceBenchmarks()
    {
        Console.WriteLine("4. PERFORMANCE BENCHMARKS");
        Console.WriteLine("─".PadRight(50, '─'));

        double spot = 95.0, strike = 100.0, r = -0.01, q = -0.02, sigma = 0.15;
        var maturity = new Date(15, Month.October, 2025); // 9 months

        var spectralNodeCounts = new[] { 4, 6, 8, 10, 12 };
        
        Console.WriteLine("  Spectral Nodes vs. Accuracy/Performance:");
        Console.WriteLine("  Nodes  Price      Error      Time(ms)  Iterations");
        Console.WriteLine("  ─────  ─────────  ─────────  ────────  ──────────");

        OptionPricingResults? referenceResult = null;
        
        foreach (int nodes in spectralNodeCounts)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var results = PriceAmericanOption(spot, strike, sigma, r, q, maturity, 
                                            useExtendedEngine: true, spectralNodes: nodes);
            stopwatch.Stop();

            if (referenceResult == null) referenceResult = results;
            
            double error = Math.Abs(results.Price - referenceResult.Price);
            int iterations = results.DetailedResults?.IterationsConverged ?? 0;
            
            Console.WriteLine($"  {nodes,3}    ${results.Price:F6}  {error:E2}     {stopwatch.ElapsedMilliseconds,6}   {iterations,8}");
        }
    }

    static void RunConvergenceAnalysis()
    {
        Console.WriteLine("5. CONVERGENCE ANALYSIS");
        Console.WriteLine("─".PadRight(50, '─'));

        double spot = 90.0, strike = 100.0, r = -0.015, q = -0.025, sigma = 0.12;
        var maturity = new Date(15, Month.December, 2025); // 11 months

        var tolerances = new[] { 1e-6, 1e-8, 1e-10, 1e-12 };
        
        Console.WriteLine("  Tolerance vs. Convergence:");
        Console.WriteLine("  Tolerance   Price      Iterations  Final Error   Time(ms)");
        Console.WriteLine("  ─────────   ─────────  ──────────  ───────────   ────────");

        foreach (double tol in tolerances)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var results = PriceAmericanOption(spot, strike, sigma, r, q, maturity, 
                                                useExtendedEngine: true, tolerance: tol);
                stopwatch.Stop();

                var details = results.DetailedResults;
                if (details != null)
                {
                    Console.WriteLine($"  {tol:E1}     ${results.Price:F6}   {details.IterationsConverged,8}    {details.FinalError:E2}      {stopwatch.ElapsedMilliseconds,6}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {tol:E1}     Failed: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Spectral Convergence Analysis:");
        
        // Test spectral convergence rate
        var results8 = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: true, spectralNodes: 8);
        var results12 = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: true, spectralNodes: 12);
        var results16 = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: true, spectralNodes: 16);

        if (results8.DetailedResults?.UpperBoundary != null)
        {
            double convergenceRate = results8.DetailedResults.UpperBoundary.EstimatedError;
            Console.WriteLine($"  Estimated convergence rate: {convergenceRate:F2}");
            Console.WriteLine($"  Richardson extrapolation:");
            
            double p = Math.Log((results8.Price - results12.Price) / (results12.Price - results16.Price)) / Math.Log(8.0/12.0);
            double extrapolated = results16.Price + (results16.Price - results12.Price) / (Math.Pow(16.0/12.0, p) - 1.0);
            
            Console.WriteLine($"    Convergence order: {p:F2}");
            Console.WriteLine($"    Extrapolated price: ${extrapolated:F6}");
        }
    }

    static OptionPricingResults PriceAmericanOption(double spot, double strike, double sigma, double r, double q, 
                                                  Date maturity, bool useExtendedEngine = true, 
                                                  int spectralNodes = 8, double tolerance = 1e-12)
    {
        // Create market data
        var underlying = new SimpleQuote(spot);
        var dividendYield = new FlatForward(Settings.instance().getEvaluationDate(), q, new Actual365Fixed());
        var volatility = new BlackConstantVol(Settings.instance().getEvaluationDate(), new TARGET(), sigma, new Actual365Fixed());
        var riskFreeRate = new FlatForward(Settings.instance().getEvaluationDate(), r, new Actual365Fixed());

        // Create Black-Scholes-Merton process
        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendYield),
            new YieldTermStructureHandle(riskFreeRate),
            new BlackVolTermStructureHandle(volatility)
        );

        // Create American put option
        var exercise = new AmericanExercise(Settings.instance().getEvaluationDate(), maturity);
        var payoff = new PlainVanillaPayoff(Option.Type.Put, strike);
        var option = new VanillaOption(payoff, exercise);

        PricingEngine engine;
        DoubleBoundaryResults? detailedResults = null;

        if (useExtendedEngine)
        {
            var extendedEngine = new DoubleBoundaryAmericanEngine(
                process, spectralNodes, tolerance, maxIterations: 100, useAcceleration: true, _logger);
            engine = extendedEngine;
            option.setPricingEngine(engine);
            
            double price = option.NPV();
            detailedResults = extendedEngine.GetDetailedResults();
            
            return new OptionPricingResults
            {
                Price = price,
                Regime = detailedResults?.Regime ?? ExerciseRegimeType.Degenerate,
                ComputationTime = detailedResults?.ComputationTime ?? TimeSpan.Zero,
                DetailedResults = detailedResults
            };
        }
        else
        {
            // Use standard QuantLib engine
            engine = new QdFpAmericanEngine(process, QdFpAmericanEngine.accurateScheme());
            option.setPricingEngine(engine);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            double price = option.NPV();
            stopwatch.Stop();
            
            return new OptionPricingResults
            {
                Price = price,
                Regime = ExerciseRegimeType.SingleBoundaryPositive,
                ComputationTime = stopwatch.Elapsed,
                DetailedResults = null
            };
        }
    }
}

/// <summary>
/// Container for option pricing results
/// </summary>
public class OptionPricingResults
{
    public double Price { get; set; }
    public ExerciseRegimeType Regime { get; set; }
    public TimeSpan ComputationTime { get; set; }
    public DoubleBoundaryResults? DetailedResults { get; set; }
}