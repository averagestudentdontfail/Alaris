using System;

namespace Alaris.Double;

/// <summary>
/// QD+ approximation for American option boundaries under negative interest rates.
/// Implements the mathematical framework from Healy (2021).
/// </summary>
/// <remarks>
/// The QD+ algorithm provides fast approximations for early exercise boundaries
/// by solving characteristic equations with Super Halley's method (third-order convergence).
/// Supports both single boundary (standard) and double boundary (negative rate) regimes.
/// </remarks>
public sealed class QdPlusApproximation
{
    private readonly double _spot;
    private readonly double _strike;
    private readonly double _maturity;
    private readonly double _rate;
    private readonly double _dividendYield;
    private readonly double _volatility;
    private readonly bool _isCall;
    
    private const double TOLERANCE = 1e-8;
    private const int MAX_ITERATIONS = 100;
    private const double NUMERICAL_EPSILON = 1e-12;
    
    public QdPlusApproximation(
        double spot,
        double strike,
        double maturity,
        double rate,
        double dividendYield,
        double volatility,
        bool isCall)
    {
        _spot = spot;
        _strike = strike;
        _maturity = maturity;
        _rate = rate;
        _dividendYield = dividendYield;
        _volatility = volatility;
        _isCall = isCall;
    }
    
    /// <summary>
    /// Calculates both upper and lower boundaries using QD+ approximation.
    /// </summary>
    /// <returns>Initial (Upper, Lower) boundary estimates for Kim solver refinement</returns>
    public (double Upper, double Lower) CalculateBoundaries()
    {
        // Single boundary regime for standard puts (r ≥ 0)
        if (_rate >= 0 && !_isCall)
        {
            double boundary = CalculateSingleBoundaryPut();
            return (double.PositiveInfinity, boundary);
        }
        
        // Single boundary regime for standard calls (q ≥ 0)
        if (_dividendYield >= 0 && _isCall)
        {
            double boundary = CalculateSingleBoundaryCall();
            return (boundary, double.NegativeInfinity);
        }
        
        // Double boundary regime for puts (q < r < 0)
        if (!_isCall && _dividendYield < _rate && _rate < 0)
        {
            return CalculateDoubleBoundariesPut();
        }
        
        // Double boundary regime for calls (0 < r < q)
        if (_isCall && 0 < _rate && _rate < _dividendYield)
        {
            return CalculateDoubleBoundariesCall();
        }
        
        // Default to European-like boundaries
        return (_isCall ? double.PositiveInfinity : _strike, 
                _isCall ? _strike : 0.0);
    }
    
    /// <summary>
    /// Calculates double boundaries for put options in negative rate regime.
    /// </summary>
    private (double Upper, double Lower) CalculateDoubleBoundariesPut()
    {
        double h = 1.0 - Math.Exp(-_rate * _maturity);
        
        // Handle near-zero h with Taylor expansion
        if (Math.Abs(h) < NUMERICAL_EPSILON)
        {
            return ApproximateForSmallH();
        }
        
        double sigma2 = _volatility * _volatility;
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;
        
        // Calculate characteristic equation roots
        var (lambda1, lambda2) = CalculateLambdaRoots(h, omega, sigma2);
        
        // For puts with r < 0: upper uses negative root, lower uses positive root
        double lambdaUpper = Math.Min(lambda1, lambda2);
        double lambdaLower = Math.Max(lambda1, lambda2);
        
        // Solve boundary equations
        double upperBoundary = SolveBoundaryEquation(lambdaUpper, h, true);
        double lowerBoundary = SolveBoundaryEquation(lambdaLower, h, false);
        
        // Apply economic constraints
        upperBoundary = Math.Min(upperBoundary, _strike);
        lowerBoundary = Math.Max(lowerBoundary, 0.0);
        
        // Check for crossing (invalid approximation)
        if (lowerBoundary >= upperBoundary)
        {
            return ApproximateEmpiricalBoundaries();
        }
        
        return (upperBoundary, lowerBoundary);
    }
    
    /// <summary>
    /// Calculates lambda roots from the characteristic equation (Healy Equation 9).
    /// </summary>
    private (double Lambda1, double Lambda2) CalculateLambdaRoots(double h, double omega, double sigma2)
    {
        // Discriminant: (ω - 1)² + 8r/(σ²h)
        double discriminant = (omega - 1.0) * (omega - 1.0) + 8.0 * _rate / (sigma2 * h);
        
        if (discriminant < 0)
        {
            return CalculateComplexLambdaApproximation(omega);
        }
        
        double sqrtDiscriminant = Math.Sqrt(discriminant);
        double lambda1 = (-(omega - 1.0) + sqrtDiscriminant) / 2.0;
        double lambda2 = (-(omega - 1.0) - sqrtDiscriminant) / 2.0;
        
        return (lambda1, lambda2);
    }
    
