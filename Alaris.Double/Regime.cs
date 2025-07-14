using Alaris.Quantlib;

namespace Alaris.Double;

/// <summary>
/// Optimized regime analyzer that leverages QuantLib's mathematical infrastructure
/// and fixes the regime detection logic
/// </summary>
public static class OptimizedRegimeAnalyzer
{
    private const double EPSILON = 1e-12;

    /// <summary>
    /// Determines the exercise regime with corrected logic for negative rates
    /// </summary>
    public static ExerciseRegimeType DetermineRegime(double r, double q, double sigma, Option.Type optionType)
    {
        // For American calls, use put-call symmetry relation
        if (optionType == Option.Type.Call)
        {
            // Transform call parameters using McDonald-Schroder symmetry
            return DetermineRegime(-q, -r, sigma, Option.Type.Put);
        }

        // American Put analysis (corrected logic)
        
        // Case 1: Traditional positive regime
        if (r >= 0.0 && q >= 0.0)
        {
            return ExerciseRegimeType.SingleBoundaryPositive;
        }
        
        // Case 2: Negative dividend yield with positive interest rate
        if (r >= 0.0 && q < 0.0)
        {
            return ExerciseRegimeType.SingleBoundaryNegativeDividend;
        }
        
        // Case 3: Both rates negative - need detailed analysis
        if (r < 0.0 && q < 0.0)
        {
            // Sub-case: r ≤ q < 0 (never optimal to exercise)
            if (r <= q + EPSILON)
            {
                return ExerciseRegimeType.NoEarlyExercise;
            }
            
            // Sub-case: q < r < 0 (potential double boundary)
            double criticalVol = CalculateCriticalVolatility(r, q);
            
            // If volatility is below critical threshold, double boundary exists
            if (sigma <= criticalVol + EPSILON)
            {
                return ExerciseRegimeType.DoubleBoundaryNegativeRates;
            }
            else
            {
                return ExerciseRegimeType.NoEarlyExercise;
            }
        }
        
        // Case 4: Mixed signs (r < 0, q ≥ 0)
        if (r < 0.0 && q >= 0.0)
        {
            return ExerciseRegimeType.NoEarlyExercise;
        }
        
        // Default fallback
        return ExerciseRegimeType.SingleBoundaryPositive;
    }

    /// <summary>
    /// Calculate critical volatility using QuantLib's mathematical functions
    /// Leverages existing inverse normal and optimization capabilities
    /// </summary>
    public static double CalculateCriticalVolatility(double r, double q)
    {
        // Only meaningful for q < r < 0
        if (!(r < 0.0 && q < 0.0 && q < r))
        {
            return double.NaN;
        }

        // Use the corrected mathematical formula from literature
        // σ* = |√(-2r) - √(-2q)|
        double sqrtNeg2r = Math.Sqrt(-2.0 * r);
        double sqrtNeg2q = Math.Sqrt(-2.0 * q);
        
        return Math.Abs(sqrtNeg2r - sqrtNeg2q);
    }

    /// <summary>
    /// Enhanced regime analysis with QuantLib integration
    /// </summary>
    public static RegimeAnalysisResult AnalyzeRegimeWithQuantLib(
        double spot, double strike, double r, double q, double sigma, 
        double timeToMaturity, Option.Type optionType)
    {
        var regime = DetermineRegime(r, q, sigma, optionType);
        var criticalVol = CalculateCriticalVolatility(r, q);
        
        // Use QuantLib for European option pricing as baseline
        var europeanPrice = CalculateEuropeanBaselineWithQuantLib(
            spot, strike, r, q, sigma, timeToMaturity, optionType);
        
        return new RegimeAnalysisResult
        {
            Regime = regime,
            CriticalVolatility = criticalVol,
            EuropeanBaseline = europeanPrice,
            RecommendedEngine = GetOptimalEngine(regime),
            NumericalComplexity = EstimateComplexity(regime),
            VolatilityMargin = !double.IsNaN(criticalVol) ? sigma - criticalVol : double.NaN
        };
    }

