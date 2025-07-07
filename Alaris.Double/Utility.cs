using Alaris.Quantlib;
using Microsoft.Extensions.Logging;

namespace Alaris.Quantlib.Double;

/// <summary>
/// Extension methods and utility functions for the Alaris Double Boundary engine
/// Provides convenience methods for common operations and parameter validation
/// </summary>
public static class UtilityExtensions
{
    #region VanillaOption Extensions

    /// <summary>
    /// Prices an American option using the appropriate engine based on market conditions
    /// Automatically selects between standard and double boundary engines
    /// </summary>
    /// <param name="option">The American option to price</param>
    /// <param name="process">Market process</param>
    /// <param name="spectralNodes">Number of spectral nodes for double boundary cases</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Option price and detailed results</returns>
    public static (double price, DoubleBoundaryResults? details) PriceWithOptimalEngine(
        this VanillaOption option, 
        GeneralizedBlackScholesProcess process,
        int spectralNodes = Constants.DEFAULT_SPECTRAL_NODES,
        ILogger? logger = null)
    {
        var arguments = option.arguments_ as VanillaOption.Arguments;
        if (arguments == null)
            throw new ArgumentException("Option must have valid arguments");

        // Extract market parameters
        var marketParams = ExtractMarketParameters(process, arguments);
        
        // Determine optimal regime
        var regime = RegimeAnalyzer.DetermineRegime(
            marketParams.r, marketParams.q, marketParams.sigma, arguments.payoff.optionType());

        logger?.LogDebug("Selected regime {Regime} for r={R:F4}, q={Q:F4}, σ={Sigma:F4}", 
                        regime, marketParams.r, marketParams.q, marketParams.sigma);

        PricingEngine engine = regime switch
        {
            ExerciseRegimeType.DoubleBoundaryNegativeRates => 
                new DoubleBoundaryAmericanEngine(process, spectralNodes, logger: logger),
            ExerciseRegimeType.NoEarlyExercise => 
                CreateEuropeanEngine(process),
            _ => new QdFpAmericanEngine(process, QdFpAmericanEngine.accurateScheme())
        };

        option.setPricingEngine(engine);
        double price = option.NPV();

        // Get detailed results if available
        DoubleBoundaryResults? details = null;
        if (engine is DoubleBoundaryAmericanEngine doubleEngine)
        {
            details = doubleEngine.GetDetailedResults();
        }

        return (price, details);
    }

    /// <summary>
    /// Validates that option parameters are reasonable for pricing
    /// </summary>
    /// <param name="option">Option to validate</param>
    /// <param name="process">Market process</param>
    /// <returns>True if parameters are valid</returns>
    public static bool ValidateParameters(this VanillaOption option, GeneralizedBlackScholesProcess process)
    {
        try
        {
            var arguments = option.arguments_ as VanillaOption.Arguments;
            if (arguments == null) return false;

            var marketParams = ExtractMarketParameters(process, arguments);
            
            return marketParams.tau >= Constants.MIN_TIME_TO_MATURITY &&
                   marketParams.tau <= Constants.MAX_TIME_TO_MATURITY &&
                   marketParams.sigma >= Constants.MIN_VOLATILITY &&
                   marketParams.sigma <= Constants.MAX_VOLATILITY &&
                   marketParams.r >= Constants.MIN_INTEREST_RATE &&
                   marketParams.r <= Constants.MAX_INTEREST_RATE &&
                   marketParams.spot > 0 &&
                   marketParams.strike > 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Market Parameter Utilities

    /// <summary>
    /// Creates a complete market environment for American option pricing
    /// </summary>
    /// <param name="spot">Current asset price</param>
    /// <param name="strike">Strike price</param>
    /// <param name="timeToMaturity">Time to maturity in years</param>
    /// <param name="volatility">Volatility (annualized)</param>
    /// <param name="riskFreeRate">Risk-free interest rate</param>
    /// <param name="dividendYield">Dividend yield</param>
    /// <returns>Configured Black-Scholes process</returns>
    public static GeneralizedBlackScholesProcess CreateMarketEnvironment(
        double spot, double strike, double timeToMaturity,
        double volatility, double riskFreeRate, double dividendYield)
    {
        var today = Settings.instance().getEvaluationDate();
        
        var underlying = new SimpleQuote(spot);
        var dividendYieldCurve = new FlatForward(today, dividendYield, new Actual365Fixed());
        var volatilitySurface = new BlackConstantVol(today, new TARGET(), volatility, new Actual365Fixed());
        var riskFreeRateCurve = new FlatForward(today, riskFreeRate, new Actual365Fixed());

        return new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendYieldCurve),
            new YieldTermStructureHandle(riskFreeRateCurve),
            new BlackVolTermStructureHandle(volatilitySurface)
        );
    }

