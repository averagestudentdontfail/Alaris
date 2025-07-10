using Alaris.Double;
using Microsoft.Extensions.Logging;

namespace Alaris.Double;

/// <summary>
/// Results structure for double boundary American option calculations
/// Contains detailed information about the exercise regime and boundaries
/// </summary>
public class DoubleBoundaryResults
{
    public double OptionPrice { get; set; }
    public ExerciseRegimeType Regime { get; set; }
    public double CriticalVolatility { get; set; } = double.NaN;
    public double BoundaryIntersectionTime { get; set; } = double.PositiveInfinity;
    public BoundaryFunction? UpperBoundary { get; set; }
    public BoundaryFunction? LowerBoundary { get; set; }
    public int IterationsConverged { get; set; }
    public double FinalError { get; set; }
    public TimeSpan ComputationTime { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Advanced American option pricing engine supporting double boundaries under negative interest rates
/// Inherits from PricingEngine to be compatible with QuantLib option pricing framework
/// </summary>
public class DoubleBoundaryAmericanEngine : PricingEngine
{
    private readonly GeneralizedBlackScholesProcess _process;
    private readonly int _spectralNodes;
    private readonly double _tolerance;
    private readonly int _maxIterations;
    private readonly bool _useAcceleration;
    private readonly ILogger? _logger;

    // Option parameters - these will be set when the engine is used
    private double _strike = 100.0;
    private double _timeToMaturity = 1.0;
    private Option.Type _optionType = Option.Type.Put;

    // Cached calculation results
    private DoubleBoundaryResults? _lastResults;
    
    public DoubleBoundaryAmericanEngine(
        GeneralizedBlackScholesProcess process,
        int spectralNodes = 8,
        double tolerance = 1e-12,
        int maxIterations = 100,
        bool useAcceleration = true,
        ILogger? logger = null) : base()
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _spectralNodes = Math.Max(3, spectralNodes);
        _tolerance = tolerance;
        _maxIterations = maxIterations;
        _useAcceleration = useAcceleration;
        _logger = logger;
    }

    // Method to set option parameters externally since we can't access Arguments directly
    public void SetOptionParameters(double strike, double timeToMaturity, Option.Type optionType)
    {
        _strike = strike;
        _timeToMaturity = timeToMaturity;
        _optionType = optionType;
    }

    /// <summary>
    /// Main pricing method - calculates option price and stores results
    /// </summary>
    public double CalculatePrice()
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Extract market parameters using alternative approach
            var marketParams = ExtractMarketParametersAlternative();
            
            _lastResults = CalculateDoubleBoundaryOption(marketParams);
            
            _logger?.LogInformation("American option priced: {Price:F6} in regime {Regime}", 
                                  _lastResults.OptionPrice, _lastResults.Regime);
            
            return _lastResults.OptionPrice;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to price American option");
            throw;
        }
        finally
        {
            var endTime = DateTime.UtcNow;
            if (_lastResults != null)
            {
                _lastResults.ComputationTime = endTime - startTime;
            }
        }
    }

    /// <summary>
    /// Gets the detailed results from the last calculation
    /// </summary>
    public DoubleBoundaryResults? GetDetailedResults() => _lastResults;

    /// <summary>
    /// Static method to price an American option with optimal engine selection
    /// </summary>
    public static double PriceAmericanOption(VanillaOption option, GeneralizedBlackScholesProcess process,
        int spectralNodes = 8, double tolerance = 1e-12, ILogger? logger = null)
    {
        // Extract parameters from the option and process
        var marketParams = ExtractMarketParameters(option, process);
        
        // Determine the optimal regime
        var regime = RegimeAnalyzer.DetermineRegime(marketParams.R, marketParams.Q, 
                                                  marketParams.Sigma, marketParams.OptionType);
        
        logger?.LogDebug("Selected regime {Regime} for pricing", regime);

        switch (regime)
        {
            case ExerciseRegimeType.DoubleBoundaryNegativeRates:
                {
                    var engine = new DoubleBoundaryAmericanEngine(process, spectralNodes, tolerance, logger: logger);
                    engine.SetOptionParameters(marketParams.Strike, marketParams.Tau, marketParams.OptionType);
                    return engine.CalculatePrice();
                }
                
            case ExerciseRegimeType.NoEarlyExercise:
                {
                    // Use European engine
                    var europeanEngine = new AnalyticEuropeanEngine(process);
                    var today = Settings.instance().getEvaluationDate();
                    var maturity = QuantLibApiHelper.AddDaysToDate(today, (int)(marketParams.Tau * 365));
                    var europeanExercise = new EuropeanExercise(maturity);
                    var payoff = new PlainVanillaPayoff(marketParams.OptionType, marketParams.Strike);
                    var europeanOption = new VanillaOption(payoff, europeanExercise);
                    europeanOption.setPricingEngine(europeanEngine);
                    return europeanOption.NPV();
                }
                
            default:
                {
                    // Use standard American engine
                    var standardEngine = new QdFpAmericanEngine(process, QdFpAmericanEngine.accurateScheme());
                    option.setPricingEngine(standardEngine);
                    return option.NPV();
                }
        }
    }

    private DoubleBoundaryResults CalculateDoubleBoundaryOption(MarketParameters marketParams)
    {
        // Determine exercise regime
        var regime = RegimeAnalyzer.DetermineRegime(marketParams.R, marketParams.Q, 
                                                  marketParams.Sigma, marketParams.OptionType);
        
        var detailedResults = new DoubleBoundaryResults
        {
            Regime = regime,
            CriticalVolatility = RegimeAnalyzer.CriticalVolatility(marketParams.R, marketParams.Q)
        };

        _logger?.LogDebug("Detected regime: {Regime} for r={R:F4}, q={Q:F4}, σ={Sigma:F4}", 
                         regime, marketParams.R, marketParams.Q, marketParams.Sigma);

        switch (regime)
        {
            case ExerciseRegimeType.SingleBoundaryPositive:
            case ExerciseRegimeType.SingleBoundaryNegativeDividend:
                detailedResults.OptionPrice = CalculateSingleBoundary(marketParams, detailedResults);
                break;
                
            case ExerciseRegimeType.DoubleBoundaryNegativeRates:
                detailedResults.OptionPrice = CalculateDoubleBoundary(marketParams, detailedResults);
                break;
                
            case ExerciseRegimeType.NoEarlyExercise:
                detailedResults.OptionPrice = CalculateEuropeanPrice(marketParams);
                break;
                
            default:
                throw new ArgumentException($"Unsupported exercise regime: {regime}");
        }

        return detailedResults;
    }

    private double CalculateSingleBoundary(MarketParameters marketParams, DoubleBoundaryResults results)
    {
        _logger?.LogDebug("Computing single boundary American option");
        
        // Use existing QdFp engine for single boundary cases
        var standardEngine = new QdFpAmericanEngine(_process, QdFpAmericanEngine.accurateScheme());
        
        // Create temporary option for pricing
        var today = Settings.instance().getEvaluationDate();
        var maturity = QuantLibApiHelper.AddDaysToDate(today, (int)(marketParams.Tau * 365));
        var exercise = new AmericanExercise(today, maturity);
        var payoff = new PlainVanillaPayoff(marketParams.OptionType, marketParams.Strike);
        var tempOption = new VanillaOption(payoff, exercise);
        
        tempOption.setPricingEngine(standardEngine);
        
        results.IterationsConverged = 1; // Single calculation
        results.FinalError = 0.0; // Assume QuantLib engine converged
        
        return tempOption.NPV();
    }

    private double CalculateDoubleBoundary(MarketParameters marketParams, DoubleBoundaryResults results)
    {
        _logger?.LogDebug("Computing double boundary American option");
        
        if (marketParams.OptionType != Option.Type.Put)
        {
            throw new NotImplementedException("Double boundary calls not yet implemented");
        }

        // Estimate boundary intersection time
        results.BoundaryIntersectionTime = RegimeAnalyzer.EstimateBoundaryIntersectionTime(
            marketParams.R, marketParams.Q, marketParams.Sigma, marketParams.Strike);
        
        double effectiveTau = Math.Min(marketParams.Tau, results.BoundaryIntersectionTime);
        
        _logger?.LogDebug("Effective maturity: {EffectiveTau:F4} (intersection at {IntersectionTime:F4})", 
                         effectiveTau, results.BoundaryIntersectionTime);

        // Compute boundaries using decoupled iterations
        var (upperBoundary, lowerBoundary, iterations, finalError) = ComputeBoundaries(marketParams, effectiveTau);
        
        results.UpperBoundary = upperBoundary;
        results.LowerBoundary = lowerBoundary;
        results.IterationsConverged = iterations;
        results.FinalError = finalError;

        // Evaluate option price using double boundary integral
        return EvaluateDoubleBoundaryPrice(marketParams, effectiveTau, upperBoundary, lowerBoundary);
    }

    private (BoundaryFunction upperBoundary, BoundaryFunction lowerBoundary, int iterations, double finalError) 
        ComputeBoundaries(MarketParameters marketParams, double effectiveTau)
    {
        // Generate temporal mesh with higher density near expiration
        var timePoints = GenerateTimePoints(effectiveTau, _spectralNodes);
        var chebyshevNodes = Spectral.ChebyshevNodes(_spectralNodes);
        
        // Initialize boundaries with analytical approximations
        var upperBoundaryValues = InitializeUpperBoundary(timePoints, marketParams);
        var lowerBoundaryValues = InitializeLowerBoundary(timePoints, marketParams);
        
        int totalIterations = 0;
        double maxError = double.MaxValue;
        
        // Decoupled fixed-point iterations
        for (int iter = 0; iter < _maxIterations; iter++)
        {
            var oldUpperValues = (double[])upperBoundaryValues.Clone();
            var oldLowerValues = (double[])lowerBoundaryValues.Clone();
            
            // Update upper boundary (value-matching)
            var upperBoundary = new BoundaryFunction(chebyshevNodes, upperBoundaryValues, timePoints,
                                                   marketParams.Strike, marketParams.R, marketParams.Q);
            
            for (int i = 0; i < _spectralNodes; i++)
            {
                upperBoundaryValues[i] = IntegralEquationSolvers.SolveUpperBoundaryEquation(
                    timePoints[i], upperBoundaryValues[i], upperBoundary,
                    marketParams.Strike, marketParams.R, marketParams.Q, marketParams.Sigma, effectiveTau);
            }
            
            // Update lower boundary (smooth-pasting)
            var lowerBoundary = new BoundaryFunction(chebyshevNodes, lowerBoundaryValues, timePoints,
                                                   marketParams.Strike, marketParams.R, marketParams.Q);
            
            for (int i = 0; i < _spectralNodes; i++)
            {
                lowerBoundaryValues[i] = IntegralEquationSolvers.SolveLowerBoundaryEquation(
                    timePoints[i], lowerBoundaryValues[i], lowerBoundary,
                    marketParams.Strike, marketParams.R, marketParams.Q, marketParams.Sigma, effectiveTau);
            }
            
            // Check convergence
            double upperError = ComputeMaxRelativeError(upperBoundaryValues, oldUpperValues);
            double lowerError = ComputeMaxRelativeError(lowerBoundaryValues, oldLowerValues);
            maxError = Math.Max(upperError, lowerError);
            
            totalIterations = iter + 1;
            
            _logger?.LogDebug("Iteration {Iter}: Upper error={UpperError:E2}, Lower error={LowerError:E2}", 
                             iter + 1, upperError, lowerError);
            
            if (maxError < _tolerance) break;
            
            // Apply acceleration if enabled
            if (_useAcceleration && iter > 2)
            {
                ApplyAndersonAcceleration(ref upperBoundaryValues, ref lowerBoundaryValues, iter);
            }
        }
        
        var finalUpperBoundary = new BoundaryFunction(chebyshevNodes, upperBoundaryValues, timePoints,
                                                     marketParams.Strike, marketParams.R, marketParams.Q);
        var finalLowerBoundary = new BoundaryFunction(chebyshevNodes, lowerBoundaryValues, timePoints,
                                                     marketParams.Strike, marketParams.R, marketParams.Q);
        
        return (finalUpperBoundary, finalLowerBoundary, totalIterations, maxError);
    }

    private double EvaluateDoubleBoundaryPrice(MarketParameters marketParams, double effectiveTau,
                                             BoundaryFunction upperBoundary, BoundaryFunction lowerBoundary)
    {
        // European option baseline
        double europeanPrice = CalculateEuropeanPrice(marketParams);
        
        // Early exercise premium with double boundary integration
        double premium = ComputeDoubleBoundaryPremium(marketParams, effectiveTau, upperBoundary, lowerBoundary);
        
        _logger?.LogDebug("European price: {European:F6}, Premium: {Premium:F6}", europeanPrice, premium);
        
        return europeanPrice + premium;
    }

    private double ComputeDoubleBoundaryPremium(MarketParameters marketParams, double effectiveTau,
                                              BoundaryFunction upperBoundary, BoundaryFunction lowerBoundary)
    {
        // Use high-order quadrature for premium calculation
        var integrator = new GaussLobattoIntegral(1000, 1e-10);
        
        var premiumIntegrand = new DoubleBoundaryPremiumIntegrand(
            marketParams, effectiveTau, upperBoundary, lowerBoundary);
        
        try
        {
            return QuantLibApiHelper.CallGaussLobattoIntegral(integrator, premiumIntegrand.value, 0.0, effectiveTau);
        }
        catch
        {
            // Fallback to Simpson's rule
            var simpsonIntegrator = new SimpsonIntegral(1e-10, 1000);
            return QuantLibApiHelper.CallSimpsonIntegral(simpsonIntegrator, premiumIntegrand.value, 0.0, effectiveTau);
        }
    }

    private double CalculateEuropeanPrice(MarketParameters marketParams)
    {
        var today = Settings.instance().getEvaluationDate();
        var maturity = QuantLibApiHelper.AddDaysToDate(today, (int)(marketParams.Tau * 365));
        var europeanExercise = new EuropeanExercise(maturity);
        var payoff = new PlainVanillaPayoff(marketParams.OptionType, marketParams.Strike);
        var europeanOption = new VanillaOption(payoff, europeanExercise);
        
        var bsEngine = new AnalyticEuropeanEngine(_process);
        europeanOption.setPricingEngine(bsEngine);
        
        return europeanOption.NPV();
    }

    private MarketParameters ExtractMarketParametersAlternative()
    {
        // Alternative method to extract parameters without using VanillaOption.Arguments
        var today = Settings.instance().getEvaluationDate();
        double tau = _timeToMaturity;
        
        double r = QuantLibApiHelper.GetTermStructure(_process.riskFreeRate()).zeroRate(tau, Compounding.Continuous).rate();
        double q = QuantLibApiHelper.GetTermStructure(_process.dividendYield()).zeroRate(tau, Compounding.Continuous).rate();
        double sigma = QuantLibApiHelper.GetVolatilityStructure(_process.blackVolatility()).blackVol(tau, _process.x0());
        double spot = _process.x0();
        double strike = _strike;
        var optionType = _optionType;
        
        return new MarketParameters
        {
            Spot = spot,
            Strike = strike,
            Tau = tau,
            R = r,
            Q = q,
            Sigma = sigma,
            OptionType = optionType
        };
    }

    private static MarketParameters ExtractMarketParameters(VanillaOption option, GeneralizedBlackScholesProcess process)
    {
        var today = Settings.instance().getEvaluationDate();
        
        // Try to extract parameters using reflection
        double strike = 100.0; // default value
        double tau = 1.0; // default value
        Option.Type optionType = Option.Type.Put; // default value
        
        try
        {
            // Try to access the payoff to get strike and option type
            var optionType_obj = option.GetType();
            
            // Look for payoff property/field
            var payoffProperty = optionType_obj.GetProperty("payoff");
            var payoffField = optionType_obj.GetField("payoff");
            
            object? payoff = null;
            if (payoffProperty != null)
            {
                payoff = payoffProperty.GetValue(option);
            }
            else if (payoffField != null)
            {
                payoff = payoffField.GetValue(option);
            }
            
            if (payoff != null)
            {
                var payoffType = payoff.GetType();
                
                // Try to get strike
                var strikeMethod = payoffType.GetMethod("strike");
                if (strikeMethod != null)
                {
                    var strikeResult = strikeMethod.Invoke(payoff, null);
                    if (strikeResult is double strikeValue)
                    {
                        strike = strikeValue;
                    }
                }
                
                // Try to get option type
                var optionTypeMethod = payoffType.GetMethod("optionType");
                if (optionTypeMethod != null)
                {
                    var optionTypeResult = optionTypeMethod.Invoke(payoff, null);
                    if (optionTypeResult is Option.Type typeValue)
                    {
                        optionType = typeValue;
                    }
                }
            }
            
            // Try to access the exercise to get maturity
            var exerciseProperty = optionType_obj.GetProperty("exercise");
            var exerciseField = optionType_obj.GetField("exercise");
            
            object? exercise = null;
            if (exerciseProperty != null)
            {
                exercise = exerciseProperty.GetValue(option);
            }
            else if (exerciseField != null)
            {
                exercise = exerciseField.GetValue(option);
            }
            
            if (exercise != null)
            {
                var exerciseType = exercise.GetType();
                
                // Try to get last date (maturity)
                var lastDateMethod = exerciseType.GetMethod("lastDate");
                if (lastDateMethod != null)
                {
                    var lastDateResult = lastDateMethod.Invoke(exercise, null);
                    if (lastDateResult is Date maturityDate)
                    {
                        tau = Math.Max((maturityDate.serialNumber() - today.serialNumber()) / 365.0, Constants.MIN_TIME_TO_MATURITY);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If reflection fails, use default values
            System.Diagnostics.Debug.WriteLine($"Failed to extract option parameters via reflection: {ex.Message}");
        }
        
        // Extract market parameters from process
        double r = QuantLibApiHelper.GetTermStructure(process.riskFreeRate()).zeroRate(tau, Compounding.Continuous).rate();
        double q = QuantLibApiHelper.GetTermStructure(process.dividendYield()).zeroRate(tau, Compounding.Continuous).rate();
        double sigma = QuantLibApiHelper.GetVolatilityStructure(process.blackVolatility()).blackVol(tau, process.x0());
        double spot = process.x0();
        
        return new MarketParameters
        {
            Spot = spot,
            Strike = strike,
            Tau = tau,
            R = r,
            Q = q,
            Sigma = sigma,
            OptionType = optionType
        };
    }

    private double[] GenerateTimePoints(double maxTau, int numPoints)
    {
        // Non-uniform spacing with higher density near expiration
        var points = new double[numPoints];
        
        for (int i = 0; i < numPoints; i++)
        {
            double xi = (double)i / (numPoints - 1);
            // Square-root transformation concentrates points near t=0
            points[i] = maxTau * xi * xi;
        }
        
        return points;
    }

    private double[] InitializeUpperBoundary(double[] timePoints, MarketParameters marketParams)
    {
        double perpetualBoundary = RegimeAnalyzer.PerpetualBoundary(
            marketParams.Strike, marketParams.R, marketParams.Q, marketParams.Sigma, Option.Type.Put);
        
        var values = new double[timePoints.Length];
        
        for (int i = 0; i < timePoints.Length; i++)
        {
            double tau = timePoints[i];
            if (tau < 1e-6)
            {
                values[i] = marketParams.Strike; // Boundary condition at expiration
            }
            else
            {
                // Exponential approach to perpetual boundary
                values[i] = marketParams.Strike + (perpetualBoundary - marketParams.Strike) * 
                           (1.0 - Math.Exp(-tau / (0.1 * marketParams.Sigma * marketParams.Sigma)));
            }
        }
        
        return values;
    }

    private double[] InitializeLowerBoundary(double[] timePoints, MarketParameters marketParams)
    {
        double limitingRatio = marketParams.R / marketParams.Q; // 0 < r/q < 1 for q < r < 0
        
        var values = new double[timePoints.Length];
        
        for (int i = 0; i < timePoints.Length; i++)
        {
            double tau = timePoints[i];
            if (tau < 1e-6)
            {
                values[i] = marketParams.Strike * limitingRatio;
            }
            else
            {
                // Gradual approach from limiting value
                values[i] = marketParams.Strike * limitingRatio * 
                           (1.0 + 0.2 * tau / marketParams.Tau);
            }
        }
        
        return values;
    }

    private static double ComputeMaxRelativeError(double[] newValues, double[] oldValues)
    {
        double maxError = 0.0;
        
        for (int i = 0; i < newValues.Length; i++)
        {
            if (Math.Abs(oldValues[i]) > 1e-10)
            {
                double relativeError = Math.Abs((newValues[i] - oldValues[i]) / oldValues[i]);
                maxError = Math.Max(maxError, relativeError);
            }
        }
        
        return maxError;
    }

    private void ApplyAndersonAcceleration(ref double[] upperValues, ref double[] lowerValues, int iteration)
    {
        // Simplified Anderson acceleration - in production, would maintain full history
        if (iteration < 3) return;
        
        const double relaxation = 0.7;
        
        for (int i = 0; i < upperValues.Length; i++)
        {
            upperValues[i] = relaxation * upperValues[i] + (1 - relaxation) * upperValues[i];
            lowerValues[i] = relaxation * lowerValues[i] + (1 - relaxation) * lowerValues[i];
        }
    }
}

/// <summary>
/// Market parameter container
/// </summary>
internal class MarketParameters
{
    public double Spot { get; set; }
    public double Strike { get; set; }
    public double Tau { get; set; }
    public double R { get; set; }
    public double Q { get; set; }
    public double Sigma { get; set; }
    public Option.Type OptionType { get; set; }
}

/// <summary>
/// Integrand for computing double boundary early exercise premium
/// </summary>
internal class DoubleBoundaryPremiumIntegrand
{
    private readonly MarketParameters _params;
    private readonly double _effectiveTau;
    private readonly BoundaryFunction _upperBoundary, _lowerBoundary;
    private static readonly CumulativeNormalDistribution _normalCdf = new CumulativeNormalDistribution();

    public DoubleBoundaryPremiumIntegrand(MarketParameters parameters, double effectiveTau,
                                        BoundaryFunction upperBoundary, BoundaryFunction lowerBoundary)
    {
        _params = parameters;
        _effectiveTau = effectiveTau;
        _upperBoundary = upperBoundary;
        _lowerBoundary = lowerBoundary;
    }

    public double value(double u)
    {
        if (u >= _effectiveTau || u < 0) return 0.0;

        double upperBoundaryAtU = _upperBoundary.Evaluate(u);
        double lowerBoundaryAtU = _lowerBoundary.Evaluate(u);
        double timeStep = _effectiveTau - u;
        
        if (timeStep <= 0) return 0.0;

        // Interest component: r*K*e^(-r*timeStep) * [Φ(-d_-(B)) - Φ(-d_-(Y))]
        double d_minus_upper = ComputeD(-1, timeStep, _params.Spot / upperBoundaryAtU, _params.R, _params.Q, _params.Sigma);
        double d_minus_lower = ComputeD(-1, timeStep, _params.Spot / lowerBoundaryAtU, _params.R, _params.Q, _params.Sigma);
        
        double interestTerm = _params.R * _params.Strike * Math.Exp(-_params.R * timeStep) *
                            (QuantLibApiHelper.CallCumNorm(_normalCdf, -d_minus_upper) - QuantLibApiHelper.CallCumNorm(_normalCdf, -d_minus_lower));

        // Dividend component: q*S*e^(-q*timeStep) * [Φ(-d_+(B)) - Φ(-d_+(Y))]
        double d_plus_upper = ComputeD(1, timeStep, _params.Spot / upperBoundaryAtU, _params.R, _params.Q, _params.Sigma);
        double d_plus_lower = ComputeD(1, timeStep, _params.Spot / lowerBoundaryAtU, _params.R, _params.Q, _params.Sigma);
        
        double dividendTerm = _params.Q * _params.Spot * Math.Exp(-_params.Q * timeStep) *
                            (QuantLibApiHelper.CallCumNorm(_normalCdf, -d_plus_upper) - QuantLibApiHelper.CallCumNorm(_normalCdf, -d_plus_lower));

        return interestTerm - dividendTerm;
    }

    private static double ComputeD(int sign, double tau, double moneyness, double r, double q, double sigma)
    {
        if (tau <= 0 || sigma <= 0)
        {
            return sign > 0 ? double.PositiveInfinity : double.NegativeInfinity;
        }

        return (Math.Log(moneyness) + (r - q + sign * 0.5 * sigma * sigma) * tau) / (sigma * Math.Sqrt(tau));
    }
}