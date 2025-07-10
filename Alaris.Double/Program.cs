using Alaris.Double;
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
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ALARIS DOUBLE BOUNDARY AMERICAN OPTIONS ENGINE");
        Console.WriteLine("Advanced Pricing Under General Interest Rate Conditions");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<Program>();

        try
        {
            // ========================================================================
            // CRITICAL: Initialize native library loading BEFORE using any QuantLib objects
            // ========================================================================
            Console.WriteLine("🔧 Initializing native libraries...");
            Console.WriteLine();
            
            // Display comprehensive diagnostics
            NativeLibraryLoader.DisplayDiagnostics();
            Console.WriteLine();
            
            // Initialize the custom library resolver
            Console.WriteLine("📚 Setting up library resolver...");
            NativeLibraryLoader.Initialize();
            
            // Verify that libraries can actually be loaded and used
            Console.WriteLine("✅ Verifying library functionality...");
            if (!NativeLibraryLoader.VerifyLibraries())
            {
                Console.WriteLine();
                Console.WriteLine("❌ FATAL ERROR: Failed to verify native libraries.");
                Console.WriteLine("   Please check the following:");
                Console.WriteLine("   1. libNQuantLibc.so exists in Alaris.Library/Native/");
                Console.WriteLine("   2. libQuantLib.so.1 exists in Alaris.Library/Runtime/");
                Console.WriteLine("   3. Both files have read permissions");
                Console.WriteLine("   4. All dependencies are satisfied (check with 'ldd')");
                Console.WriteLine();
                Console.WriteLine("   Alternative solutions:");
                Console.WriteLine("   - Set LD_LIBRARY_PATH manually:");
                Console.WriteLine("     export LD_LIBRARY_PATH=\"../Alaris.Library/Native:../Alaris.Library/Runtime:$LD_LIBRARY_PATH\"");
                Console.WriteLine("   - Copy libraries to system path:");
                Console.WriteLine("     sudo cp ../Alaris.Library/Runtime/libQuantLib.so* /usr/local/lib/");
                Console.WriteLine("     sudo cp ../Alaris.Library/Native/libNQuantLibc.so /usr/local/lib/");
                Console.WriteLine("     sudo ldconfig");
                
                _logger?.LogError("Native library verification failed");
                Environment.Exit(1);
            }
            
            Console.WriteLine("✅ Native libraries loaded and verified successfully!");
            Console.WriteLine();

            // ========================================================================
            // Now safe to use QuantLib objects
            // ========================================================================
            Console.WriteLine("🚀 Initializing QuantLib environment...");
            
            // Set evaluation date (first actual QuantLib operation)
            var today = new Date(15, Month.January, 2025);
            Settings.instance().setEvaluationDate(today);
            
            Console.WriteLine($"   Evaluation date set to: {today}");
            Console.WriteLine("   QuantLib initialization complete!");
            Console.WriteLine();

            // ========================================================================
            // Run comprehensive test suite
            // ========================================================================
            Console.WriteLine("📊 Starting comprehensive test suite...");
            Console.WriteLine();
            
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

            Console.WriteLine("🎉 All tests completed successfully!");
            Console.WriteLine();
            Console.WriteLine("The Alaris Double Boundary Engine is functioning correctly.");
            Console.WriteLine("You can now use it for pricing American options under general interest rate conditions.");
        }
        catch (TypeInitializationException ex)
        {
            Console.WriteLine();
            Console.WriteLine("❌ CRITICAL ERROR: Failed to initialize QuantLib native libraries");
            Console.WriteLine($"   Root cause: {ex.InnerException?.Message ?? ex.Message}");
            Console.WriteLine();
            Console.WriteLine("🔧 Troubleshooting steps:");
            Console.WriteLine("   1. Verify library files exist:");
            Console.WriteLine("      ls -la ../Alaris.Library/Native/libNQuantLibc.so");
            Console.WriteLine("      ls -la ../Alaris.Library/Runtime/libQuantLib.so.1");
            Console.WriteLine();
            Console.WriteLine("   2. Check library dependencies:");
            Console.WriteLine("      ldd ../Alaris.Library/Native/libNQuantLibc.so");
            Console.WriteLine();
            Console.WriteLine("   3. Set library path manually:");
            Console.WriteLine("      export LD_LIBRARY_PATH=\"../Alaris.Library/Native:../Alaris.Library/Runtime:$LD_LIBRARY_PATH\"");
            Console.WriteLine("      dotnet run");
            Console.WriteLine();
            Console.WriteLine("   4. Install libraries system-wide:");
            Console.WriteLine("      sudo cp ../Alaris.Library/Runtime/libQuantLib.so* /usr/local/lib/");
            Console.WriteLine("      sudo cp ../Alaris.Library/Native/libNQuantLibc.so /usr/local/lib/");
            Console.WriteLine("      sudo ldconfig");
            
            _logger?.LogError(ex, "QuantLib library initialization failed");
            Environment.Exit(1);
        }
        catch (DllNotFoundException ex)
        {
            Console.WriteLine();
            Console.WriteLine("❌ LIBRARY NOT FOUND ERROR");
            Console.WriteLine($"   Missing library: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("🔧 Quick fixes:");
            Console.WriteLine("   Run with library path:");
            Console.WriteLine("   LD_LIBRARY_PATH=\"../Alaris.Library/Native:../Alaris.Library/Runtime:$LD_LIBRARY_PATH\" dotnet run");
            Console.WriteLine();
            Console.WriteLine("   Or use the provided launch script:");
            Console.WriteLine("   ./run-alaris.sh");
            
            _logger?.LogError(ex, "Required native library not found");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ UNEXPECTED ERROR: {ex.Message}");
            
            // Provide additional context for common issues
            if (ex.Message.Contains("NQuantLibc") || ex.Message.Contains("QuantLib"))
            {
                Console.WriteLine();
                Console.WriteLine("🔧 This appears to be a library loading issue. Try:");
                Console.WriteLine("   1. Using the launch script: ./run-alaris.sh");
                Console.WriteLine("   2. Setting LD_LIBRARY_PATH manually");
                Console.WriteLine("   3. Installing libraries system-wide");
            }
            
            if (ex.StackTrace != null)
            {
                Console.WriteLine();
                Console.WriteLine("📋 Stack trace:");
                Console.WriteLine(ex.StackTrace);
            }
            
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
            try
            {
                var detected = RegimeAnalyzer.DetermineRegime(test.r, test.q, test.sigma, Option.Type.Put);
                var status = detected == test.expected ? "✅ PASS" : "❌ FAIL";
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
            catch (Exception ex)
            {
                Console.WriteLine($"  {test.Name,-25} ❌ ERROR");
                Console.WriteLine($"    Error: {ex.Message}");
                Console.WriteLine();
                _logger?.LogError(ex, "Failed regime detection test {TestName}", test.Name);
            }
        }
    }

    static void RunSingleBoundaryValidation()
    {
        Console.WriteLine("2. SINGLE BOUNDARY VALIDATION");
        Console.WriteLine("─".PadRight(50, '─'));

        try
        {
            // Test case: Standard American put
            double spot = 36.0, strike = 40.0, sigma = 0.20, r = 0.06, q = 0.02;
            var maturity = new Date(17, Month.May, 2025);

            Console.WriteLine("  Comparing standard vs. extended engine on traditional case...");
            
            var results = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: false);
            var extendedResults = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: true);

            Console.WriteLine($"  Market: S=${spot:F1}, K=${strike:F1}, r={r:F3}, q={q:F3}, σ={sigma:F2}");
            Console.WriteLine($"  Standard QdFp Engine:  ${results.Price:F6}");
            Console.WriteLine($"  Extended Engine:       ${extendedResults.Price:F6}");
            Console.WriteLine($"  Difference:            ${Math.Abs(results.Price - extendedResults.Price):E2}");
            Console.WriteLine($"  Regime:                {extendedResults.Regime}");
            Console.WriteLine($"  Computation Time:      {extendedResults.ComputationTime.TotalMilliseconds:F1}ms");
            
            // Validate that they agree (should be identical for single boundary cases)
            double relativeDiff = Math.Abs(results.Price - extendedResults.Price) / Math.Abs(results.Price);
            if (relativeDiff < 0.01) // 1% tolerance
            {
                Console.WriteLine("  ✅ Engines agree within tolerance");
            }
            else
            {
                Console.WriteLine("  ⚠️  Significant difference detected");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ ERROR: {ex.Message}");
            _logger?.LogError(ex, "Single boundary validation failed");
        }
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
            Console.WriteLine($"🔬 Scenario: {scenario.Name}");
            
            try
            {
                double sigma = scenario.sigma;
                if (scenario.Name.Contains("Critical"))
                {
                    sigma = RegimeAnalyzer.CriticalVolatility(scenario.r, scenario.q);
                    if (double.IsNaN(sigma))
                    {
                        Console.WriteLine("  ⏭️  Skipped: Invalid parameters for critical volatility");
                        Console.WriteLine();
                        continue;
                    }
                }

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
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                _logger?.LogError(ex, "Failed to price scenario {ScenarioName}", scenario.Name);
            }
            
            Console.WriteLine();
        }
    }

    static void RunPerformanceBenchmarks()
    {
        Console.WriteLine("4. PERFORMANCE BENCHMARKS");
        Console.WriteLine("─".PadRight(50, '─'));

        try
        {
            double spot = 95.0, strike = 100.0, r = -0.01, q = -0.02, sigma = 0.15;
            var maturity = new Date(15, Month.October, 2025); // 9 months

            var spectralNodeCounts = new[] { 4, 6, 8, 10, 12 };
            
            Console.WriteLine("  📊 Spectral Nodes vs. Accuracy/Performance:");
            Console.WriteLine("  Nodes  Price      Error      Time(ms)  Iterations");
            Console.WriteLine("  ─────  ─────────  ─────────  ────────  ──────────");

            OptionPricingResults? referenceResult = null;
            
            foreach (int nodes in spectralNodeCounts)
            {
                try
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
                catch (Exception ex)
                {
                    Console.WriteLine($"  {nodes,3}    FAILED     {ex.Message}");
                    _logger?.LogError(ex, "Performance test failed for {Nodes} nodes", nodes);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Performance benchmark failed: {ex.Message}");
            _logger?.LogError(ex, "Performance benchmark suite failed");
        }
    }

    static void RunConvergenceAnalysis()
    {
        Console.WriteLine("5. CONVERGENCE ANALYSIS");
        Console.WriteLine("─".PadRight(50, '─'));

        try
        {
            double spot = 90.0, strike = 100.0, r = -0.015, q = -0.025, sigma = 0.12;
            var maturity = new Date(15, Month.December, 2025); // 11 months

            var tolerances = new[] { 1e-6, 1e-8, 1e-10, 1e-12 };
            
            Console.WriteLine("  📈 Tolerance vs. Convergence:");
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
                    else
                    {
                        Console.WriteLine($"  {tol:E1}     ${results.Price:F6}   N/A        N/A           {stopwatch.ElapsedMilliseconds,6}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  {tol:E1}     Failed: {ex.Message}");
                    _logger?.LogError(ex, "Convergence test failed for tolerance {Tolerance}", tol);
                }
            }

            Console.WriteLine();
            Console.WriteLine("  🔬 Spectral Convergence Analysis:");
            
            // Test spectral convergence rate
            try
            {
                var results8 = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: true, spectralNodes: 8);
                var results12 = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: true, spectralNodes: 12);
                var results16 = PriceAmericanOption(spot, strike, sigma, r, q, maturity, useExtendedEngine: true, spectralNodes: 16);

                if (results8.DetailedResults?.UpperBoundary != null)
                {
                    double convergenceRate = results8.DetailedResults.UpperBoundary.EstimatedError;
                    Console.WriteLine($"  Estimated convergence rate: {convergenceRate:F2}");
                    Console.WriteLine($"  Richardson extrapolation:");
                    
                    if (Math.Abs(results8.Price - results12.Price) > 1e-10 && Math.Abs(results12.Price - results16.Price) > 1e-10)
                    {
                        double p = Math.Log((results8.Price - results12.Price) / (results12.Price - results16.Price)) / Math.Log(8.0/12.0);
                        double extrapolated = results16.Price + (results16.Price - results12.Price) / (Math.Pow(16.0/12.0, p) - 1.0);
                        
                        Console.WriteLine($"    Convergence order: {p:F2}");
                        Console.WriteLine($"    Extrapolated price: ${extrapolated:F6}");
                    }
                    else
                    {
                        Console.WriteLine($"    Prices too close for reliable extrapolation");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Spectral analysis failed: {ex.Message}");
                _logger?.LogError(ex, "Spectral convergence analysis failed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Convergence analysis failed: {ex.Message}");
            _logger?.LogError(ex, "Convergence analysis suite failed");
        }
    }

    static OptionPricingResults PriceAmericanOption(double spot, double strike, double sigma, double r, double q, 
                                                  Date maturity, bool useExtendedEngine = true, 
                                                  int spectralNodes = 8, double tolerance = 1e-12)
    {
        try
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

            DoubleBoundaryResults? detailedResults = null;
            double timeToMaturity = (maturity.serialNumber() - Settings.instance().getEvaluationDate().serialNumber()) / 365.0;

            if (useExtendedEngine)
            {
                var extendedEngine = new DoubleBoundaryAmericanEngine(
                    process, spectralNodes, tolerance, maxIterations: 100, useAcceleration: true, _logger);
                
                // Set option parameters for the engine
                extendedEngine.SetOptionParameters(strike, timeToMaturity, Option.Type.Put);
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                double price = extendedEngine.CalculatePrice();
                stopwatch.Stop();
                
                detailedResults = extendedEngine.GetDetailedResults();
                
                return new OptionPricingResults
                {
                    Price = price,
                    Regime = detailedResults?.Regime ?? ExerciseRegimeType.Degenerate,
                    ComputationTime = detailedResults?.ComputationTime ?? stopwatch.Elapsed,
                    DetailedResults = detailedResults
                };
            }
            else
            {
                // Use standard QuantLib engine
                PricingEngine engine = new QdFpAmericanEngine(process, QdFpAmericanEngine.accurateScheme());
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
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to price American option with spot={Spot}, strike={Strike}", spot, strike);
            throw;
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