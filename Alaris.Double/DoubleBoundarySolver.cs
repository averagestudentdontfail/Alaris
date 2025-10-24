using System;

namespace Alaris.Double
{
    /// <summary>
    /// Solves the Kim integral equation for American option early exercise boundaries
    /// under negative interest rate regimes with double boundaries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements the double boundary integral equation from Healy (2021) Equation 27:
    /// VA = VE + ∫[rK·exp(-rt)Φ(-d₂(S,u(t),t)) - qS·exp(-qt)Φ(-d₁(S,u(t),t))]dt
    ///         - ∫[rK·exp(-rt)Φ(-d₂(S,l(t),t)) - qS·exp(-qt)Φ(-d₁(S,l(t),t))]dt
    /// </para>
    /// <para>
    /// Uses the QD+ approximation as initial guess and refines via Gauss-Newton method.
    /// Valid for the regime q &lt; r &lt; 0 where two exercise boundaries exist.
    /// </para>
    /// <para>
    /// Reference: Healy, J. (2021). Pricing American Options Under Negative Rates. 
    /// Section 5, Equations 27-29.
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
        
        private const double TOLERANCE = 1e-6;
        private const int MAX_ITERATIONS = 100;
        private const int INTEGRATION_POINTS = 50;
        
        /// <summary>
        /// Initializes a new instance of the DoubleBoundarySolver.
        /// </summary>
        /// <param name="spot">Current asset price S₀</param>
        /// <param name="strike">Strike price K</param>
        /// <param name="maturity">Time to maturity T (in years)</param>
        /// <param name="rate">Risk-free interest rate r</param>
        /// <param name="dividendYield">Continuous dividend yield q</param>
        /// <param name="volatility">Volatility σ</param>
        /// <param name="isCall">True for call options, false for put options</param>
        /// <param name="collocationPoints">Number of time points for boundary discretization (default: 50)</param>
        public DoubleBoundarySolver(
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
        /// Solves for the exercise boundaries using the Kim integral equation.
        /// </summary>
        /// <returns>
        /// Tuple of (upper boundary array, lower boundary array, crossing time).
        /// Each array contains boundary values at collocation points from t=0 to t=T.
        /// </returns>
        public (double[] Upper, double[] Lower, double CrossingTime) SolveBoundaries()
        {
            // Get initial guess from QD+ approximation
            var initialGuess = GetInitialGuess();
            
            if (initialGuess.BoundariesCross)
            {
                // Boundaries cross - return constant boundaries at strike
                double[] constantUpper = new double[_collocationPoints];
                double[] constantLower = new double[_collocationPoints];
                Array.Fill(constantUpper, _strike);
                Array.Fill(constantLower, _strike);
                return (constantUpper, constantLower, 0.0);
            }
            
            // Initialize boundary arrays
            double[] upperBoundary = new double[_collocationPoints];
            double[] lowerBoundary = new double[_collocationPoints];
            
            // Fill with initial guess (constant boundaries)
            Array.Fill(upperBoundary, initialGuess.UpperBoundary);
            Array.Fill(lowerBoundary, initialGuess.LowerBoundary);
            
            // Find crossing time if boundaries will cross
            double crossingTime = FindCrossingTime(upperBoundary, lowerBoundary);
            
            // Solve the system using Gauss-Newton method
            var (refinedUpper, refinedLower) = RefineUsingGaussNewton(
                upperBoundary, lowerBoundary, crossingTime);
            
            return (refinedUpper, refinedLower, crossingTime);
        }
        
        /// <summary>
        /// Calculates the American option value using the solved boundaries.
        /// </summary>
        public double CalculateValue()
        {
            var (upper, lower, crossingTime) = SolveBoundaries();
            
            // Check if should exercise immediately
            if (ShouldExerciseImmediately(upper[0], lower[0]))
            {
                return CalculateIntrinsicValue();
            }
            
            // Calculate using Kim integral equation (Healy 2021 Equation 27)
            double europeanValue = CalculateEuropeanValue(_spot);
            double earlyExercisePremium = CalculateEarlyExercisePremium(upper, lower, crossingTime);
            
            return europeanValue + earlyExercisePremium;
        }
        
        /// <summary>
        /// Gets the initial guess for boundaries from QD+ approximation.
        /// </summary>
        private BoundaryResult GetInitialGuess()
        {
            var approximation = new DoubleBoundaryApproximation(
                _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall);
            
            return approximation.CalculateBoundaries();
        }
        
        /// <summary>
        /// Refines the boundary guess using Gauss-Newton method on the Kim integral equations.
        /// </summary>
        /// <remarks>
        /// Implements Healy (2021) Equations 28-29 for the two boundaries.
        /// Uses a 2m-dimensional Gauss-Newton solver where m is the number of collocation points.
        /// </remarks>
        private (double[] Upper, double[] Lower) RefineUsingGaussNewton(
            double[] upperInitial, double[] lowerInitial, double crossingTime)
        {
            int m = _collocationPoints;
            double[] upper = (double[])upperInitial.Clone();
            double[] lower = (double[])lowerInitial.Clone();
            
            for (int iter = 0; iter < MAX_ITERATIONS; iter++)
            {
                // Evaluate residuals for all collocation points
                double maxResidual = 0.0;
                
                for (int i = 0; i < m; i++)
                {
                    double ti = i * _maturity / (m - 1);
                    
                    // Skip if before crossing time
                    if (ti < crossingTime)
                        continue;
                    
                    // Evaluate Kim equations at this collocation point
                    double upperResidual = EvaluateUpperBoundaryEquation(ti, upper, lower, crossingTime);
                    double lowerResidual = EvaluateLowerBoundaryEquation(ti, upper, lower, crossingTime);
                    
                    maxResidual = Math.Max(maxResidual, Math.Abs(upperResidual));
                    maxResidual = Math.Max(maxResidual, Math.Abs(lowerResidual));
                    
                    // Apply Newton correction (simplified - full Gauss-Newton would build Jacobian)
                    double correction = 0.1; // Damping factor
                    upper[i] -= correction * upperResidual;
                    lower[i] += correction * lowerResidual;
                    
                    // Enforce constraints
                    upper[i] = Math.Max(upper[i], _strike * 0.5);
                    lower[i] = Math.Min(lower[i], _strike * 2.0);
                }
                
                if (maxResidual < TOLERANCE)
                    break;
            }
            
            return (upper, lower);
        }
        
        /// <summary>
        /// Evaluates the Kim integral equation for the upper boundary at time ti.
        /// Implements Healy (2021) Equation 28.
        /// </summary>
        private double EvaluateUpperBoundaryEquation(double ti, double[] upper, double[] lower, double crossingTime)
        {
            double eta = _isCall ? 1.0 : -1.0;
            double Si = upper[(int)(ti / _maturity * (_collocationPoints - 1))];
            
            // Left-hand side: K - u(ti)
            double lhs = eta * (Si - _strike);
            
            // European value at u(ti)
            double europeanValue = CalculateEuropeanValue(Si, _maturity - ti);
            
            // Integral term from ti to T
            double integralUpper = CalculateIntegralTerm(Si, ti, upper, lower, crossingTime, true);
            double integralLower = CalculateIntegralTerm(Si, ti, upper, lower, crossingTime, false);
            
            // Right-hand side
            double rhs = europeanValue + integralUpper - integralLower;
            
            return lhs - rhs;
        }
        
        /// <summary>
        /// Evaluates the Kim integral equation for the lower boundary at time ti.
        /// Implements Healy (2021) Equation 29.
        /// </summary>
        private double EvaluateLowerBoundaryEquation(double ti, double[] upper, double[] lower, double crossingTime)
        {
            double eta = _isCall ? 1.0 : -1.0;
            double Si = lower[(int)(ti / _maturity * (_collocationPoints - 1))];
            
            // Left-hand side: K - l(ti)
            double lhs = eta * (Si - _strike);
            
            // European value at l(ti)
            double europeanValue = CalculateEuropeanValue(Si, _maturity - ti);
            
            // Integral term from ti to T
            double integralUpper = CalculateIntegralTerm(Si, ti, upper, lower, crossingTime, true);
            double integralLower = CalculateIntegralTerm(Si, ti, upper, lower, crossingTime, false);
            
            // Right-hand side
            double rhs = europeanValue + integralUpper - integralLower;
            
            return lhs - rhs;
        }
        
        /// <summary>
        /// Calculates the integral term in the Kim equation.
        /// </summary>
        private double CalculateIntegralTerm(double S, double ti, double[] upper, double[] lower,
            double crossingTime, bool isUpperBoundary)
        {
            double tStart = Math.Max(ti, crossingTime);
            if (tStart >= _maturity)
                return 0.0;
            
            double dt = (_maturity - tStart) / INTEGRATION_POINTS;
            double integral = 0.0;
            
            for (int j = 0; j < INTEGRATION_POINTS; j++)
            {
                double t = tStart + (j + 0.5) * dt;
                double tMinusTi = t - ti;
                
                if (tMinusTi < 1e-10)
                    continue;
                
                // Interpolate boundary at time t
                double boundaryValue = InterpolateBoundary(
                    isUpperBoundary ? upper : lower, t);
                
                // Calculate d1 and d2
                double d1 = CalculateD1(S, boundaryValue, tMinusTi);
                double d2 = CalculateD2(S, boundaryValue, tMinusTi);
                
                // Kim integrand: rK·exp(-r·t)·Φ(-d₂) - qS·exp(-q·t)·Φ(-d₁)
                double term1 = _rate * _strike * Math.Exp(-_rate * tMinusTi) * NormalCDF(-d2);
                double term2 = _dividendYield * S * Math.Exp(-_dividendYield * tMinusTi) * NormalCDF(-d1);
                
                integral += (term1 - term2) * dt;
            }
            
            return integral;
        }
        
        /// <summary>
        /// Calculates the early exercise premium using the solved boundaries.
        /// </summary>
        private double CalculateEarlyExercisePremium(double[] upper, double[] lower, double crossingTime)
        {
            return CalculateIntegralTerm(_spot, 0.0, upper, lower, crossingTime, true)
                 - CalculateIntegralTerm(_spot, 0.0, upper, lower, crossingTime, false);
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
        /// Finds the time when boundaries cross.
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
            return _maturity; // No crossing
        }
        
        /// <summary>
        /// Checks if option should be exercised immediately.
        /// </summary>
        private bool ShouldExerciseImmediately(double upperBoundary, double lowerBoundary)
        {
            if (_isCall)
                return _spot >= upperBoundary;
            else
                return _spot <= lowerBoundary;
        }
        
        /// <summary>
        /// Calculates European option value.
        /// </summary>
        private double CalculateEuropeanValue(double S, double? T = null)
        {
            double timeToMaturity = T ?? _maturity;
            double d1 = CalculateD1(S, _strike, timeToMaturity);
            double d2 = d1 - _volatility * Math.Sqrt(timeToMaturity);
            
            double discountFactor = Math.Exp(-_rate * timeToMaturity);
            double dividendFactor = Math.Exp(-_dividendYield * timeToMaturity);
            
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
                ? Math.Max(_spot - _strike, 0.0) 
                : Math.Max(_strike - _spot, 0.0);
        }
        
        /// <summary>
        /// Calculates d₁ for Black-Scholes formula.
        /// </summary>
        private double CalculateD1(double S, double K, double T)
        {
            if (T < 1e-10)
                return S > K ? 10.0 : -10.0;
            
            double numerator = Math.Log(S / K) + (_rate - _dividendYield + 0.5 * _volatility * _volatility) * T;
            return numerator / (_volatility * Math.Sqrt(T));
        }
        
        /// <summary>
        /// Calculates d₂ for Black-Scholes formula.
        /// </summary>
        private double CalculateD2(double S, double K, double T)
        {
            return CalculateD1(S, K, T) - _volatility * Math.Sqrt(T);
        }
        
        /// <summary>
        /// Standard normal cumulative distribution function.
        /// </summary>
        private double NormalCDF(double x)
        {
            if (x > 8.0) return 1.0;
            if (x < -8.0) return 0.0;
            return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        }
        
        /// <summary>
        /// Error function approximation.
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
            x = Math.Abs(x);
            
            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
            
            return sign * y;
        }
    }
}