    /// <summary>
    /// Creates an American option with specified parameters
    /// </summary>
    /// <param name="optionType">Put or Call</param>
    /// <param name="strike">Strike price</param>
    /// <param name="maturityDate">Maturity date</param>
    /// <returns>Configured American option</returns>
    public static VanillaOption CreateAmericanOption(Option.Type optionType, double strike, Date maturityDate)
    {
        var exercise = new AmericanExercise(Settings.instance().getEvaluationDate(), maturityDate);
        var payoff = new PlainVanillaPayoff(optionType, strike);
        return new VanillaOption(payoff, exercise);
    }

    /// <summary>
    /// Analyzes the exercise regime for given market parameters
    /// </summary>
    /// <param name="r">Risk-free rate</param>
    /// <param name="q">Dividend yield</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="optionType">Option type</param>
    /// <returns>Detailed regime analysis</returns>
    public static RegimeAnalysis AnalyzeExerciseRegime(double r, double q, double sigma, Option.Type optionType)
    {
        var regime = RegimeAnalyzer.DetermineRegime(r, q, sigma, optionType);
        var criticalVol = RegimeAnalyzer.CriticalVolatility(r, q);
        
        return new RegimeAnalysis
        {
            Regime = regime,
            CriticalVolatility = criticalVol,
            IsDoubleBoundary = regime == ExerciseRegimeType.DoubleBoundaryNegativeRates,
            RecommendedEngine = GetRecommendedEngine(regime),
            ParameterConstraints = GetParameterConstraints(regime),
            EstimatedComplexity = GetComplexityEstimate(regime)
        };
    }

    #endregion

    #region Convergence and Performance Utilities

