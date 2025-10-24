namespace Alaris.Double;

/// <summary>
/// Complete solver for American options with double boundaries under negative rates.
/// Combines QD+ approximation with Kim integral equation refinement.
/// </summary>
/// <remarks>
/// <para>
/// Two-stage solving process:
/// 1. QD+ approximation provides fast initial boundaries
/// 2. Kim solver refines using fixed point iteration on integral equations
/// </para>
/// <para>
/// Architecture mirrors QuantLib's approach:
/// - Single boundary: QdPlus → QdFp (Chebyshev)
/// - Double boundary: QdPlus → Kim (collocation + fixed point)
/// </para>
/// </remarks>
public sealed class DoubleBoundarySolver
{
    private readonly double _spot;
    private readonly double _strike;
    private readonly double _maturity;
    private readonly double _rate;
    private readonly double _dividendYield;
    private readonly double _volatility;
    private readonly bool _isCall;
    private readonly int _collocationPoints;
    private readonly bool _useRefinement;
    
    /// <summary>
    /// Initializes the double boundary solver.
    /// </summary>
    /// <param name="spot">Current asset price</param>
    /// <param name="strike">Strike price</param>
    /// <param name="maturity">Time to maturity</param>
    /// <param name="rate">Risk-free rate</param>
    /// <param name="dividendYield">Dividend yield</param>
    /// <param name="volatility">Volatility</param>
    /// <param name="isCall">True for call, false for put</param>
    /// <param name="collocationPoints">Number of time points (default 50)</param>
    /// <param name="useRefinement">Use Kim refinement (default true)</param>
    public DoubleBoundarySolver(
        double spot,
        double strike,
        double maturity,
        double rate,
        double dividendYield,
        double volatility,
        bool isCall,
        int collocationPoints = 50,
        bool useRefinement = true)
    {
        _spot = spot;
        _strike = strike;
        _maturity = maturity;
        _rate = rate;
        _dividendYield = dividendYield;
        _volatility = volatility;
        _isCall = isCall;
        _collocationPoints = collocationPoints;
        _useRefinement = useRefinement;
    }
    
    /// <summary>
    /// Solves for both boundaries.
    /// </summary>
    /// <returns>Upper and lower boundaries at t=0, and crossing time</returns>
    public (double Upper, double Lower, double CrossingTime) SolveBoundaries()
    {
        var qdPlus = new QdPlusApproximation(
            _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall);
        
        var (upperInitial, lowerInitial) = qdPlus.CalculateBoundaries();
        
        if (upperInitial == _strike && lowerInitial == _strike)
        {
            return (_strike, _strike, 0.0);
        }
        
        if (!_useRefinement)
        {
            return (upperInitial, lowerInitial, _maturity);
        }
        
        var kimSolver = new DoubleBoundaryKimSolver(
            _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall, _collocationPoints);
        
        var (upperArray, lowerArray, crossingTime) = kimSolver.SolveBoundaries(upperInitial, lowerInitial);
        
        return (upperArray[0], lowerArray[0], crossingTime);
    }
    
    /// <summary>
    /// Calculates the American option value.
    /// </summary>
    public double CalculateValue()
    {
        if (!_useRefinement)
        {
            var approximation = new DoubleBoundaryApproximation(
                _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall);
            return approximation.ApproximateValue();
        }
        
        var (upper, lower, crossingTime) = SolveBoundaries();
        
        if (ShouldExerciseImmediately(upper, lower))
        {
            return CalculateIntrinsicValue();
        }
        
        // Get refined boundary arrays for integration
        var kimSolver = new DoubleBoundaryKimSolver(
            _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall, _collocationPoints);
        
        var qdPlus = new QdPlusApproximation(
            _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall);
        
        var (upperInitial, lowerInitial) = qdPlus.CalculateBoundaries();
        var (upperArray, lowerArray, refinedCrossingTime) = kimSolver.SolveBoundaries(upperInitial, lowerInitial);
        
        double europeanValue = CalculateEuropeanValue();
        double earlyExercisePremium = CalculateEarlyExercisePremium(upperArray, lowerArray, refinedCrossingTime);
        
        return europeanValue + earlyExercisePremium;
    }
    
