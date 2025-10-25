using System;
using System.Linq;

namespace Alaris.Double;

/// <summary>
/// Refines QD+ boundary approximations using the Kim integral equation with FP-B' stabilized fixed point iteration.
/// Implements Healy (2021) Equations 27-29 with the stabilized FP-B' method (Equations 33-35).
/// </summary>
/// <remarks>
/// <para>
/// CRITICAL: Uses FP-B' stabilized iteration instead of basic FP-B to prevent oscillations
/// in the lower boundary for longer maturities. The key difference is that the lower boundary
/// update uses the JUST-COMPUTED upper boundary from the same iteration.
/// </para>
/// <para>
/// Architecture:
/// - Single boundary: QdFp uses Chebyshev polynomials
/// - Double boundary: KimSolver uses collocation with FP-B' fixed point iteration
/// </para>
/// <para>
/// Reference: Healy, J. (2021). Section 5.3, Equations 27-29 (Kim equation for double boundaries)
/// and Equations 33-35 (FP-B' stabilized iteration).
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
    public readonly int _collocationPoints;
    
    private const double TOLERANCE = 1e-6;
    private const int MAX_ITERATIONS = 100;
    private const int INTEGRATION_POINTS = 50;
    private const double NUMERICAL_EPSILON = 1e-10;
    private const double CROSSING_TIME_THRESHOLD = 1e-2; // Healy recommendation: Δt < 10^-2
    
    /// <summary>
    /// Initializes a new Kim solver for double boundaries.
    /// </summary>
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
    /// Solves for refined boundaries using Kim's integral equation with FP-B' stabilized iteration.
    /// </summary>
    /// <param name="upperInitial">Initial upper boundary from QD+</param>
    /// <param name="lowerInitial">Initial lower boundary from QD+</param>
    /// <returns>Refined (upper, lower) boundaries and crossing time</returns>
    public (double[] Upper, double[] Lower, double CrossingTime) SolveBoundaries(
        double upperInitial, double lowerInitial)
    {
        int m = _collocationPoints;
        double[] upper = new double[m];
        double[] lower = new double[m];
        
        // Initialize with QD+ values
        Array.Fill(upper, upperInitial);
        Array.Fill(lower, lowerInitial);
        
        // Find initial crossing time estimate
        double crossingTime = FindCrossingTime(upper, lower);
        
        // Refine crossing time by subdivision (Healy p.12)
        crossingTime = RefineCrossingTime(upper, lower, crossingTime);
        
        // Adjust initial guess if boundaries cross (Healy p.12 procedure)
        AdjustCrossingInitialGuess(upper, lower, crossingTime);
        
        // Refine using FP-B' stabilized iteration
        var (refinedUpper, refinedLower) = RefineUsingFpbPrime(upper, lower, crossingTime);
        
        return (refinedUpper, refinedLower, crossingTime);
    }
    
    /// <summary>
    /// Refines boundaries using FP-B' stabilized fixed point iteration (Healy Equations 33-35).
    /// </summary>
    /// <remarks>
    /// FP-B' differs from FP-B in that the lower boundary update uses the JUST-COMPUTED
    /// upper boundary from the same iteration: l^j_i = f(l^(j-1), u^j) not f(l^(j-1), u^(j-1)).
    /// This prevents oscillations and ensures convergence for longer maturities.
    /// </remarks>
    private (double[] Upper, double[] Lower) RefineUsingFpbPrime(
        double[] upperInitial, double[] lowerInitial, double crossingTime)
    {
        int m = _collocationPoints;
        double[] upper = (double[])upperInitial.Clone();
        double[] lower = (double[])lowerInitial.Clone();
        
        for (int iter = 0; iter < MAX_ITERATIONS; iter++)
        {
            double maxChange = 0.0;
            double[] upperNew = new double[m];
            double[] lowerNew = new double[m];
            
            // FP-B' iteration (Equation 33)
            for (int i = 0; i < m; i++)
            {
                double ti = i * _maturity / (m - 1);
                
                // Skip points before crossing time
                if (ti < crossingTime - NUMERICAL_EPSILON)
                {
                    upperNew[i] = upper[i];
                    lowerNew[i] = lower[i];
                    continue;
                }
                
                // Update upper boundary using FP-B (Equations 30-32)
                upperNew[i] = SolveUpperBoundaryPoint(ti, upper, lower, crossingTime);
                
                // CRITICAL: Update lower boundary using FP-B' (Equations 33-35)
                // Uses the JUST-COMPUTED upper boundary (upperNew) instead of old value
                double[] tempUpper = (double[])upper.Clone();
                tempUpper[i] = upperNew[i]; // Use new upper value
                lowerNew[i] = SolveLowerBoundaryPointStabilized(ti, lower, tempUpper, crossingTime);
                
                // Enforce constraints
                (upperNew[i], lowerNew[i]) = EnforceConstraints(upperNew[i], lowerNew[i], ti);
                
                maxChange = Math.Max(maxChange, Math.Abs(upperNew[i] - upper[i]));
                maxChange = Math.Max(maxChange, Math.Abs(lowerNew[i] - lower[i]));
            }
            
            upper = upperNew;
            lower = lowerNew;
            
            if (maxChange < TOLERANCE)
                break;
        }
        
        return (upper, lower);
    }
    
    /// <summary>
    /// Solves for upper boundary at single point using FP-B (Healy Equations 30-32).
    /// </summary>
    /// <remarks>
    /// u^j_i = K * N(t_i, u^(j-1), l^(j-1)) / D(t_i, u^(j-1), l^(j-1))
    /// </remarks>
    private double SolveUpperBoundaryPoint(double ti, double[] upper, double[] lower, double crossingTime)
    {
        double Ni = CalculateNumerator(ti, upper, lower, crossingTime, true);
        double Di = CalculateDenominator(ti, upper, lower, crossingTime, true);
        
        if (Math.Abs(Di) < NUMERICAL_EPSILON)
            return InterpolateBoundary(upper, ti);
        
        return _strike * Ni / Di;
    }
    
    /// <summary>
    /// Solves for lower boundary using FP-B' stabilized method (Healy Equations 33-35).
    /// </summary>
    /// <remarks>
    /// CRITICAL: l^j_i = K * N'(t_i, l^(j-1), u^j) / D'(t_i, l^(j-1), u^j)
    /// Note the use of u^j (just-computed upper) instead of u^(j-1).
    /// </remarks>
    private double SolveLowerBoundaryPointStabilized(
        double ti, double[] lowerOld, double[] upperNew, double crossingTime)
    {
        // Calculate N' (Equation 34) - includes additional term
        double NiPrime = CalculateNumeratorPrime(ti, lowerOld, upperNew, crossingTime);
        
        // Calculate D' (Equation 35) - simplified form
        double DiPrime = CalculateDenominatorPrime(ti, lowerOld, crossingTime);
        
        if (Math.Abs(DiPrime) < NUMERICAL_EPSILON)
            return InterpolateBoundary(lowerOld, ti);
        
        return _strike * NiPrime / DiPrime;
    }
    
    /// <summary>
    /// Calculates numerator N for FP-B (Healy Equation 31).
    /// </summary>
    private double CalculateNumerator(double ti, double[] upper, double[] lower, 
        double crossingTime, bool isUpper)
    {
        double[] boundary = isUpper ? upper : lower;
        double Bi = InterpolateBoundary(boundary, ti);
        
        // Non-integral term
        double tauToMaturity = _maturity - ti;
        double d2Terminal = CalculateD2(Bi, _strike, tauToMaturity);
        double nonIntegral = 1.0 - Math.Exp(-_rate * tauToMaturity) * NormalCDF(-d2Terminal);
        
        // Integral term (Equation 27 structure)
        double integral = CalculateIntegralTermN(ti, Bi, upper, lower, crossingTime);
        
        return nonIntegral - integral;
    }
    
    /// <summary>
    /// Calculates denominator D for FP-B (Healy Equation 32).
    /// </summary>
    private double CalculateDenominator(double ti, double[] upper, double[] lower,
        double crossingTime, bool isUpper)
    {
        double[] boundary = isUpper ? upper : lower;
        double Bi = InterpolateBoundary(boundary, ti);
        
        // Non-integral term
        double tauToMaturity = _maturity - ti;
        double d1Terminal = CalculateD1(Bi, _strike, tauToMaturity);
        double nonIntegral = 1.0 - Math.Exp(-_dividendYield * tauToMaturity) * NormalCDF(-d1Terminal);
        
        // Integral term
        double integral = CalculateIntegralTermD(ti, Bi, upper, lower, crossingTime);
        
        return nonIntegral - integral;
    }
    
    /// <summary>
    /// Calculates modified numerator N' for FP-B' (Healy Equation 34).
    /// </summary>
    private double CalculateNumeratorPrime(double ti, double[] lower, double[] upper, double crossingTime)
    {
        double Bi = InterpolateBoundary(lower, ti);
        
        // Standard N term
        double N = CalculateNumerator(ti, upper, lower, crossingTime, false);
        
        // Additional stabilization term: (B^l_i/K) * integral
        double additionalTerm = (Bi / _strike) * CalculateIntegralTermD(ti, Bi, upper, lower, crossingTime);
        
        return N + additionalTerm;
    }
    
    /// <summary>
    /// Calculates simplified denominator D' for FP-B' (Healy Equation 35).
    /// </summary>
    private double CalculateDenominatorPrime(double ti, double[] lower, double crossingTime)
    {
        double Bi = InterpolateBoundary(lower, ti);
        
        // Simplified form - only non-integral term
        double tauToMaturity = _maturity - ti;
        double d1Terminal = CalculateD1(Bi, _strike, tauToMaturity);
        
        return 1.0 - Math.Exp(-_dividendYield * tauToMaturity) * NormalCDF(-d1Terminal);
    }
    
    /// <summary>
    /// Calculates integral term for numerator (r-weighted).
    /// </summary>
    private double CalculateIntegralTermN(double ti, double Bi, double[] upper, double[] lower, 
        double crossingTime)
    {
        double integral = 0.0;
        double tStart = Math.Max(ti, crossingTime);
        
        if (tStart >= _maturity)
            return 0.0;
        
        // Trapezoidal integration
        int nSteps = INTEGRATION_POINTS;
        double dt = (_maturity - tStart) / nSteps;
        
        for (int j = 0; j <= nSteps; j++)
        {
            double t = tStart + j * dt;
            double upperVal = InterpolateBoundary(upper, t);
            double lowerVal = InterpolateBoundary(lower, t);
            
            double tau = t - ti;
            double d2Upper = CalculateD2(Bi, upperVal, tau);
            double d2Lower = CalculateD2(Bi, lowerVal, tau);
            
            double integrand = _rate * Math.Exp(-_rate * tau) * 
                              (NormalCDF(-d2Upper) - NormalCDF(-d2Lower));
            
            // Trapezoidal rule weights
            double weight = (j == 0 || j == nSteps) ? 0.5 : 1.0;
            integral += weight * integrand * dt;
        }
        
        return integral;
    }
    
    /// <summary>
    /// Calculates integral term for denominator (q-weighted).
    /// </summary>
    private double CalculateIntegralTermD(double ti, double Bi, double[] upper, double[] lower,
        double crossingTime)
    {
        double integral = 0.0;
        double tStart = Math.Max(ti, crossingTime);
        
        if (tStart >= _maturity)
            return 0.0;
        
        // Trapezoidal integration
        int nSteps = INTEGRATION_POINTS;
        double dt = (_maturity - tStart) / nSteps;
        
        for (int j = 0; j <= nSteps; j++)
        {
            double t = tStart + j * dt;
            double upperVal = InterpolateBoundary(upper, t);
            double lowerVal = InterpolateBoundary(lower, t);
            
            double tau = t - ti;
            double d1Upper = CalculateD1(Bi, upperVal, tau);
            double d1Lower = CalculateD1(Bi, lowerVal, tau);
            
            double integrand = _dividendYield * Math.Exp(-_dividendYield * tau) * 
                              (NormalCDF(-d1Upper) - NormalCDF(-d1Lower));
            
            // Trapezoidal rule weights
            double weight = (j == 0 || j == nSteps) ? 0.5 : 1.0;
            integral += weight * integrand * dt;
        }
        
        return integral;
    }
    
    /// <summary>
    /// Finds initial crossing time estimate.
    /// </summary>
    private double FindCrossingTime(double[] upper, double[] lower)
    {
        // Find where boundaries cross
        for (int i = 1; i < _collocationPoints; i++)
        {
            if (upper[i] <= lower[i])
            {
                return i * _maturity / (_collocationPoints - 1);
            }
        }
        
        return 0.0; // No crossing
    }
    
    /// <summary>
    /// Refines crossing time estimate to achieve Δt < threshold.
    /// </summary>
    private double RefineCrossingTime(double[] upper, double[] lower, double initialCrossing)
    {
        if (initialCrossing <= 0 || initialCrossing >= _maturity)
            return initialCrossing;
        
        // Binary search refinement
        double left = Math.Max(0, initialCrossing - 0.1);
        double right = Math.Min(_maturity, initialCrossing + 0.1);
        
        while (right - left > CROSSING_TIME_THRESHOLD)
        {
            double mid = (left + right) / 2.0;
            double upperVal = InterpolateBoundary(upper, mid);
            double lowerVal = InterpolateBoundary(lower, mid);
            
            if (upperVal > lowerVal)
            {
                left = mid;
            }
            else
            {
                right = mid;
            }
        }
        
        return (left + right) / 2.0;
    }
    
    /// <summary>
    /// Adjusts initial guess when boundaries cross (Healy p.12).
    /// </summary>
    private void AdjustCrossingInitialGuess(double[] upper, double[] lower, double crossingTime)
    {
        if (crossingTime <= 0 || crossingTime >= _maturity)
            return;
        
        // Find crossing index
        int crossingIndex = (int)(crossingTime / _maturity * (_collocationPoints - 1));
        
        // Set boundaries equal at and before crossing
        double crossingValue = (upper[crossingIndex] + lower[crossingIndex]) / 2.0;
        
        for (int i = 0; i <= crossingIndex; i++)
        {
            upper[i] = crossingValue;
            lower[i] = crossingValue;
        }
    }
    
    /// <summary>
    /// Enforces boundary constraints.
    /// </summary>
    private (double Upper, double Lower) EnforceConstraints(double upper, double lower, double ti)
    {
        if (!_isCall)
        {
            // Put constraints
            upper = Math.Min(upper, _strike);
            lower = Math.Max(lower, 0.0);
            
            // Ensure ordering
            if (lower >= upper)
            {
                double mid = (upper + lower) / 2.0;
                upper = mid + NUMERICAL_EPSILON;
                lower = mid - NUMERICAL_EPSILON;
            }
        }
        else
        {
            // Call constraints
            upper = Math.Max(upper, _strike);
            lower = Math.Max(lower, 0.0);
            
            // Ensure ordering
            if (lower >= upper)
            {
                double mid = (upper + lower) / 2.0;
                upper = mid + NUMERICAL_EPSILON;
                lower = mid - NUMERICAL_EPSILON;
            }
        }
        
        return (upper, lower);
    }
    
    /// <summary>
    /// Interpolates boundary value at given time.
    /// </summary>
    private double InterpolateBoundary(double[] boundary, double t)
    {
        if (boundary.Length == 1)
            return boundary[0];
        
        double index = t / _maturity * (boundary.Length - 1);
        int i0 = (int)Math.Floor(index);
        int i1 = Math.Min(i0 + 1, boundary.Length - 1);
        double alpha = index - i0;
        
        return boundary[i0] * (1.0 - alpha) + boundary[i1] * alpha;
    }
    
    /// <summary>
    /// Calculates d1 for Black-Scholes formula.
    /// </summary>
    private double CalculateD1(double S, double K, double tau)
    {
        if (tau <= NUMERICAL_EPSILON)
            return 0.0;
        
        double sigma = _volatility;
        double r = _rate;
        double q = _dividendYield;
        
        return (Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * tau) / (sigma * Math.Sqrt(tau));
    }
    
    /// <summary>
    /// Calculates d2 for Black-Scholes formula.
    /// </summary>
    private double CalculateD2(double S, double K, double tau)
    {
        if (tau <= NUMERICAL_EPSILON)
            return 0.0;
        
        return CalculateD1(S, K, tau) - _volatility * Math.Sqrt(tau);
    }
    
    /// <summary>
    /// Normal cumulative distribution function.
    /// </summary>
    private static double NormalCDF(double x)
    {
        return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
    }
    
    /// <summary>
    /// Error function approximation.
    /// </summary>
    private static double Erf(double x)
    {
        // Abramowitz and Stegun approximation
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;
        
        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);
        
        double t = 1.0 / (1.0 + p * x);
        double t2 = t * t;
        double t3 = t2 * t;
        double t4 = t3 * t;
        double t5 = t4 * t;
        
        double y = 1.0 - (a1 * t + a2 * t2 + a3 * t3 + a4 * t4 + a5 * t5) * Math.Exp(-x * x);
        
        return sign * y;
    }
}