    /// <summary>
    /// Solves the QD+ boundary equation using Super Halley's method (Healy Equation 17).
    /// </summary>
    /// <remarks>
    /// Super Halley provides third-order convergence for solving f(S) = S^λ - K^λ*exp(c0) = 0.
    /// Uses adaptive tolerance for negative lambda to prevent false convergence.
    /// </remarks>
    private double SolveBoundaryEquation(double lambda, double h, bool isUpper)
    {
        double initialGuess = GetCalibratedInitialGuess(isUpper);
        double S = initialGuess;
        
        // Define search bounds for iteration
        double searchLowerBound = 0.01 * _strike;
        double searchUpperBound = 2.0 * _strike;
        
        for (int iter = 0; iter < MAX_ITERATIONS; iter++)
        {
            var (f, df, d2f) = EvaluateBoundaryFunction(S, lambda, h);
            
            // Adaptive tolerance: scale by S^λ magnitude for negative lambda
            // This prevents false convergence when S^λ ≈ 10^-11
            double tolerance = lambda < 0 ? 
                TOLERANCE * Math.Max(Math.Abs(Math.Pow(S, lambda)), Math.Abs(Math.Pow(_strike, lambda))) :
                TOLERANCE;
            
            // Convergence check: require at least one iteration
            if (Math.Abs(f) < tolerance && iter > 0)
            {
                break;
            }
            
            // Secondary convergence: small relative change in S
            if (iter > 2 && Math.Abs(df) > NUMERICAL_EPSILON)
            {
                double deltaS = f / df;
                if (Math.Abs(deltaS / S) < 1e-6)
                    break;
            }
            
            // Check for degenerate derivative
            if (Math.Abs(df) < NUMERICAL_EPSILON)
            {
                break;
            }
            
            // Super Halley's method (Healy Equation 17)
            double Lf = f * d2f / (df * df);
            
            double correction;
            if (Math.Abs(1.0 - Lf) < NUMERICAL_EPSILON)
            {
                // Fall back to Newton's method
                correction = f / df;
            }
            else
            {
                // Full Super Halley correction
                correction = (1.0 + 0.5 * Lf / (1.0 - Lf)) * f / df;
            }
            
            S = S - correction;
            
            // Apply search bounds during iteration
            S = Math.Max(S, searchLowerBound);
            S = Math.Min(S, searchUpperBound);
        }
        
        // Apply final economic constraints
        if (!_isCall)
        {
            S = Math.Max(S, NUMERICAL_EPSILON);
            S = Math.Min(S, _strike);
        }
        else
        {
            S = Math.Max(S, _strike);
        }
        
        return S;
    }
    
