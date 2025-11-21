using System;

namespace Alaris.Double;

/// <summary>
/// QD+ approximation for American option boundaries under negative interest rates.
/// Implements the mathematical framework from Healy (2021).
/// </summary>
/// <remarks>
/// <para>
/// The QD+ algorithm provides fast approximations for early exercise boundaries
/// by solving characteristic equations with Super Halley's method (third-order convergence).
/// </para>
/// <para>
/// Compliance: Alaris High-Integrity Coding Standard v1.2
/// - Rule 9: Guard clauses on all public inputs
/// - Rule 13: Complexity limits observed via helper extraction
/// - Rule 2: Zero warnings / strict typing
/// </para>
/// <para>
/// Reference: Healy, J. (2021). "Pricing American Options Under Negative Rates".
/// </para>
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

    private const double Tolerance = 1e-8;
    private const int MaxIterations = 100;
    private const double NumericalEpsilon = 1e-12;

    /// <summary>
    /// Initializes a new instance of the QD+ approximation engine.
    /// </summary>
    /// <param name="spot">Current underlying price (must be > 0).</param>
    /// <param name="strike">Option strike price (must be > 0).</param>
    /// <param name="maturity">Time to maturity in years (must be > 0).</param>
    /// <param name="rate">Risk-free interest rate (can be negative).</param>
    /// <param name="dividendYield">Dividend yield (can be negative).</param>
    /// <param name="volatility">Annualized volatility (must be > 0).</param>
    /// <param name="isCall">True for Call options, False for Put options.</param>
    /// <exception cref="ArgumentException">Thrown if positive constraints are violated.</exception>
    public QdPlusApproximation(
        double spot,
        double strike,
        double maturity,
        double rate,
        double dividendYield,
        double volatility,
        bool isCall)
    {
        // Rule 9: Guard Clauses
        if (spot <= 0)
        {
            throw new ArgumentException("Spot price must be positive", nameof(spot));
        }
        if (strike <= 0)
        {
            throw new ArgumentException("Strike price must be positive", nameof(strike));
        }
        if (maturity <= 0)
        {
            throw new ArgumentException("Maturity must be positive", nameof(maturity));
        }
        if (volatility <= 0)
        {
            throw new ArgumentException("Volatility must be positive", nameof(volatility));
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
    /// Calculates both upper and lower boundaries using QD+ approximation.
    /// </summary>
    /// <returns>Initial (Upper, Lower) boundary estimates for Kim solver refinement.</returns>
    public (double Upper, double Lower) CalculateBoundaries()
    {
        // Single boundary regime for standard puts (r >= 0)
        if (_rate >= 0 && !_isCall)
        {
            double boundary = CalculateSingleBoundaryPut();
            return (double.PositiveInfinity, boundary);
        }

        // Single boundary regime for standard calls (q >= 0)
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

        // Default Fallback: European-like boundaries (Safety net)
        return (_isCall ? double.PositiveInfinity : _strike,
                _isCall ? _strike : 0.0);
    }

    private (double Upper, double Lower) CalculateDoubleBoundariesPut()
    {
        double h = 1.0 - Math.Exp(-_rate * _maturity);

        // Handle near-zero h with Taylor expansion (singularity prevention)
        if (Math.Abs(h) < NumericalEpsilon)
        {
            return ApproximateForSmallH();
        }

        double sigma2 = _volatility * _volatility;
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;

        // Calculate characteristic equation roots
        (double lambda1, double lambda2) = CalculateLambdaRoots(h, omega, sigma2);

        // For puts with r < 0: upper uses negative root, lower uses positive root
        double lambdaUpper = Math.Min(lambda1, lambda2);
        double lambdaLower = Math.Max(lambda1, lambda2);

        // Solve boundary equations
        double upperBoundary = SolveBoundaryEquation(lambdaUpper, h, isUpper: true);
        double lowerBoundary = SolveBoundaryEquation(lambdaLower, h, isUpper: false);

        // Apply constraints and validation
        return ApplyBoundaryConstraints(upperBoundary, lowerBoundary);
    }

    private (double Upper, double Lower) CalculateDoubleBoundariesCall()
    {
        double h = 1.0 - Math.Exp(-_rate * _maturity);

        // Handle near-zero h
        if (Math.Abs(h) < NumericalEpsilon)
        {
            return ApproximateForSmallH();
        }

        double sigma2 = _volatility * _volatility;
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;

        (double lambda1, double lambda2) = CalculateLambdaRoots(h, omega, sigma2);

        // For calls with r > 0: similar logic to puts but inverted regime
        double lambdaUpper = Math.Min(lambda1, lambda2);
        double lambdaLower = Math.Max(lambda1, lambda2);

        double upperBoundary = SolveBoundaryEquation(lambdaUpper, h, isUpper: true);
        double lowerBoundary = SolveBoundaryEquation(lambdaLower, h, isUpper: false);

        // Apply constraints and validation
        return ApplyBoundaryConstraints(upperBoundary, lowerBoundary);
    }

    private double CalculateSingleBoundaryPut()
    {
        double h = 1.0 - Math.Exp(-_rate * _maturity);
        double sigma2 = _volatility * _volatility;
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;

        (double lambda1, double lambda2) = CalculateLambdaRoots(h, omega, sigma2);
        double lambda = Math.Max(lambda1, lambda2);

        return SolveBoundaryEquation(lambda, h, isUpper: false);
    }

    private double CalculateSingleBoundaryCall()
    {
        double h = 1.0 - Math.Exp(-_rate * _maturity);
        double sigma2 = _volatility * _volatility;
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;

        (double lambda1, double lambda2) = CalculateLambdaRoots(h, omega, sigma2);
        double lambda = Math.Min(lambda1, lambda2);

        return SolveBoundaryEquation(lambda, h, isUpper: true);
    }

    /// <summary>
    /// Solves the QD+ boundary equation using Super Halley's method.
    /// </summary>
    private double SolveBoundaryEquation(double lambda, double h, bool isUpper)
    {
        // Generalized initialization based on economic boundaries (No hardcoded tables)
        double initialGuess = GetGeneralizedInitialGuess(isUpper);
        double S = initialGuess;

        // Define safe search bounds
        double searchLowerBound = 0.01 * _strike;
        double searchUpperBound = 3.0 * _strike; // Increased for calls

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            (double f, double df, double d2f) = EvaluateBoundaryFunction(S, lambda, h);

            // Adaptive tolerance scaling
            double tolerance = lambda < 0 ?
                Tolerance * Math.Max(Math.Abs(Math.Pow(S, lambda)), Math.Abs(Math.Pow(_strike, lambda))) :
                Tolerance;

            if (Math.Abs(f) < tolerance && iter > 0)
            {
                break;
            }

            // Derivative check
            if (Math.Abs(df) < NumericalEpsilon)
            {
                break;
            }

            // Super Halley's method (Healy Equation 17)
            double Lf = f * d2f / (df * df);
            double correction;

            if (Math.Abs(1.0 - Lf) < NumericalEpsilon)
            {
                correction = f / df; // Fallback to Newton
            }
            else
            {
                correction = (1.0 + (0.5 * Lf / (1.0 - Lf))) * (f / df);
            }

            S -= correction;
            S = Math.Clamp(S, searchLowerBound, searchUpperBound);
        }

        // Validation: Reject spurious roots
        return ValidateRoot(S, initialGuess);
    }

    /// <summary>
    /// Evaluates the boundary function f(S) and its first two derivatives.
    /// </summary>
    private (double f, double df, double d2f) EvaluateBoundaryFunction(double S, double lambda, double h)
    {
        // Ensure we don't evaluate at exactly the strike (singularity risk)
        double safeS = EnsureDistanceSteps(S);

        // Extract Black-Scholes components (Rule 13: Reduce complexity)
        // Explicit type used (Rule IDE0008)
        (double VE, double Theta, double D1) bs = CalculateBsComponents(safeS);

        // Healy parameters
        double sigma2 = _volatility * _volatility;
        double alpha = 2.0 * _rate / sigma2;
        double beta = 2.0 * (_rate - _dividendYield) / sigma2;

        double lambdaPrime = CalculateLambdaPrime(lambda, h, sigma2);
        double c0 = CalculateC0(safeS, h, lambda, alpha, beta, lambdaPrime, bs);

        // Function and derivatives construction
        double Slambda = Math.Pow(safeS, lambda);
        double Klambda = Math.Pow(_strike, lambda);
        double exp_c0 = Math.Exp(c0);

        double f = Slambda - (Klambda * exp_c0);

        // df calculation
        double dc0_dS = CalculateDc0DS(safeS, bs);
        double df = (lambda * Math.Pow(safeS, lambda - 1.0)) - (Klambda * exp_c0 * dc0_dS);

        // d2f calculation
        double d2f = (lambda * (lambda - 1.0) * Math.Pow(safeS, lambda - 2.0))
                   - (Klambda * exp_c0 * dc0_dS * dc0_dS);

        return (f, df, d2f);
    }

    /// <summary>
    /// Calculates the c0 coefficient (Healy Equation 10).
    /// </summary>
    private double CalculateC0(
        double S, double h, double lambda, double alpha, double beta, double lambdaPrime,
        (double VE, double Theta, double D1) bs)
    {
        double eta = _isCall ? 1.0 : -1.0;
        double intrinsic = eta * (S - _strike);
        double diff = intrinsic - bs.VE;

        // Safeguard against division by zero near boundary intersection
        double term2;
        if (Math.Abs(diff) < NumericalEpsilon || Math.Abs(_rate * diff) < NumericalEpsilon)
        {
            term2 = 1.0 / h;
        }
        else
        {
            term2 = (1.0 / h) - (bs.Theta / (_rate * diff));
        }

        // Healy Equation 10 implementation
        double denominator = (2.0 * lambda) + beta - 1.0;
        double term1 = (1.0 - h) * alpha / denominator;
        double term3 = lambdaPrime / denominator;

        double c0 = (-term1 * term2) + term3;
        return Math.Clamp(c0, -10.0, 10.0); // Prevent overflow
    }

    /// <summary>
    /// Helper to compute Black-Scholes value, Theta, and d1.
    /// </summary>
    private (double VE, double Theta, double D1) CalculateBsComponents(double S)
    {
        double d1 = (Math.Log(S / _strike) + ((_rate - _dividendYield + (0.5 * _volatility * _volatility)) * _maturity))
                  / (_volatility * Math.Sqrt(_maturity));
        double d2 = d1 - (_volatility * Math.Sqrt(_maturity));

        double nd1 = NormalCDF(d1);
        double nd2 = NormalCDF(d2);
        double npd1 = NormalPDF(d1);
        double npd2 = NormalPDF(d2);

        double ve, theta;

        if (_isCall)
        {
            ve = (S * Math.Exp(-_dividendYield * _maturity) * nd1) - (_strike * Math.Exp(-_rate * _maturity) * nd2);
            theta = (-S * npd1 * _volatility * Math.Exp(-_dividendYield * _maturity) / (2.0 * Math.Sqrt(_maturity)))
                  + (_dividendYield * S * Math.Exp(-_dividendYield * _maturity) * nd1)
                  - (_rate * _strike * Math.Exp(-_rate * _maturity) * nd2);
        }
        else
        {
            ve = (_strike * Math.Exp(-_rate * _maturity) * (1.0 - nd2)) - (S * Math.Exp(-_dividendYield * _maturity) * (1.0 - nd1));
            theta = (-S * npd1 * _volatility * Math.Exp(-_dividendYield * _maturity) / (2.0 * Math.Sqrt(_maturity)))
                  - (_dividendYield * S * Math.Exp(-_dividendYield * _maturity) * (1.0 - nd1))
                  + (_rate * _strike * Math.Exp(-_rate * _maturity) * (1.0 - nd2));
        }

        return (ve, theta, d1);
    }

    /// <summary>
    /// Generates an initial guess based on economic limits
    /// </summary>
    private double GetGeneralizedInitialGuess(bool isUpper)
    {
        // CASE 1: Put Option
        if (!_isCall)
        {
            if (isUpper)
            {
                // Upper boundary is bounded by Strike.
                // Start slightly OTM relative to exercise region (S < K).
                return _strike * 0.95;
            }
            else
            {
                // Lower boundary (Double Boundary Regime: q < r < 0).
                // Asymptotic limit is rK/q.
                if (_rate < 0 && _dividendYield < 0 && Math.Abs(_dividendYield) > NumericalEpsilon)
                {
                    double ratio = _rate / _dividendYield;
                    return _strike * ratio * 0.9; // Slightly below asymptotic limit
                }
                return _strike * 0.5; // Fallback
            }
        }
        // CASE 2: Call Option
        else
        {
            if (isUpper)
            {
                // Upper boundary (Double Boundary Regime: 0 < r < q).
                // Asymptotic limit is rK/q.
                if (_rate > 0 && _dividendYield > 0 && Math.Abs(_dividendYield) > NumericalEpsilon)
                {
                    double ratio = _rate / _dividendYield;
                    return _strike * ratio * 1.1; // Slightly above asymptotic limit
                }
                return _strike * 1.5; // Fallback deep OTM
            }
            else
            {
                // Lower boundary is bounded by Strike.
                // Start slightly OTM relative to exercise region (S > K).
                return _strike * 1.05;
            }
        }
    }

    private (double Lambda1, double Lambda2) CalculateLambdaRoots(double h, double omega, double sigma2)
    {
        // Healy Equation 9
        double discriminant = ((omega - 1.0) * (omega - 1.0)) + (8.0 * _rate / (sigma2 * h));

        if (discriminant < 0)
        {
            double realPart = -(omega - 1.0) / 2.0;
            return (realPart + 0.5, realPart - 0.5);
        }

        double sqrtDisc = Math.Sqrt(discriminant);
        return ((-(omega - 1.0) + sqrtDisc) / 2.0,
                (-(omega - 1.0) - sqrtDisc) / 2.0);
    }

    private double ValidateRoot(double S, double initialGuess)
    {
        // Reject roots too close to strike
        if (Math.Abs(S - _strike) / _strike < 0.05)
        {
            return initialGuess;
        }

        // If deviation is extreme (> 50%), trust initialization more than divergence
        if (Math.Abs(S - initialGuess) > 0.5 * initialGuess)
        {
            return initialGuess;
        }

        return S;
    }

    private (double Upper, double Lower) ApplyBoundaryConstraints(double upper, double lower)
    {
        // Standard economic constraints
        if (!_isCall)
        {
            upper = Math.Min(upper, _strike);
            lower = Math.Max(lower, 0.0);
        }
        else
        {
            upper = Math.Max(upper, _strike);
            lower = Math.Max(lower, 0.0);
        }

        // Validate ordering
        if ((_isCall && upper <= lower) || (!_isCall && lower >= upper))
        {
            return ApproximateEmpiricalBoundaries();
        }

        return (upper, lower);
    }

    private (double Upper, double Lower) ApproximateForSmallH()
    {
        double sqrtT = Math.Sqrt(_maturity);
        double factor = 0.2 * _volatility * sqrtT;

        double b1 = _strike * (1.0 - factor);
        double b2 = _strike * (0.5 + (factor * 0.5));

        if (_isCall)
        {
            // Mirror for calls
            return (_strike + (_strike - b2), _strike + (_strike - b1));
        }
        return (b1, b2);
    }

    private (double Upper, double Lower) ApproximateEmpiricalBoundaries()
    {
        // Fallback when calculation fails - purely heuristic
        double sqrtT = Math.Sqrt(_maturity);
        double upper = _isCall ? _strike * 1.4 : _strike * 0.75;
        double lower = _isCall ? _strike * 1.1 : _strike * 0.50;

        // Adjust slightly for volatility
        double volAdj = _volatility * sqrtT * 0.1 * _strike;
        if (!_isCall)
        {
            upper -= volAdj;
            lower -= volAdj;
        }
        else
        {
            upper += volAdj;
            lower += volAdj;
        }

        return (upper, lower);
    }

    private double CalculateLambdaPrime(double lambda, double h, double sigma2)
    {
        double omega = 2.0 * (_rate - _dividendYield) / sigma2;
        double discriminant = ((omega - 1.0) * (omega - 1.0)) + (8.0 * _rate / (sigma2 * h));

        if (discriminant <= NumericalEpsilon)
        {
            return 0.0;
        }

        double sqrtDisc = Math.Sqrt(discriminant);
        double sign = lambda > -(omega - 1.0) / 2.0 ? 1.0 : -1.0;

        return sign * 4.0 * _rate / (sigma2 * h * h * sqrtDisc);
    }

    private double CalculateDc0DS(double S, (double VE, double Theta, double D1) bs)
    {
        double dtheta_dS = NormalPDF(bs.D1) * _dividendYield * Math.Exp(-_dividendYield * _maturity);
        double dVE_dS = Math.Exp(-_dividendYield * _maturity) * NormalCDF(bs.D1);
        double diff = S - _strike;

        if (Math.Abs(diff) < NumericalEpsilon || Math.Abs(_rate * diff) < NumericalEpsilon)
        {
            return 0.0;
        }

        double term1 = -dtheta_dS / (_rate * diff);
        double term2 = bs.Theta * dVE_dS / (_rate * diff * diff);

        return term1 + term2;
    }

    private double EnsureDistanceSteps(double S)
    {
        double minDistance = _strike * 0.01;
        if (Math.Abs(S - _strike) < minDistance)
        {
            return _isCall ? _strike + minDistance : _strike - minDistance;
        }
        return S;
    }

    private static double NormalCDF(double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));

    private static double NormalPDF(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

    private static double Erf(double x)
    {
        const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741;
        const double a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        double t = 1.0 / (1.0 + (p * x));
        
        double polynomial = (((((((a5 * t) + a4) * t) + a3) * t) + a2) * t) + a1;
        
        double y = 1.0 - (polynomial * t * Math.Exp(-(x * x)));

        return sign * y;
    }
}