    /// <summary>
    /// Leverage QuantLib's BlackScholesProcess for European baseline
    /// Reduces redundant mathematical computations
    /// </summary>
    private static double CalculateEuropeanBaselineWithQuantLib(
        double spot, double strike, double r, double q, double sigma, 
        double timeToMaturity, Option.Type optionType)
    {
        try
        {
            // Use QuantLib's existing mathematical infrastructure
            var today = Settings.instance().getEvaluationDate();
            var maturity = new Date(today.serialNumber() + (uint)(timeToMaturity * 365));
            
            var underlying = new SimpleQuote(spot);
            var riskFreeRate = new FlatForward(today, r, new Actual365Fixed());
            var dividendYield = new FlatForward(today, q, new Actual365Fixed());
            var volatility = new BlackConstantVol(today, new TARGET(), sigma, new Actual365Fixed());
            
            var process = new BlackScholesMertonProcess(
                new QuoteHandle(underlying),
                new YieldTermStructureHandle(dividendYield),
                new YieldTermStructureHandle(riskFreeRate),
                new BlackVolTermStructureHandle(volatility)
            );
            
            var exercise = new EuropeanExercise(maturity);
            var payoff = new PlainVanillaPayoff(optionType, strike);
            var option = new VanillaOption(payoff, exercise);
            
            // Use QuantLib's analytic engine
            var engine = new AnalyticEuropeanEngine(process);
            option.setPricingEngine(engine);
            
            return option.NPV();
        }
        catch
        {
            // Fallback to manual calculation if QuantLib setup fails
            return CalculateEuropeanManual(spot, strike, r, q, sigma, timeToMaturity, optionType);
        }
    }

    /// <summary>
    /// Simplified manual European calculation as fallback
    /// </summary>
    private static double CalculateEuropeanManual(
        double spot, double strike, double r, double q, double sigma, 
        double timeToMaturity, Option.Type optionType)
    {
        if (timeToMaturity <= 0) return Math.Max(
            optionType == Option.Type.Call ? spot - strike : strike - spot, 0.0);
        
        double d1 = (Math.Log(spot / strike) + (r - q + 0.5 * sigma * sigma) * timeToMaturity) 
                   / (sigma * Math.Sqrt(timeToMaturity));
        double d2 = d1 - sigma * Math.Sqrt(timeToMaturity);
        
        var normalCdf = new CumulativeNormalDistribution();
        double nd1 = normalCdf.value(d1);
        double nd2 = normalCdf.value(d2);
        double nmd1 = normalCdf.value(-d1);
        double nmd2 = normalCdf.value(-d2);
        
        if (optionType == Option.Type.Call)
        {
            return spot * Math.Exp(-q * timeToMaturity) * nd1 - 
                   strike * Math.Exp(-r * timeToMaturity) * nd2;
        }
        else
        {
            return strike * Math.Exp(-r * timeToMaturity) * nmd2 - 
                   spot * Math.Exp(-q * timeToMaturity) * nmd1;
        }
    }

    private static string GetOptimalEngine(ExerciseRegimeType regime)
    {
        return regime switch
        {
            ExerciseRegimeType.DoubleBoundaryNegativeRates => "DoubleBoundaryAmericanEngine",
            ExerciseRegimeType.NoEarlyExercise => "AnalyticEuropeanEngine",
            _ => "QdFpAmericanEngine"
        };
    }

    private static string EstimateComplexity(ExerciseRegimeType regime)
    {
        return regime switch
        {
            ExerciseRegimeType.DoubleBoundaryNegativeRates => "High (Spectral+Iterative)",
            ExerciseRegimeType.NoEarlyExercise => "Low (Analytic)",
            ExerciseRegimeType.SingleBoundaryPositive => "Medium (QdFp)",
            ExerciseRegimeType.SingleBoundaryNegativeDividend => "Medium (QdFp)",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Enhanced regime analysis result with QuantLib integration
/// </summary>
public class RegimeAnalysisResult
{
    public ExerciseRegimeType Regime { get; set; }
    public double CriticalVolatility { get; set; } = double.NaN;
    public double EuropeanBaseline { get; set; }
    public string RecommendedEngine { get; set; } = "";
    public string NumericalComplexity { get; set; } = "";
    public double VolatilityMargin { get; set; } = double.NaN;
}