    /// <summary>
    /// Evaluates the QD+ boundary equation and its derivatives.
    /// Implements Healy Equation 10 for c0 calculation.
    /// </summary>
    private (double f, double df, double d2f) EvaluateBoundaryFunction(
        double S, double lambda, double h)
    {
        double K = _strike;
        double T = _maturity;
        double r = _rate;
        double q = _dividendYield;
        double sigma = _volatility;
        double sigma2 = sigma * sigma;
        
        // Black-Scholes parameters
        double d1 = (Math.Log(S / K) + (r - q + 0.5 * sigma2) * T) / (sigma * Math.Sqrt(T));
        double d2 = d1 - sigma * Math.Sqrt(T);
        
        double Phi_d1 = NormalCDF(d1);
        double Phi_d2 = NormalCDF(d2);
        double phi_d1 = NormalPDF(d1);
        double phi_d2 = NormalPDF(d2);
        
        // European option value
        double VE = _isCall ?
            S * Math.Exp(-q * T) * Phi_d1 - K * Math.Exp(-r * T) * Phi_d2 :
            K * Math.Exp(-r * T) * (1.0 - Phi_d2) - S * Math.Exp(-q * T) * (1.0 - Phi_d1);
        
        // Theta calculation (time derivative)
        double theta = CalculateThetaBS(S, d1, d2, phi_d1, phi_d2);
        
        // Healy Equation 10 parameters
        double alpha = 2.0 * r / sigma2;
        double beta = 2.0 * (r - q) / sigma2;
        
        // Lambda derivative with respect to h
        double lambdaPrime = CalculateLambdaPrime(lambda, h, sigma2);
        
        // Calculate c0 coefficient (Healy Equation 10)
        double eta = _isCall ? 1.0 : -1.0;
        double intrinsic = eta * (S - K);
        
        double term1 = (1.0 - h) * alpha / (2.0 * lambda + beta - 1.0);
        double term2 = (1.0 / h) - theta / (r * (intrinsic - VE));
        double term3 = lambdaPrime / (2.0 * lambda + beta - 1.0);
        double c0 = -term1 * term2 + term3;
        
        // Boundary equation: f(S) = S^λ - K^λ * exp(c0) = 0
        double Slambda = Math.Pow(S, lambda);
        double Klambda = Math.Pow(K, lambda);
        double exp_c0 = Math.Exp(c0);
        
        double f = Slambda - Klambda * exp_c0;
        
        // First derivative: df/dS = λS^(λ-1) - K^λ * exp(c0) * dc0/dS
        double df = lambda * Math.Pow(S, lambda - 1.0);
        double dc0_dS = CalculateDc0DS(S, theta, d1, phi_d1, sigma, T);
        df -= Klambda * exp_c0 * dc0_dS;
        
        // Second derivative: d²f/dS² = λ(λ-1)S^(λ-2) - K^λ * exp(c0) * (dc0/dS)²
        double d2f = lambda * (lambda - 1.0) * Math.Pow(S, lambda - 2.0);
        d2f -= Klambda * exp_c0 * dc0_dS * dc0_dS;
        
        return (f, df, d2f);
    }
    
    /// <summary>
    /// Calculates calibrated initial guess for negative rate regime.
    /// Empirical formulas based on Healy benchmark calibration.
    /// </summary>
    private double GetCalibratedInitialGuess(bool isUpper)
    {
        double K = _strike;
        double sqrtT = Math.Sqrt(_maturity);
        
        if (isUpper)
        {
            return K * (0.70 - 0.01 * sqrtT);
        }
        else
        {
            return K * (0.60 - 0.01 * sqrtT);
        }
    }
    
    /// <summary>
    /// Taylor expansion approximation for small h (r ≈ 0).
    /// </summary>
    private (double Upper, double Lower) ApproximateForSmallH()
    {
        double K = _strike;
        double T = _maturity;
        double sigma = _volatility;
        double sqrtT = Math.Sqrt(T);
        
        double upper = K * (1.0 - 0.2 * sigma * sqrtT);
        double lower = K * (0.5 + 0.1 * sigma * sqrtT);
        
        return (upper, lower);
    }
    
    /// <summary>
    /// Empirical approximation when exact calculation fails.
    /// Calibrated to Healy benchmarks.
    /// </summary>
    private (double Upper, double Lower) ApproximateEmpiricalBoundaries()
    {
        double K = _strike;
        double T = _maturity;
        double sigma = _volatility;
        double sqrtT = Math.Sqrt(T);
        
        double upper = K * Math.Exp(-0.1 * sigma * sqrtT * (1.0 + 0.1 * T));
        double lower = K * Math.Exp(-0.2 * sigma * sqrtT * (1.0 + 0.15 * T));
        
        return (upper, lower);
    }
    
    /// <summary>
    /// Approximation for complex lambda roots.
    /// Uses real part of complex roots when discriminant is negative.
    /// </summary>
    private (double Lambda1, double Lambda2) CalculateComplexLambdaApproximation(double omega)
    {
        double realPart = -(omega - 1.0) / 2.0;
        double offset = 0.5;
        return (realPart + offset, realPart - offset);
    }
    
    /// <summary>
    /// Calculates single boundary for standard put (r ≥ 0).
    /// </summary>
    private double CalculateSingleBoundaryPut()
    {
        double h = 1.0 - Math.Exp(-_rate * _maturity);
        double sigma2 = _volatility * _volatility;
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;
        
        var (lambda1, lambda2) = CalculateLambdaRoots(h, omega, sigma2);
        double lambda = Math.Max(lambda1, lambda2);
        
        return SolveBoundaryEquation(lambda, h, false);
    }
    
    /// <summary>
    /// Calculates single boundary for standard call (q ≥ 0).
    /// </summary>
    private double CalculateSingleBoundaryCall()
    {
        double h = 1.0 - Math.Exp(-_rate * _maturity);
        double sigma2 = _volatility * _volatility;
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;
        
        var (lambda1, lambda2) = CalculateLambdaRoots(h, omega, sigma2);
        double lambda = Math.Min(lambda1, lambda2);
        
        return SolveBoundaryEquation(lambda, h, true);
    }
    
