namespace Alaris.Double;

/// <summary>
/// High-level API for American option double boundary approximation under negative rates.
/// Combines QD+ approximation with optional Kim solver refinement.
/// </summary>
/// <remarks>
/// <para>
/// Provides a simple interface to the double boundary pricing methodology:
/// 1. QdPlusApproximation - Fast initial boundary estimate
/// 2. DoubleBoundaryKimSolver - Accurate refinement (optional)
/// </para>
/// <para>
/// This class mirrors the QuantLib structure for single boundary options,
/// adapted for the double boundary regime where q &lt; r &lt; 0.
/// </para>
/// </remarks>
public sealed class DoubleBoundaryApproximation
{
    private readonly double _spot;
    private readonly double _strike;
    private readonly double _maturity;
    private readonly double _rate;
    private readonly double _dividendYield;
    private readonly double _volatility;
    private readonly bool _isCall;
    
    /// <summary>
    /// Initializes the double boundary approximation.
    /// </summary>
    public DoubleBoundaryApproximation(
        double spot,
        double strike,
        double maturity,
        double rate,
        double dividendYield,
        double volatility,
        bool isCall)
    {
        if (spot <= 0)
        {
            throw new System.ArgumentException("Spot price must be positive", nameof(spot));
        }
        if (strike <= 0)
        {
            throw new System.ArgumentException("Strike price must be positive", nameof(strike));
        }
        if (maturity <= 0)
        {
            throw new System.ArgumentException("Maturity must be positive", nameof(maturity));
        }
        if (volatility <= 0)
        {
            throw new System.ArgumentException("Volatility must be positive", nameof(volatility));
        }
        
        _spot = spot;
        _strike = strike;
        _maturity = maturity;
        _rate = rate;
        _dividendYield = dividendYield;
        _volatility = volatility;
        _isCall = isCall;
    }
    
    /// <summary>
    /// Calculates boundaries using QD+ approximation (fast).
    /// </summary>
    public BoundaryResult CalculateBoundaries()
    {
        QdPlusApproximation solver = new QdPlusApproximation(
            _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall);

        var (upper, lower) = solver.CalculateBoundaries();
        
        bool boundariesCross = _isCall ? (upper <= lower) : (lower >= upper);
        
        return new BoundaryResult
        {
            UpperBoundary = upper,
            LowerBoundary = lower,
            BoundariesCross = boundariesCross,
            IsValid = !boundariesCross
        };
    }
    
    /// <summary>
    /// Approximates option value using QD+ boundaries.
    /// </summary>
    public double ApproximateValue()
    {
        BoundaryResult boundaries = CalculateBoundaries();
        
        if (boundaries.BoundariesCross || !boundaries.IsValid)
        {
            return CalculateEuropeanValue();
        }
        
        if (ShouldExerciseImmediately(boundaries))
        {
            return CalculateIntrinsicValue();
        }
        
        double europeanValue = CalculateEuropeanValue();
        double earlyExercisePremium = CalculateEarlyExercisePremium(boundaries);
        
        return europeanValue + earlyExercisePremium;
    }
    
    /// <summary>
    /// Checks if option should be exercised immediately.
    /// </summary>
    private bool ShouldExerciseImmediately(BoundaryResult boundaries)
    {
        if (_isCall)
        {
            return _spot >= boundaries.UpperBoundary;
        }
        else
        {
            return _spot <= boundaries.LowerBoundary;
        }
    }
    
    /// <summary>
    /// Calculates early exercise premium using QD+ approximation.
    /// Uses region-based logic according to Healy (2021) Equation 13.
    /// </summary>
    /// <remarks>
    /// Premium formula: <c>e(S) = a1 * S^lambda1 * 1_{S &gt;= S1*} + a2 * S^lambda2 * 1_{S &lt;= S2*}</c>
    ///
    /// Three regions (same for both calls and puts):
    /// - <c>S &gt;= S1*</c> (upper): Use <c>a1</c> term with <c>lambda1</c>
    /// - <c>S &lt;= S2*</c> (lower): Use <c>a2</c> term with <c>lambda2</c>
    /// - <c>S2* &lt; S &lt; S1*</c> (between): No early exercise premium
    /// </remarks>
    private double CalculateEarlyExercisePremium(BoundaryResult boundaries)
    {
        var (lambda1, lambda2) = CalculateLambdas();
        
        // Check which region the spot price falls into
        if (_spot >= boundaries.UpperBoundary)
        {
            // Above upper boundary: use a₁ term with λ₁
            double a1 = CalculateBoundaryCoefficient(boundaries.UpperBoundary, lambda1);
            return a1 * System.Math.Pow(_spot, lambda1);
        }
        else if (_spot <= boundaries.LowerBoundary)
        {
            // Below lower boundary: use a₂ term with λ₂
            double a2 = CalculateBoundaryCoefficient(boundaries.LowerBoundary, lambda2);
            return a2 * System.Math.Pow(_spot, lambda2);
        }
        else
        {
            // Between boundaries: no early exercise premium
            // American value = European value
            return 0.0;
        }
    }
    
