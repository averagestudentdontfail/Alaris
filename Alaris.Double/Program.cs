using Alaris.Quantlib;
using Microsoft.Extensions.Logging;

namespace Alaris.Double;

/// <summary>
/// Simplified factory that automatically selects the optimal pricing engine
/// Eliminates redundant engine code by maximally leveraging QuantLib
/// </summary>
public static class SimplifiedEngineFactory
{
    /// <summary>
    /// Creates the optimal pricing engine based on market conditions
    /// Automatically falls back to QuantLib engines when appropriate
    /// </summary>
    public static IPricingEngine CreateOptimalEngine(
        GeneralizedBlackScholesProcess process,
        double strike,
        double timeToMaturity, 
        Option.Type optionType,
        ILogger? logger = null)
    {
        // Extract market parameters using QuantLib's infrastructure
        var marketParams = ExtractMarketParameters(process, timeToMaturity);
        
        // Use optimized regime detection
        var regime = OptimizedRegimeAnalyzer.DetermineRegime(
            marketParams.r, marketParams.q, marketParams.sigma, optionType);
        
        logger?.LogDebug("Selected regime {Regime} for r={R:F4}, q={Q:F4}, σ={Sigma:F4}", 
                        regime, marketParams.r, marketParams.q, marketParams.sigma);

        // Return appropriate QuantLib engine for most cases
        return regime switch
        {
            ExerciseRegimeType.NoEarlyExercise => 
                new AnalyticEuropeanEngine(process),
                
            ExerciseRegimeType.SingleBoundaryPositive or 
            ExerciseRegimeType.SingleBoundaryNegativeDividend => 
                new QdFpAmericanEngine(process, QdFpAmericanEngine.accurateScheme()),
                
            ExerciseRegimeType.DoubleBoundaryNegativeRates => 
                new DoubleBoundaryEngineWrapper(process, logger),
                
            _ => new QdFpAmericanEngine(process, QdFpAmericanEngine.fastScheme())
        };
    }

    /// <summary>
    /// Extracts market parameters using QuantLib's term structure methods
    /// Avoids redundant parameter extraction code
    /// </summary>
    private static (double r, double q, double sigma) ExtractMarketParameters(
        GeneralizedBlackScholesProcess process, double timeToMaturity)
    {
        var r = process.riskFreeRate().currentLink()
            .forwardRate(0.0, timeToMaturity, Compounding.Continuous).rate();
        var q = process.dividendYield().currentLink()
            .forwardRate(0.0, timeToMaturity, Compounding.Continuous).rate();
        var sigma = process.blackVolatility().currentLink()
            .blackVol(timeToMaturity, process.x0());

        return (r, q, sigma);
    }
}

/// <summary>
/// Wrapper for double boundary engine that implements QuantLib's PricingEngine interface
/// Minimizes custom code by delegating to QuantLib where possible
/// </summary>
public class DoubleBoundaryEngineWrapper : PricingEngine
{
    private readonly GeneralizedBlackScholesProcess _process;
    private readonly OptimizedDoubleBoundaryEngine _engine;
    private readonly ILogger? _logger;

    public DoubleBoundaryEngineWrapper(GeneralizedBlackScholesProcess process, ILogger? logger = null)
    {
        _process = process;
        _engine = new OptimizedDoubleBoundaryEngine(process, logger: logger);
        _logger = logger;
    }

    /// <summary>
    /// Calculate option price - integrates with QuantLib's pricing framework
    /// </summary>
    public override void calculate()
    {
        try
        {
            // Extract option parameters from QuantLib option object
            var option = arguments_ as VanillaOption.Arguments;
            if (option?.payoff is PlainVanillaPayoff payoff && option.exercise is AmericanExercise exercise)
            {
                var strike = payoff.strike();
                var optionType = payoff.optionType();
                var maturity = exercise.lastDate();
                var today = Settings.instance().getEvaluationDate();
                var timeToMaturity = (maturity.serialNumber() - today.serialNumber()) / 365.0;

                // Use optimized engine
                var price = _engine.PriceAmericanOption(strike, timeToMaturity, optionType);
                
                // Set result in QuantLib format
                var results = results_ as VanillaOption.Results;
                if (results != null)
                {
                    results.value = price;
                }

                _logger?.LogDebug("Double boundary option priced: {Price:F6}", price);
            }
            else
            {
                throw new ArgumentException("Unsupported option type for double boundary engine");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Double boundary pricing failed");
            throw;
        }
    }

    // Required QuantLib interface methods
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cleanup if needed
        }
        base.Dispose(disposing);
    }
}