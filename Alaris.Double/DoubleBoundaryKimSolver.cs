namespace Alaris.Double;

/// <summary>
/// Refines QD+ boundary approximations using the Kim integral equation with fixed point iteration.
/// Analogous to QuantLib's QdFp algorithm but for double boundaries under negative rates.
/// </summary>
/// <remarks>
/// <para>
/// Implements Healy (2021) Equations 27-29 for the double boundary integral equation.
/// Uses QD+ approximation as initial guess and refines via fixed point iteration.
/// </para>
/// <para>
/// Architecture:
/// - Single boundary: QdFp uses Chebyshev polynomials
/// - Double boundary: KimSolver uses collocation with fixed point iteration
/// </para>
/// <para>
/// Reference: Healy, J. (2021). Section 5.3, Equations 27-29.
/// </para>
/// </remarks>
public sealed class DoubleBoundaryKimSolver
{
    private readonly double _spot;
    private readonly double _strike;
    private readonly double _maturity;
    private readonly double _rate;
    private readonly double _dividendYield;
    private readonly double _volatility;
    private readonly bool _isCall;
    private readonly int _collocationPoints;
    
    private const double TOLERANCE = 1e-6;
    private const int MAX_ITERATIONS = 100;
    private const int INTEGRATION_POINTS = 50;
    private const double NUMERICAL_EPSILON = 1e-10;
    
    /// <summary>
    /// Initializes a new Kim solver for double boundaries.
    /// </summary>
    /// <param name="spot">Current asset price</param>
    /// <param name="strike">Strike price</param>
    /// <param name="maturity">Time to maturity</param>
    /// <param name="rate">Risk-free rate</param>
    /// <param name="dividendYield">Dividend yield</param>
    /// <param name="volatility">Volatility</param>
    /// <param name="isCall">True for call, false for put</param>
    /// <param name="collocationPoints">Number of time discretization points</param>
    public DoubleBoundaryKimSolver(
        double spot,
        double strike,
        double maturity,
        double rate,
        double dividendYield,
        double volatility,
        bool isCall,
        int collocationPoints = 50)
    {
        _spot = spot;
        _strike = strike;
        _maturity = maturity;
        _rate = rate;
        _dividendYield = dividendYield;
        _volatility = volatility;
        _isCall = isCall;
        _collocationPoints = collocationPoints;
    }
    
    /// <summary>
    /// Solves for refined boundaries using Kim's integral equation with fixed point iteration.
    /// </summary>
    /// <param name="upperInitial">Initial upper boundary from QD+</param>
    /// <param name="lowerInitial">Initial lower boundary from QD+</param>
    /// <returns>Refined (upper, lower) boundaries and crossing time</returns>
    public (double[] Upper, double[] Lower, double CrossingTime) SolveBoundaries(
        double upperInitial, double lowerInitial)
    {
        double[] upper = new double[_collocationPoints];
        double[] lower = new double[_collocationPoints];
        
        System.Array.Fill(upper, upperInitial);
        System.Array.Fill(lower, lowerInitial);
        
        double crossingTime = FindCrossingTime(upper, lower);
        
        var (refinedUpper, refinedLower) = RefineUsingFixedPoint(upper, lower, crossingTime);
        
        return (refinedUpper, refinedLower, crossingTime);
    }
    
    /// <summary>
    /// Refines boundaries using fixed point iteration on Kim's integral equations.
    /// </summary>
    private (double[] Upper, double[] Lower) RefineUsingFixedPoint(
        double[] upperInitial, double[] lowerInitial, double crossingTime)
    {
        int m = _collocationPoints;
        double[] upper = (double[])upperInitial.Clone();
        double[] lower = (double[])lowerInitial.Clone();
        
        for (int iter = 0; iter < MAX_ITERATIONS; iter++)
        {
            double maxChange = 0.0;
            
            for (int i = 0; i < m; i++)
            {
                double ti = i * _maturity / (m - 1);
                
                if (ti < crossingTime)
                    continue;
                
                double upperNew = SolveSinglePoint(ti, upper, lower, crossingTime, true);
                double lowerNew = SolveSinglePoint(ti, upper, lower, crossingTime, false);
                
                maxChange = System.Math.Max(maxChange, System.Math.Abs(upperNew - upper[i]));
                maxChange = System.Math.Max(maxChange, System.Math.Abs(lowerNew - lower[i]));
                
                upper[i] = upperNew;
                lower[i] = lowerNew;
            }
            
            if (maxChange < TOLERANCE)
                break;
        }
        
        return (upper, lower);
    }
    
