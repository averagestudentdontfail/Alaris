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
    private readonly int _collocationPoints;
    
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
    /// Solves for refined boundaries using Kim's integral equation with FP-B' stabilized fixed point iteration.
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
        System.Array.Fill(upper, upperInitial);
        System.Array.Fill(lower, lowerInitial);
        
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
            
            // FP-B' iteration (Equation 33)
            for (int i = 0; i < m; i++)
            {
                double ti = i * _maturity / (m - 1);
                
                // Skip points before crossing time
                if (ti < crossingTime)
                    continue;
                
                // Update upper boundary using FP-B (Equation 30-32)
                double upperNew = SolveUpperBoundaryPoint(ti, upper, lower, crossingTime);
                
                // CRITICAL: Update lower boundary using FP-B' (Equations 33-35)
                // Uses the JUST-COMPUTED upper boundary (upperNew) instead of old value
                double lowerNew = SolveLowerBoundaryPointStabilized(ti, lower, upper, upperNew, crossingTime);
                
                // Enforce constraints
                (upperNew, lowerNew) = EnforceConstraints(upperNew, lowerNew, ti);
                
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
    /// Solves for upper boundary at single point using FP-B (Healy Equations 30-32).
    /// </summary>
    /// <remarks>
    /// u^j_i = K * N(t_i, u^(j-1), l^(j-1)) / D(t_i, u^(j-1), l^(j-1))
    /// </remarks>
    private double SolveUpperBoundaryPoint(double ti, double[] upper, double[] lower, double crossingTime)
    {
        double Ni = CalculateNumerator(ti, upper, lower, crossingTime, true);
        double Di = CalculateDenominator(ti, upper, lower, crossingTime, true);
        
        if (System.Math.Abs(Di) < NUMERICAL_EPSILON)
            return InterpolateBoundary(upper, ti);
        
        return _strike * Ni / Di;
    }
    
    /// <summary>
    /// Solves for lower boundary using FP-B' stabilized method (Healy Equations 33-35).
    /// </summary>
    /// <remarks>
    /// CRITICAL: l^j_i = K * N'(t_i, l^(j-1), u^j) / D'(t_i, l^(j-1), u^j)
    /// Note the use of u^j (just-computed upper) instead of u^(j-1).
    /// 
    /// N' and D' are modified (Equations 34-35):
    /// - N' includes additional term: + (B^u_i/K) * ∫ q*exp(-q(t-ti)) * [Φ(...) - Φ(...)] dt
    /// - D' is simplified: = 1 - exp(-q(T-ti)) * Φ(-d1(B^u_i, K, T-ti))
    /// </remarks>
    private double SolveLowerBoundaryPointStabilized(
        double ti, double[] lowerOld, double[] upperOld, double upperNew, double crossingTime)
    {
        // Build temporary upper array with the new value
        double[] upperCurrent = (double[])upperOld.Clone();
        int idx = (int)(ti / _maturity * (_collocationPoints - 1));
        if (idx >= 0 && idx < upperCurrent.Length)
            upperCurrent[idx] = upperNew;
        
        // Calculate N' (Equation 34) - includes additional term
        double NiPrime = CalculateNumeratorPrime(ti, lowerOld, upperCurrent, crossingTime);
        
        // Calculate D' (Equation 35) - simplified form
        double DiPrime = CalculateDenominatorPrime(ti, lowerOld, crossingTime);
        
        if (System.Math.Abs(DiPrime) < NUMERICAL_EPSILON)
            return InterpolateBoundary(lowerOld, ti);
        
        return _strike * NiPrime / DiPrime;
    }
    
    /// <summary>
    /// Calculates numerator N for FP-B (Healy Equation 31).
    /// </summary>
    /// <remarks>
    /// N(t_i, B^u, B^l) = 1 - exp(-r(T-t_i))*Φ(-d2(B^u_i, K, T-t_i))
    ///                    - ∫[max(t_i,t_s) to T] r*exp(-r(t-t_i)) * [Φ(-d2(B^u_i, B^u_t, t-t_i)) - Φ(-d2(B^u_i, B^l_t, t-t_i))] dt
    /// </remarks>
    private double CalculateNumerator(double ti, double[] upper, double[] lower, 
        double crossingTime, bool isUpper)
    {
        double[] boundary = isUpper ? upper : lower;
        double Bi = InterpolateBoundary(boundary, ti);
        
        // Non-integral term
        double tauToMaturity = _maturity - ti;
        double d2Terminal = CalculateD2(Bi, _strike, tauToMaturity);
        double nonIntegral = 1.0 - System.Math.Exp(-_rate * tauToMaturity) * NormalCDF(-d2Terminal);
        
        // Integral term (Equation 27 structure)
        double integral = CalculateIntegralTermN(ti, Bi, upper, lower, crossingTime);
        
        return nonIntegral - integral;
    }
    
    /// <summary>
    /// Calculates denominator D for FP-B (Healy Equation 32).
    /// </summary>
    /// <remarks>
    /// D(t_i, B^u, B^l) = 1 - exp(-q(T-t_i))*Φ(-d1(B^u_i, K, T-t_i))
    ///                    - ∫[max(t_i,t_s) to T] q*exp(-q(t-t_i)) * [Φ(-d1(B^u_i, B^u_t, t-t_i)) - Φ(-d1(B^u_i, B^l_t, t-t_i))] dt
    /// </remarks>
    private double CalculateDenominator(double ti, double[] upper, double[] lower,
        double crossingTime, bool isUpper)
    {
        double[] boundary = isUpper ? upper : lower;
        double Bi = InterpolateBoundary(boundary, ti);
        
        // Non-integral term
        double tauToMaturity = _maturity - ti;
        double d1Terminal = CalculateD1(Bi, _strike, tauToMaturity);
        double nonIntegral = 1.0 - System.Math.Exp(-_dividendYield * tauToMaturity) * NormalCDF(-d1Terminal);
        
        // Integral term
        double integral = CalculateIntegralTermD(ti, Bi, upper, lower, crossingTime);
        
        return nonIntegral - integral;
    }
    
    /// <summary>
    /// Calculates modified numerator N' for FP-B' (Healy Equation 34).
    /// </summary>
    /// <remarks>
    /// N'(t_i, B^u, B^l) = N(t_i, B^u, B^l) 
    ///                     + (B^u_i/K) * ∫[max(t_i,t_s) to T] q*exp(-q(t-t_i)) * [Φ(-d1(B^u_i, B^u_t, t-t_i)) - Φ(-d1(B^u_i, B^l_t, t-t_i))] dt
    /// 
    /// The additional term restores stability for the lower boundary update.
    /// </remarks>
    private double CalculateNumeratorPrime(double ti, double[] lower, double[] upper, double crossingTime)
    {
        double Bi = InterpolateBoundary(lower, ti);
        
        // Standard N term
        double N = CalculateNumerator(ti, upper, lower, crossingTime, false);
        
        // Additional stabilization term: (B^u_i/K) * integral
        double additionalTerm = (Bi / _strike) * CalculateIntegralTermD(ti, Bi, upper, lower, crossingTime);
        
        return N + additionalTerm;
    }
    
    /// <summary>
    /// Calculates simplified denominator D' for FP-B' (Healy Equation 35).
    /// </summary>
    /// <remarks>
    /// D'(t_i, B^u, B^l) = 1 - exp(-q(T-t_i)) * Φ(-d1(B^u_i, K, T-t_i))
    /// 
    /// Notice the integral term is REMOVED in the FP-B' formulation.
    /// </remarks>
    private double CalculateDenominatorPrime(double ti, double[] lower, double crossingTime)
    {
        double Bi = InterpolateBoundary(lower, ti);
        
        // Simplified form - only non-integral term
        double tauToMaturity = _maturity - ti;
        double d1Terminal = CalculateD1(Bi, _strike, tauToMaturity);
        
        return 1.0 - System.Math.Exp(-_dividendYield * tauToMaturity) * NormalCDF(-d1Terminal);
    }
    
    /// <summary>
    /// Calculates integral term for numerator (r-weighted).
    /// </summary>
    private double CalculateIntegralTermN(double ti, double Bi, double[] upper, double[] lower, double crossingTime)
    {
        double tStart = System.Math.Max(ti, crossingTime);
        if (tStart >= _maturity)
            return 0.0;
        
        double dt = (_maturity - tStart) / INTEGRATION_POINTS;
        double integral = 0.0;
        
        for (int j = 0; j < INTEGRATION_POINTS; j++)
        {
            double t = tStart + (j + 0.5) * dt;
            double tau = t - ti;
            
            if (tau < NUMERICAL_EPSILON)
                continue;
            
            double Bu_t = InterpolateBoundary(upper, t);
            double Bl_t = InterpolateBoundary(lower, t);
            
            double d2_upper = CalculateD2(Bi, Bu_t, tau);
            double d2_lower = CalculateD2(Bi, Bl_t, tau);
            
            double phi_diff = NormalCDF(-d2_upper) - NormalCDF(-d2_lower);
            double weight = _rate * System.Math.Exp(-_rate * tau);
            
            integral += weight * phi_diff * dt;
        }
        
        return integral;
    }
    
    /// <summary>
    /// Calculates integral term for denominator (q-weighted).
    /// </summary>
    private double CalculateIntegralTermD(double ti, double Bi, double[] upper, double[] lower, double crossingTime)
    {
        double tStart = System.Math.Max(ti, crossingTime);
        if (tStart >= _maturity)
            return 0.0;
        
        double dt = (_maturity - tStart) / INTEGRATION_POINTS;
        double integral = 0.0;
        
        for (int j = 0; j < INTEGRATION_POINTS; j++)
        {
            double t = tStart + (j + 0.5) * dt;
            double tau = t - ti;
            
            if (tau < NUMERICAL_EPSILON)
                continue;
            
            double Bu_t = InterpolateBoundary(upper, t);
            double Bl_t = InterpolateBoundary(lower, t);
            
            double d1_upper = CalculateD1(Bi, Bu_t, tau);
            double d1_lower = CalculateD1(Bi, Bl_t, tau);
            
            double phi_diff = NormalCDF(-d1_upper) - NormalCDF(-d1_lower);
            double weight = _dividendYield * System.Math.Exp(-_dividendYield * tau);
            
            integral += weight * phi_diff * dt;
        }
        
        return integral;
    }
    
    /// <summary>
    /// Finds initial crossing time estimate.
    /// </summary>
    private double FindCrossingTime(double[] upper, double[] lower)
    {
        int m = _collocationPoints;
        
        for (int i = m - 1; i >= 0; i--)
        {
            double ti = i * _maturity / (m - 1);
            
            if (_isCall)
            {
                if (upper[i] <= lower[i])
                    return ti;
            }
            else
            {
                if (lower[i] >= upper[i])
                    return ti;
            }
        }
        
        return 0.0; // No crossing
    }
    
    /// <summary>
    /// Refines crossing time by subdivision (Healy p.12: Δt &lt; 10^-2).
    /// </summary>
    private double RefineCrossingTime(double[] upper, double[] lower, double initialGuess)
    {
        if (initialGuess <= 0.0)
            return 0.0;
        
        int m = _collocationPoints;
        double dt = _maturity / (m - 1);
        
        // Find the interval containing the crossing
        int idx = (int)(initialGuess / dt);
        if (idx <= 0 || idx >= m - 1)
            return initialGuess;
        
        double tLeft = idx * dt;
        double tRight = (idx + 1) * dt;
        
        // Binary search for crossing point
        while (tRight - tLeft > CROSSING_TIME_THRESHOLD)
        {
            double tMid = (tLeft + tRight) / 2.0;
            
            double upperMid = InterpolateBoundary(upper, tMid);
            double lowerMid = InterpolateBoundary(lower, tMid);
            
            bool crossesAtMid = _isCall ? (upperMid <= lowerMid) : (lowerMid >= upperMid);
            
            if (crossesAtMid)
                tRight = tMid;
            else
                tLeft = tMid;
        }
        
        return (tLeft + tRight) / 2.0;
    }
    
    /// <summary>
    /// Adjusts initial guess when boundaries cross (Healy p.12 procedure).
    /// </summary>
    private void AdjustCrossingInitialGuess(double[] upper, double[] lower, double crossingTime)
    {
        if (crossingTime <= 0.0)
            return;
        
        int m = _collocationPoints;
        int crossingIdx = (int)(crossingTime / _maturity * (m - 1));
        
        if (crossingIdx <= 0)
            return;
        
        // Find adjustment value
        double cStar = _isCall 
            ? System.Math.Max(upper[crossingIdx], lower[crossingIdx + 1])
            : System.Math.Min(System.Math.Max(upper[crossingIdx], lower[crossingIdx + 1]), lower[crossingIdx + 1]);
        
        // Set both boundaries to cStar before crossing time
        for (int i = 0; i <= crossingIdx; i++)
        {
            upper[i] = cStar;
            lower[i] = cStar;
        }
    }
    
    /// <summary>
    /// Enforces monotonicity and ordering constraints.
    /// </summary>
    private (double Upper, double Lower) EnforceConstraints(double upper, double lower, double ti)
    {
        if (_isCall)
        {
            // Call: upper must be above lower, both above strike
            if (upper < _strike) upper = _strike * 1.01;
            if (lower < _strike) lower = _strike;
            if (upper <= lower) upper = lower * 1.01;
        }
        else
        {
            // Put: lower must be below upper, both below strike
            if (upper > _strike) upper = _strike;
            if (lower > _strike) lower = _strike * 0.99;
            if (lower >= upper) lower = upper * 0.99;
        }
        
        return (upper, lower);
    }
    
    /// <summary>
    /// Interpolates boundary value at arbitrary time.
    /// </summary>
    private double InterpolateBoundary(double[] boundary, double t)
    {
        int m = boundary.Length;
        double dt = _maturity / (m - 1);
        int i = (int)(t / dt);
        
        if (i >= m - 1) return boundary[m - 1];
        if (i < 0) return boundary[0];
        
        double alpha = (t - i * dt) / dt;
        return boundary[i] * (1.0 - alpha) + boundary[i + 1] * alpha;
    }
    
    /// <summary>
    /// Calculates d₁ = (ln(S/K) + (r - q + 0.5σ²)τ) / (σ√τ)
    /// </summary>
    private double CalculateD1(double S, double K, double tau)
    {
        if (tau < NUMERICAL_EPSILON)
            return S > K ? 10.0 : -10.0;
        
        if (S <= 0.0 || K <= 0.0)
            return -10.0;
        
        double sqrtTau = System.Math.Sqrt(tau);
        double numerator = System.Math.Log(S / K) + 
                          (_rate - _dividendYield + 0.5 * _volatility * _volatility) * tau;
        return numerator / (_volatility * sqrtTau);
    }
    
    /// <summary>
    /// Calculates d₂ = d₁ - σ√τ
    /// </summary>
    private double CalculateD2(double S, double K, double tau)
    {
        double d1 = CalculateD1(S, K, tau);
        return d1 - _volatility * System.Math.Sqrt(System.Math.Max(0.0, tau));
    }
    
    /// <summary>
    /// Standard normal cumulative distribution function.
    /// </summary>
    private double NormalCDF(double x)
    {
        if (x > 8.0) return 1.0;
        if (x < -8.0) return 0.0;
        return 0.5 * (1.0 + Erf(x / System.Math.Sqrt(2.0)));
    }
    
    /// <summary>
    /// Error function approximation.
    /// </summary>
    private double Erf(double x)
    {
        double a1 =  0.254829592;
        double a2 = -0.284496736;
        double a3 =  1.421413741;
        double a4 = -1.453152027;
        double a5 =  1.061405429;
        double p  =  0.3275911;
        
        int sign = x < 0 ? -1 : 1;
        x = System.Math.Abs(x);
        
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * System.Math.Exp(-x * x);
        
        return sign * y;
    }
}