    /// <summary>
    /// Calculates lambda values.
    /// </summary>
    private (double lambda1, double lambda2) CalculateLambdas()
    {
        double h = 1.0 - System.Math.Exp(-_rate * _maturity);
        double sigma2 = _volatility * _volatility;
        double alpha = (2.0 * _rate) / sigma2;
        double beta = (2.0 * (_rate - _dividendYield)) / sigma2;

        double discriminant = System.Math.Sqrt(((beta - 1) * (beta - 1)) + ((4.0 * alpha) / h));
        double lambda1 = ((-(beta - 1)) - discriminant) / 2.0;
        double lambda2 = ((-(beta - 1)) + discriminant) / 2.0;
        
        return (lambda1, lambda2);
    }
    
    /// <summary>
    /// Calculates boundary coefficient from continuity condition.
    /// </summary>
    private double CalculateBoundaryCoefficient(double boundary, double lambda)
    {
        double eta = _isCall ? 1.0 : -1.0;
        double intrinsic = eta * (boundary - _strike);
        double europeanValue = CalculateEuropeanValue(boundary);
        
        double numerator = intrinsic - europeanValue;
        double denominator = System.Math.Pow(boundary, lambda);
        
        return denominator != 0 ? numerator / denominator : 0.0;
    }
    
    /// <summary>
    /// Calculates European option value.
    /// </summary>
    private double CalculateEuropeanValue(double? spot = null)
    {
        double S = spot ?? _spot;
        double d1 = CalculateD1(S);
        double d2 = d1 - (_volatility * System.Math.Sqrt(_maturity));
        
        double discountFactor = System.Math.Exp(-_rate * _maturity);
        double dividendFactor = System.Math.Exp(-_dividendYield * _maturity);
        
        if (_isCall)
        {
            return S * dividendFactor * NormalCDF(d1) 
                 - _strike * discountFactor * NormalCDF(d2);
        }
        else
        {
            return _strike * discountFactor * NormalCDF(-d2) 
                 - S * dividendFactor * NormalCDF(-d1);
        }
    }
    
    /// <summary>
    /// Calculates intrinsic value.
    /// </summary>
    private double CalculateIntrinsicValue()
    {
        return _isCall 
            ? System.Math.Max(_spot - _strike, 0.0) 
            : System.Math.Max(_strike - _spot, 0.0);
    }
    
    /// <summary>
    /// Calculates d₁.
    /// </summary>
    private double CalculateD1(double S)
    {
        double numerator = System.Math.Log(S / _strike)
                         + ((_rate - _dividendYield + (0.5 * _volatility * _volatility)) * _maturity);
        return numerator / (_volatility * System.Math.Sqrt(_maturity));
    }
    
    /// <summary>
    /// Standard normal CDF.
    /// </summary>
    private double NormalCDF(double x)
    {
        if (x > 8.0)
        {
            return 1.0;
        }
        if (x < -8.0)
        {
            return 0.0;
        }
        return (0.5 * (1.0 + Erf(x / System.Math.Sqrt(2.0))));
    }
    
    /// <summary>
    /// Error function.
    /// </summary>
    private double Erf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;
        
        int sign = x < 0 ? -1 : 1;
        x = System.Math.Abs(x);

        double t = 1.0 / (1.0 + (p * x));
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * System.Math.Exp(-(x * x));

        return sign * y;
    }
}

/// <summary>
/// Result of boundary calculation for double boundary options.
/// </summary>
public sealed class BoundaryResult
{
    /// <summary>
    /// The upper exercise boundary S*₁.
    /// </summary>
    public double UpperBoundary { get; init; }
    
    /// <summary>
    /// The lower exercise boundary S*₂.
    /// </summary>
    public double LowerBoundary { get; init; }
    
    /// <summary>
    /// Indicates whether boundaries cross (invalid approximation).
    /// </summary>
    public bool BoundariesCross { get; init; }
    
    /// <summary>
    /// Indicates whether the boundary calculation is valid.
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// String representation.
    /// </summary>
    public override string ToString()
    {
        return $"Upper: {UpperBoundary:F4}, Lower: {LowerBoundary:F4}, " +
               $"Valid: {IsValid}, Cross: {BoundariesCross}";
    }
}