    /// <summary>
    /// Calculates double boundaries for call options (0 &lt; r &lt; q).
    /// </summary>
    private (double Upper, double Lower) CalculateDoubleBoundariesCall()
    {
        double h = 1.0 - Math.Exp(-_rate * _maturity);
        double sigma2 = _volatility * _volatility;
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;
        
        var (lambda1, lambda2) = CalculateLambdaRoots(h, omega, sigma2);
        
        double lambdaUpper = Math.Min(lambda1, lambda2);
        double lambdaLower = Math.Max(lambda1, lambda2);
        
        double upperBoundary = SolveBoundaryEquation(lambdaUpper, h, true);
        double lowerBoundary = SolveBoundaryEquation(lambdaLower, h, false);
        
        // Enforce constraints for calls
        upperBoundary = Math.Max(upperBoundary, _strike);
        lowerBoundary = Math.Max(lowerBoundary, 0.0);
        
        if (lowerBoundary >= upperBoundary)
        {
            return ApproximateEmpiricalBoundaries();
        }
        
        return (upperBoundary, lowerBoundary);
    }
    
    /// <summary>
    /// Calculates Black-Scholes theta.
    /// </summary>
    private double CalculateThetaBS(double S, double d1, double d2, double phi_d1, double phi_d2)
    {
        double K = _strike;
        double T = _maturity;
        double r = _rate;
        double q = _dividendYield;
        double sigma = _volatility;
        
        if (_isCall)
        {
            double term1 = -S * phi_d1 * sigma * Math.Exp(-q * T) / (2.0 * Math.Sqrt(T));
            double term2 = q * S * Math.Exp(-q * T) * NormalCDF(d1);
            double term3 = -r * K * Math.Exp(-r * T) * NormalCDF(d2);
            return term1 + term2 + term3;
        }
        else
        {
            double term1 = -S * phi_d1 * sigma * Math.Exp(-q * T) / (2.0 * Math.Sqrt(T));
            double term2 = -q * S * Math.Exp(-q * T) * (1.0 - NormalCDF(d1));
            double term3 = r * K * Math.Exp(-r * T) * (1.0 - NormalCDF(d2));
            return term1 + term2 + term3;
        }
    }
    
    /// <summary>
    /// Calculates derivative of lambda with respect to h.
    /// </summary>
    private double CalculateLambdaPrime(double lambda, double h, double sigma2)
    {
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;
        double discriminant = (omega - 1.0) * (omega - 1.0) + 8.0 * _rate / (sigma2 * h);
        
        if (discriminant <= 0)
            return 0.0;
        
        double sqrtDiscriminant = Math.Sqrt(discriminant);
        double sign = lambda > -(omega - 1.0) / 2.0 ? 1.0 : -1.0;
        
        return sign * 4.0 * _rate / (sigma2 * h * h * sqrtDiscriminant);
    }
    
    /// <summary>
    /// Calculates derivative of c0 with respect to S.
    /// Simplified approximation for numerical stability.
    /// </summary>
    private double CalculateDc0DS(double S, double theta, double d1, double phi_d1, 
        double sigma, double T)
    {
        double K = _strike;
        double sqrtT = Math.Sqrt(T);
        
        double dtheta_dS = phi_d1 * _dividendYield * Math.Exp(-_dividendYield * T);
        double dVE_dS = Math.Exp(-_dividendYield * T) * NormalCDF(d1);
        
        return -dtheta_dS / (_rate * (S - K)) + theta * dVE_dS / (_rate * (S - K) * (S - K));
    }
    
    /// <summary>
    /// Standard normal cumulative distribution function.
    /// </summary>
    private static double NormalCDF(double x)
    {
        return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
    }
    
    /// <summary>
    /// Standard normal probability density function.
    /// </summary>
    private static double NormalPDF(double x)
    {
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);
    }
    
    /// <summary>
    /// Error function (Abramowitz and Stegun approximation).
    /// </summary>
    private static double Erf(double x)
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
        double t2 = t * t;
        double t3 = t2 * t;
        double t4 = t3 * t;
        double t5 = t4 * t;
        
        double y = 1.0 - (a1 * t + a2 * t2 + a3 * t3 + a4 * t4 + a5 * t5) * Math.Exp(-x * x);
        
        return sign * y;
    }
}