    /// <summary>
    /// Performs convergence analysis across different spectral node counts
    /// </summary>
    /// <param name="option">Option to analyze</param>
    /// <param name="process">Market process</param>
    /// <param name="nodeRange">Range of spectral nodes to test</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Convergence analysis results</returns>
    public static ConvergenceAnalysis AnalyzeConvergence(
        VanillaOption option, 
        GeneralizedBlackScholesProcess process,
        IEnumerable<int> nodeRange,
        ILogger? logger = null)
    {
        var results = new List<ConvergencePoint>();
        
        foreach (int nodes in nodeRange.OrderBy(n => n))
        {
            try
            {
                var engine = new DoubleBoundaryAmericanEngine(process, nodes, logger: logger);
                option.setPricingEngine(engine);
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                double price = option.NPV();
                stopwatch.Stop();
                
                var details = engine.GetDetailedResults();
                
                results.Add(new ConvergencePoint
                {
                    SpectralNodes = nodes,
                    Price = price,
                    ComputationTime = stopwatch.Elapsed,
                    Iterations = details?.IterationsConverged ?? 0,
                    FinalError = details?.FinalError ?? double.NaN,
                    EstimatedError = details?.UpperBoundary?.EstimatedError ?? double.NaN
                });
                
                logger?.LogDebug("Convergence point: {Nodes} nodes → {Price:F6} in {Time:F1}ms", 
                                nodes, price, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed convergence test for {Nodes} nodes", nodes);
            }
        }
        
        return new ConvergenceAnalysis
        {
            Points = results,
            EstimatedConvergenceRate = EstimateConvergenceRate(results),
            RecommendedNodes = GetOptimalNodeCount(results),
            RichardsonExtrapolation = ComputeRichardsonExtrapolation(results)
        };
    }

    /// <summary>
    /// Benchmarks engine performance across different parameter sets
    /// </summary>
    /// <param name="testCases">Test cases to benchmark</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Performance benchmark results</returns>
    public static PerformanceBenchmark BenchmarkPerformance(
        IEnumerable<BenchmarkCase> testCases,
        ILogger? logger = null)
    {
        var results = new List<BenchmarkResult>();
        
        foreach (var testCase in testCases)
        {
            try
            {
                var process = CreateMarketEnvironment(
                    testCase.Spot, testCase.Strike, testCase.TimeToMaturity,
                    testCase.Volatility, testCase.RiskFreeRate, testCase.DividendYield);
                
                var option = CreateAmericanOption(testCase.OptionType, testCase.Strike,
                    Settings.instance().getEvaluationDate().add((int)(testCase.TimeToMaturity * 365)));
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var (price, details) = option.PriceWithOptimalEngine(process, logger: logger);
                stopwatch.Stop();
                
                results.Add(new BenchmarkResult
                {
                    TestCase = testCase,
                    Price = price,
                    ComputationTime = stopwatch.Elapsed,
                    Regime = details?.Regime ?? ExerciseRegimeType.SingleBoundaryPositive,
                    Success = true
                });
                
                logger?.LogDebug("Benchmark {Name}: {Price:F6} in {Time:F1}ms", 
                                testCase.Name, price, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                results.Add(new BenchmarkResult
                {
                    TestCase = testCase,
                    Success = false,
                    ErrorMessage = ex.Message
                });
                
                logger?.LogWarning(ex, "Benchmark {Name} failed", testCase.Name);
            }
        }
        
        return new PerformanceBenchmark
        {
            Results = results,
            AverageTime = results.Where(r => r.Success).Average(r => r.ComputationTime.TotalMilliseconds),
            SuccessRate = (double)results.Count(r => r.Success) / results.Count
        };
    }

    #endregion

    #region Private Helper Methods

    private static (double spot, double strike, double tau, double r, double q, double sigma) 
        ExtractMarketParameters(GeneralizedBlackScholesProcess process, VanillaOption.Arguments arguments)
    {
        var today = Settings.instance().getEvaluationDate();
        var maturity = arguments.exercise.lastDate();
        double tau = Math.Max((maturity.serialNumber() - today.serialNumber()) / 365.0, Constants.MIN_TIME_TO_MATURITY);
        
        double r = process.riskFreeRate().link.zeroRate(tau, Compounding.Continuous).value();
        double q = process.dividendYield().link.zeroRate(tau, Compounding.Continuous).value();
        double sigma = process.blackVolatility().link.blackVol(tau, process.x0()).value();
        double spot = process.x0();
        double strike = arguments.payoff.strike();
        
        return (spot, strike, tau, r, q, sigma);
    }

    private static PricingEngine CreateEuropeanEngine(GeneralizedBlackScholesProcess process)
    {
        return new AnalyticEuropeanEngine(process);
    }

    private static string GetRecommendedEngine(ExerciseRegimeType regime)
    {
        return regime switch
        {
            ExerciseRegimeType.DoubleBoundaryNegativeRates => "DoubleBoundaryAmericanEngine",
            ExerciseRegimeType.NoEarlyExercise => "AnalyticEuropeanEngine",
            _ => "QdFpAmericanEngine"
        };
    }

    private static string GetParameterConstraints(ExerciseRegimeType regime)
    {
        return regime switch
        {
            ExerciseRegimeType.SingleBoundaryPositive => "r ≥ q ≥ 0",
            ExerciseRegimeType.SingleBoundaryNegativeDividend => "r ≥ 0 > q",
            ExerciseRegimeType.DoubleBoundaryNegativeRates => "q < r < 0, σ ≤ σ*",
            ExerciseRegimeType.NoEarlyExercise => "Various conditions where early exercise is never optimal",
            _ => "Unknown constraints"
        };
    }

    private static string GetComplexityEstimate(ExerciseRegimeType regime)
    {
        return regime switch
        {
            ExerciseRegimeType.DoubleBoundaryNegativeRates => "High (spectral iteration)",
            ExerciseRegimeType.SingleBoundaryPositive => "Medium (single boundary)",
            ExerciseRegimeType.NoEarlyExercise => "Low (analytical)",
            _ => "Variable"
        };
    }

    private static double EstimateConvergenceRate(List<ConvergencePoint> points)
    {
        if (points.Count < 3) return double.NaN;
        
        // Simple exponential fit to price differences
        var sortedPoints = points.OrderBy(p => p.SpectralNodes).ToList();
        var priceDiffs = new List<double>();
        
        for (int i = 1; i < sortedPoints.Count; i++)
        {
            double diff = Math.Abs(sortedPoints[i].Price - sortedPoints[i-1].Price);
            if (diff > 0) priceDiffs.Add(diff);
        }
        
        if (priceDiffs.Count < 2) return double.NaN;
        
        // Estimate exponential decay rate
        double avgRatio = 0.0;
        for (int i = 1; i < priceDiffs.Count; i++)
        {
            if (priceDiffs[i-1] > 0)
                avgRatio += priceDiffs[i] / priceDiffs[i-1];
        }
        
        return avgRatio / (priceDiffs.Count - 1);
    }

    private static int GetOptimalNodeCount(List<ConvergencePoint> points)
    {
        // Balance accuracy vs. computation time
        var validPoints = points.Where(p => !double.IsNaN(p.Price) && p.ComputationTime.TotalMilliseconds > 0).ToList();
        if (!validPoints.Any()) return Constants.DEFAULT_SPECTRAL_NODES;
        
        // Find point with best accuracy/time ratio
        double bestRatio = 0.0;
        int bestNodes = Constants.DEFAULT_SPECTRAL_NODES;
        
        foreach (var point in validPoints)
        {
            double accuracy = 1.0 / Math.Max(point.FinalError, 1e-12);
            double time = point.ComputationTime.TotalMilliseconds;
            double ratio = accuracy / time;
            
            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                bestNodes = point.SpectralNodes;
            }
        }
        
        return bestNodes;
    }

    private static double ComputeRichardsonExtrapolation(List<ConvergencePoint> points)
    {
        if (points.Count < 3) return double.NaN;
        
        var sortedPoints = points.OrderByDescending(p => p.SpectralNodes).Take(3).ToList();
        if (sortedPoints.Count < 3) return double.NaN;
        
        // Richardson extrapolation assuming geometric convergence
        double p1 = sortedPoints[0].Price;
        double p2 = sortedPoints[1].Price;
        double p3 = sortedPoints[2].Price;
        
        double r = (p1 - p2) / (p2 - p3);
        if (Math.Abs(r - 1.0) < 1e-10) return p1;
        
        return p1 + (p1 - p2) / (r - 1.0);
    }

    #endregion
}

#region Supporting Data Structures

/// <summary>
/// Analysis of exercise regime characteristics
/// </summary>
public class RegimeAnalysis
{
    public ExerciseRegimeType Regime { get; set; }
    public double CriticalVolatility { get; set; } = double.NaN;
    public bool IsDoubleBoundary { get; set; }
    public string RecommendedEngine { get; set; } = string.Empty;
    public string ParameterConstraints { get; set; } = string.Empty;
    public string EstimatedComplexity { get; set; } = string.Empty;
}

/// <summary>
/// Single point in convergence analysis
/// </summary>
public class ConvergencePoint
{
    public int SpectralNodes { get; set; }
    public double Price { get; set; }
    public TimeSpan ComputationTime { get; set; }
    public int Iterations { get; set; }
    public double FinalError { get; set; }
    public double EstimatedError { get; set; }
}

/// <summary>
/// Complete convergence analysis results
/// </summary>
public class ConvergenceAnalysis
{
    public List<ConvergencePoint> Points { get; set; } = new();
    public double EstimatedConvergenceRate { get; set; }
    public int RecommendedNodes { get; set; }
    public double RichardsonExtrapolation { get; set; }
}

/// <summary>
/// Test case for benchmarking
/// </summary>
public class BenchmarkCase
{
    public string Name { get; set; } = string.Empty;
    public double Spot { get; set; }
    public double Strike { get; set; }
    public double TimeToMaturity { get; set; }
    public double Volatility { get; set; }
    public double RiskFreeRate { get; set; }
    public double DividendYield { get; set; }
    public Option.Type OptionType { get; set; }
}

/// <summary>
/// Result from a single benchmark test
/// </summary>
public class BenchmarkResult
{
    public BenchmarkCase TestCase { get; set; } = new();
    public double Price { get; set; }
    public TimeSpan ComputationTime { get; set; }
    public ExerciseRegimeType Regime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Complete performance benchmark results
/// </summary>
public class PerformanceBenchmark
{
    public List<BenchmarkResult> Results { get; set; } = new();
    public double AverageTime { get; set; }
    public double SuccessRate { get; set; }
}

#endregion