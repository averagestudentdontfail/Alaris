using Alaris.Quantlib;
using Microsoft.Extensions.Logging;

namespace Alaris.Double;

/// <summary>
/// QuantLib-focused utilities - replaces custom utility functions
/// Leverages QuantLib infrastructure instead of reimplementing mathematical functions
/// </summary>
public static class QuantLibUtilities
{
    /// <summary>
    /// Extract complete market parameters from QuantLib process
    /// Replaces manual parameter extraction throughout the codebase
    /// </summary>
    public static MarketParameters ExtractMarketParameters(
        GeneralizedBlackScholesProcess process, 
        double strike, 
        double timeToMaturity, 
        Option.Type optionType)
    {
        return new MarketParameters
        {
            Spot = process.x0(),
            Strike = strike,
            R = SimplifiedQuantLibHelper.ExtractRate(process.riskFreeRate().currentLink(), timeToMaturity),
            Q = SimplifiedQuantLibHelper.ExtractRate(process.dividendYield().currentLink(), timeToMaturity),
            Sigma = SimplifiedQuantLibHelper.ExtractVolatility(process.blackVolatility().currentLink(), timeToMaturity, strike),
            TimeToMaturity = timeToMaturity,
            OptionType = optionType
        };
    }

    /// <summary>
    /// Create QuantLib process from parameters - useful for testing
    /// </summary>
    public static GeneralizedBlackScholesProcess CreateProcess(MarketParameters parameters)
    {
        var today = Settings.instance().getEvaluationDate();
        
        var underlying = new SimpleQuote(parameters.Spot);
        var riskFreeRate = new FlatForward(today, parameters.R, new Actual365Fixed());
        var dividendYield = new FlatForward(today, parameters.Q, new Actual365Fixed());
        var volatility = new BlackConstantVol(today, new TARGET(), parameters.Sigma, new Actual365Fixed());

        return new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendYield),
            new YieldTermStructureHandle(riskFreeRate),
            new BlackVolTermStructureHandle(volatility)
        );
    }

    /// <summary>
    /// Calculate European baseline using QuantLib - eliminates custom implementations
    /// </summary>
    public static double CalculateEuropeanBaseline(MarketParameters parameters)
    {
        try
        {
            var process = CreateProcess(parameters);
            var today = Settings.instance().getEvaluationDate();
            var maturity = new Date(today.serialNumber() + (uint)(parameters.TimeToMaturity * 365));
            
            var exercise = new EuropeanExercise(maturity);
            var payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
            var option = new VanillaOption(payoff, exercise);
            
            var engine = new AnalyticEuropeanEngine(process);
            option.setPricingEngine(engine);
            
            return option.NPV();
        }
        catch
        {
            // Fallback to manual calculation
            return CalculateBlackScholes(parameters);
        }
    }

    /// <summary>
    /// Manual Black-Scholes calculation as absolute fallback
    /// </summary>
    private static double CalculateBlackScholes(MarketParameters mp)
    {
        if (mp.TimeToMaturity <= 0) 
            return Math.Max(mp.OptionType == Option.Type.Call ? mp.Spot - mp.Strike : mp.Strike - mp.Spot, 0.0);

        double d1 = (Math.Log(mp.Spot / mp.Strike) + (mp.R - mp.Q + 0.5 * mp.Sigma * mp.Sigma) * mp.TimeToMaturity) 
                   / (mp.Sigma * Math.Sqrt(mp.TimeToMaturity));
        double d2 = d1 - mp.Sigma * Math.Sqrt(mp.TimeToMaturity);

        double nd1 = SimplifiedQuantLibHelper.NormalCdf(d1);
        double nd2 = SimplifiedQuantLibHelper.NormalCdf(d2);
        double nmd1 = SimplifiedQuantLibHelper.NormalCdf(-d1);
        double nmd2 = SimplifiedQuantLibHelper.NormalCdf(-d2);

        if (mp.OptionType == Option.Type.Call)
        {
            return mp.Spot * Math.Exp(-mp.Q * mp.TimeToMaturity) * nd1 - 
                   mp.Strike * Math.Exp(-mp.R * mp.TimeToMaturity) * nd2;
        }
        else
        {
            return mp.Strike * Math.Exp(-mp.R * mp.TimeToMaturity) * nmd2 - 
                   mp.Spot * Math.Exp(-mp.Q * mp.TimeToMaturity) * nmd1;
        }
    }

    /// <summary>
    /// Performance profiling utilities
    /// </summary>
    public static class Performance
    {
        public static T MeasureTime<T>(Func<T> operation, out TimeSpan elapsed, ILogger? logger = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = operation();
                stopwatch.Stop();
                elapsed = stopwatch.Elapsed;
                logger?.LogDebug("Operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
                return result;
            }
            catch
            {
                stopwatch.Stop();
                elapsed = stopwatch.Elapsed;
                throw;
            }
        }

        public static void ProfileEnginePerformance(
            OptimizedDoubleBoundaryEngine engine, 
            MarketParameters parameters, 
            ILogger? logger = null)
        {
            var iterations = new[] { 5, 10, 25 };
            
            foreach (int iter in iterations)
            {
                var times = new List<double>();
                
                for (int i = 0; i < iter; i++)
                {
                    var result = MeasureTime(() => engine.PriceAmericanOption(
                        parameters.Strike, parameters.TimeToMaturity, parameters.OptionType), 
                        out var elapsed);
                    times.Add(elapsed.TotalMilliseconds);
                }
                
                double avgTime = times.Average();
                double minTime = times.Min();
                double maxTime = times.Max();
                
                logger?.LogInformation("Performance ({Iterations} runs): Avg={AvgTime:F1}ms, Min={MinTime:F1}ms, Max={MaxTime:F1}ms", 
                                      iter, avgTime, minTime, maxTime);
            }
        }
    }

    /// <summary>
    /// Error handling utilities focused on QuantLib integration
    /// </summary>
    public static class ErrorHandling
    {
        public static T SafeQuantLibOperation<T>(Func<T> operation, T fallbackValue, ILogger? logger = null)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "QuantLib operation failed, using fallback value");
                return fallbackValue;
            }
        }

        public static bool TryQuantLibOperation<T>(Func<T> operation, out T result, ILogger? logger = null)
        {
            try
            {
                result = operation();
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "QuantLib operation failed");
                result = default(T)!;
                return false;
            }
        }
    }

    /// <summary>
    /// Engine factory utilities - simplified compared to complex factory patterns
    /// </summary>
    public static IPricingEngine CreateOptimalEngine(
        GeneralizedBlackScholesProcess process,
        MarketParameters parameters,
        ILogger? logger = null)
    {
        var regime = OptimizedRegimeAnalyzer.DetermineRegime(
            parameters.R, parameters.Q, parameters.Sigma, parameters.OptionType);

        logger?.LogDebug("Selected {Regime} for r={R:F4}, q={Q:F4}, σ={Sigma:F4}", 
                        regime, parameters.R, parameters.Q, parameters.Sigma);

        return regime switch
        {
            ExerciseRegimeType.NoEarlyExercise => new AnalyticEuropeanEngine(process),
            ExerciseRegimeType.DoubleBoundaryNegativeRates => new DoubleBoundaryEngineWrapper(process, logger),
            _ => new QdFpAmericanEngine(process, QdFpAmericanEngine.accurateScheme())
        };
    }

    /// <summary>
    /// Logging utilities for detailed analysis
    /// </summary>
    public static void LogMarketParameters(MarketParameters parameters, ILogger logger)
    {
        logger.LogInformation("Market Parameters: S={Spot:F2}, K={Strike:F2}, r={R:F4}, q={Q:F4}, σ={Sigma:F4}, T={T:F4}",
                             parameters.Spot, parameters.Strike, parameters.R, parameters.Q, parameters.Sigma, parameters.TimeToMaturity);
        
        var regime = OptimizedRegimeAnalyzer.DetermineRegime(parameters.R, parameters.Q, parameters.Sigma, parameters.OptionType);
        logger.LogInformation("Detected regime: {Regime}", regime);
        
        if (regime == ExerciseRegimeType.DoubleBoundaryNegativeRates)
        {
            var criticalVol = OptimizedRegimeAnalyzer.CalculateCriticalVolatility(parameters.R, parameters.Q);
            logger.LogInformation("Critical volatility: {CriticalVol:F4}, Current: {CurrentVol:F4}", criticalVol, parameters.Sigma);
        }
    }
}