    /// <summary>
    /// Solves Kim equation at a single collocation point (Healy 2021 Equations 28-29).
    /// </summary>
    private double SolveSinglePoint(double ti, double[] upper, double[] lower, 
        double crossingTime, bool isUpperBoundary)
    {
        double eta = _isCall ? 1.0 : -1.0;
        double[] boundary = isUpperBoundary ? upper : lower;
        double Si = boundary[(int)(ti / _maturity * (_collocationPoints - 1))];
        
        double europeanValue = CalculateEuropeanValue(Si, _maturity - ti);
        double integralUpper = CalculateIntegralTerm(Si, ti, upper, lower, crossingTime, true);
        double integralLower = CalculateIntegralTerm(Si, ti, upper, lower, crossingTime, false);
        
        double newBoundary = _strike + eta * (europeanValue + integralUpper - integralLower);
        
        return newBoundary;
    }
    
    /// <summary>
    /// Calculates the integral term in Kim's equation.
    /// </summary>
    private double CalculateIntegralTerm(double S, double ti, double[] upper, double[] lower,
        double crossingTime, bool isUpperBoundary)
    {
        double tStart = System.Math.Max(ti, crossingTime);
        if (tStart >= _maturity)
            return 0.0;
        
        double dt = (_maturity - tStart) / INTEGRATION_POINTS;
        double integral = 0.0;
        
        for (int j = 0; j < INTEGRATION_POINTS; j++)
        {
            double t = tStart + (j + 0.5) * dt;
            double tMinusTi = t - ti;
            
            if (tMinusTi < NUMERICAL_EPSILON)
                continue;
            
            double boundaryValue = InterpolateBoundary(
                isUpperBoundary ? upper : lower, t);
            
            double d1 = CalculateD1(S, boundaryValue, tMinusTi);
            double d2 = CalculateD2(S, boundaryValue, tMinusTi);
            
            double term1 = _rate * _strike * System.Math.Exp(-_rate * tMinusTi) * NormalCDF(-d2);
            double term2 = _dividendYield * S * System.Math.Exp(-_dividendYield * tMinusTi) * NormalCDF(-d1);
            
            integral += (term1 - term2) * dt;
        }
        
        return integral;
    }
    
    /// <summary>
    /// Interpolates boundary value at a given time.
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
    /// Finds crossing time of boundaries.
    /// </summary>
    private double FindCrossingTime(double[] upper, double[] lower)
    {
        for (int i = 0; i < _collocationPoints; i++)
        {
            if (_isCall && upper[i] <= lower[i])
                return i * _maturity / (_collocationPoints - 1);
            if (!_isCall && lower[i] >= upper[i])
                return i * _maturity / (_collocationPoints - 1);
        }
        return _maturity;
    }
    
    /// <summary>
    /// Calculates European option value.
    /// </summary>
    private double CalculateEuropeanValue(double S, double T)
    {
        double d1 = CalculateD1(S, _strike, T);
        double d2 = d1 - _volatility * System.Math.Sqrt(T);
        
        double discountFactor = System.Math.Exp(-_rate * T);
        double dividendFactor = System.Math.Exp(-_dividendYield * T);
        
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
    /// Calculates d₁ for Black-Scholes.
    /// </summary>
    private double CalculateD1(double S, double K, double T)
    {
        if (T < NUMERICAL_EPSILON)
            return S > K ? 10.0 : -10.0;
        
        double numerator = System.Math.Log(S / K) + (_rate - _dividendYield + 0.5 * _volatility * _volatility) * T;
        return numerator / (_volatility * System.Math.Sqrt(T));
    }
    
    /// <summary>
    /// Calculates d₂ for Black-Scholes.
    /// </summary>
    private double CalculateD2(double S, double K, double T)
    {
        return CalculateD1(S, K, T) - _volatility * System.Math.Sqrt(T);
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