    /// <summary>
    /// Checks if immediate exercise is optimal.
    /// </summary>
    private bool ShouldExerciseImmediately(double upper, double lower)
    {
        if (_isCall)
            return _spot >= upper;
        else
            return _spot <= lower;
    }
    
    /// <summary>
    /// Calculates early exercise premium from boundaries.
    /// </summary>
    private double CalculateEarlyExercisePremium(double[] upper, double[] lower, double crossingTime)
    {
        double integralUpper = CalculateIntegral(_spot, 0.0, upper, crossingTime);
        double integralLower = CalculateIntegral(_spot, 0.0, lower, crossingTime);
        
        return integralUpper - integralLower;
    }
    
    /// <summary>
    /// Calculates Kim integral for early exercise premium.
    /// </summary>
    private double CalculateIntegral(double S, double ti, double[] boundary, double crossingTime)
    {
        double tStart = System.Math.Max(ti, crossingTime);
        if (tStart >= _maturity)
            return 0.0;
        
        const int INTEGRATION_POINTS = 50;
        double dt = (_maturity - tStart) / INTEGRATION_POINTS;
        double integral = 0.0;
        
        for (int j = 0; j < INTEGRATION_POINTS; j++)
        {
            double t = tStart + (j + 0.5) * dt;
            double tMinusTi = t - ti;
            
            if (tMinusTi < 1e-10)
                continue;
            
            double boundaryValue = InterpolateBoundary(boundary, t);
            
            double d1 = CalculateD1(S, boundaryValue, tMinusTi);
            double d2 = d1 - _volatility * System.Math.Sqrt(tMinusTi);
            
            double term1 = _rate * _strike * System.Math.Exp(-_rate * tMinusTi) * NormalCDF(-d2);
            double term2 = _dividendYield * S * System.Math.Exp(-_dividendYield * tMinusTi) * NormalCDF(-d1);
            
            integral += (term1 - term2) * dt;
        }
        
        return integral;
    }
    
    /// <summary>
    /// Interpolates boundary at given time.
    /// </summary>
    private double InterpolateBoundary(double[] boundary, double t)
    {
        int m = boundary.Length;
        double dt = _maturity / (m - 1);
        int i = (int)(t / dt);
        
        if (i >= m - 1) return boundary[m - 1];
        if (i < 0) return boundary[0];
        
        double alpha = (t - i * dt) / dt;
        return boundary[i] * (1 - alpha) + boundary[i + 1] * alpha;
    }
    
    /// <summary>
    /// Calculates European option value.
    /// </summary>
    private double CalculateEuropeanValue()
    {
        double d1 = CalculateD1(_spot, _strike, _maturity);
        double d2 = d1 - _volatility * System.Math.Sqrt(_maturity);
        
        double discountFactor = System.Math.Exp(-_rate * _maturity);
        double dividendFactor = System.Math.Exp(-_dividendYield * _maturity);
        
        if (_isCall)
        {
            return _spot * dividendFactor * NormalCDF(d1) 
                 - _strike * discountFactor * NormalCDF(d2);
        }
        else
        {
            return _strike * discountFactor * NormalCDF(-d2) 
                 - _spot * dividendFactor * NormalCDF(-d1);
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
    private double CalculateD1(double S, double K, double T)
    {
        if (T < 1e-10)
            return S > K ? 10.0 : -10.0;
        
        double numerator = System.Math.Log(S / K) + (_rate - _dividendYield + 0.5 * _volatility * _volatility) * T;
        return numerator / (_volatility * System.Math.Sqrt(T));
    }
    
    /// <summary>
    /// Standard normal CDF.
    /// </summary>
    private double NormalCDF(double x)
    {
        if (x > 8.0) return 1.0;
        if (x < -8.0) return 0.0;
        return 0.5 * (1.0 + Erf(x / System.Math.Sqrt(2.0)));
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
        
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * System.Math.Exp(-x * x);
        
        return sign * y;
    }
}