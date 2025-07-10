using Alaris.Double;

namespace Alaris.Double;

/// <summary>
/// Implements the integral equation solvers for double boundary American options
/// Based on equations (4.6) and (4.7) from "The Alaris Mathematical Framework"
/// Uses QuantLib's numerical methods for precision and robustness
/// </summary>
public static class IntegralEquationSolvers
{
    private static readonly CumulativeNormalDistribution _normalCdf = new CumulativeNormalDistribution();
    private static readonly NormalDistribution _normalPdf = new NormalDistribution();

    /// <summary>
    /// Solves the upper boundary value-matching equation (4.6)
    /// K - B(τ) = v(τ, B(τ)) + early exercise premium
    /// </summary>
    /// <param name="tau">Time to maturity</param>
    /// <param name="currentBoundary">Current boundary value B(τ)</param>
    /// <param name="boundaryFunction">Function providing B(u) for u ∈ [0,τ]</param>
    /// <param name="strike">Strike price K</param>
    /// <param name="r">Risk-free rate</param>
    /// <param name="q">Dividend yield</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="intersectionTime">Boundary intersection time τ*</param>
    /// <returns>Updated boundary value</returns>
    public static double SolveUpperBoundaryEquation(double tau, double currentBoundary,
        BoundaryFunction boundaryFunction, double strike, double r, double q, double sigma,
        double intersectionTime = double.PositiveInfinity)
    {
        double effectiveTau = Math.Min(tau, intersectionTime);
        
        // European option value at the boundary
        double europeanValue = ComputeBlackScholesPut(currentBoundary, strike, tau, r, q, sigma);
        
        // Early exercise premium components
        double interestPremium = ComputeInterestPremium(tau, effectiveTau, currentBoundary, 
                                                       boundaryFunction, strike, r, q, sigma);
        double dividendPremium = ComputeDividendPremium(tau, effectiveTau, currentBoundary,
                                                       boundaryFunction, strike, r, q, sigma);
        
        // Value-matching condition: V(τ, B(τ)) = K - B(τ)
        // Therefore: K - B(τ) = European + Interest Premium - Dividend Premium
        // Solving for B(τ): B(τ) = K - European - Interest Premium + Dividend Premium
        return strike - europeanValue - interestPremium + dividendPremium;
    }

    /// <summary>
    /// Solves the lower boundary smooth-pasting equation (4.7)
    /// ∂V/∂s(τ, Y(τ)) = -1
    /// </summary>
    /// <param name="tau">Time to maturity</param>
    /// <param name="initialGuess">Initial guess for Y(τ)</param>
    /// <param name="boundaryFunction">Function providing Y(u) for u ∈ [0,τ]</param>
    /// <param name="strike">Strike price K</param>
    /// <param name="r">Risk-free rate</param>
    /// <param name="q">Dividend yield</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="intersectionTime">Boundary intersection time τ*</param>
    /// <returns>Lower boundary value Y(τ)</returns>
    public static double SolveLowerBoundaryEquation(double tau, double initialGuess,
        BoundaryFunction boundaryFunction, double strike, double r, double q, double sigma,
        double intersectionTime = double.PositiveInfinity)
    {
        // Use simple Newton-Raphson instead of Brent solver to avoid inheritance issues
        return SolveLowerBoundaryNewtonRaphson(tau, initialGuess, boundaryFunction, strike, r, q, sigma,
                                             Math.Min(tau, intersectionTime));
    }

    /// <summary>
    /// Computes the interest rate component of the early exercise premium
    /// ∫[0,min(τ,τ*)] rK e^(-r(τ-u)) Φ(-d_-(τ-u,B(τ)/B(u))) du
    /// </summary>
    private static double ComputeInterestPremium(double tau, double effectiveTau, double currentBoundary,
        BoundaryFunction boundaryFunction, double strike, double r, double q, double sigma)
    {
        if (effectiveTau <= 1e-10) return 0.0;

        // Create integrand function
        var integrand = new InterestPremiumIntegrand(tau, currentBoundary, boundaryFunction, 
                                                   strike, r, q, sigma);

        // Use QuantLib's Simpson integrator with adaptive refinement
        var integrator = new SimpsonIntegral(1e-10, 1000);
        
        try
        {
            return QuantLibApiHelper.CallSimpsonIntegral(integrator, integrand.value, 0.0, effectiveTau);
        }
        catch
        {
            // Fallback to trapezoidal rule if Simpson fails
            return IntegrateTrapezoidal(integrand.value, 0.0, effectiveTau, 1000);
        }
    }

    /// <summary>
    /// Computes the dividend component of the early exercise premium
    /// ∫[0,min(τ,τ*)] qB(τ) e^(-q(τ-u)) Φ(-d_+(τ-u,B(τ)/B(u))) du
    /// </summary>
    private static double ComputeDividendPremium(double tau, double effectiveTau, double currentBoundary,
        BoundaryFunction boundaryFunction, double strike, double r, double q, double sigma)
    {
        if (effectiveTau <= 1e-10) return 0.0;

        var integrand = new DividendPremiumIntegrand(tau, currentBoundary, boundaryFunction,
                                                   strike, r, q, sigma);

        var integrator = new SimpsonIntegral(1e-10, 1000);
        
        try
        {
            return QuantLibApiHelper.CallSimpsonIntegral(integrator, integrand.value, 0.0, effectiveTau);
        }
        catch
        {
            return IntegrateTrapezoidal(integrand.value, 0.0, effectiveTau, 1000);
        }
    }

    /// <summary>
    /// Evaluates the smooth-pasting condition for the lower boundary
    /// Returns: ∂V/∂s(τ, Y(τ)) + 1 (should equal zero at the solution)
    /// </summary>
    public static double EvaluateLowerBoundaryCondition(double tau, double boundary,
        BoundaryFunction boundaryFunction, double strike, double r, double q, double sigma,
        double effectiveTau)
    {
        // European delta component
        double d_plus = ComputeD(1, tau, boundary / strike, r, q, sigma);
        double europeanDelta = -Math.Exp(-q * tau) * QuantLibApiHelper.CallCumNorm(_normalCdf, -d_plus);

        // Integral components from smooth-pasting condition
        double interestDeltaIntegral = ComputeLowerBoundaryInterestIntegral(tau, effectiveTau, boundary,
                                                                          boundaryFunction, strike, r, q, sigma);
        double dividendDeltaIntegral = ComputeLowerBoundaryDividendIntegral(tau, effectiveTau, boundary,
                                                                          boundaryFunction, strike, r, q, sigma);

        // Smooth-pasting condition: ∂V/∂s = -1
        // So we want: europeanDelta + interestDeltaIntegral - dividendDeltaIntegral + 1 = 0
        return europeanDelta + interestDeltaIntegral - dividendDeltaIntegral + 1.0;
    }

    private static double ComputeLowerBoundaryInterestIntegral(double tau, double effectiveTau, double boundary,
        BoundaryFunction boundaryFunction, double strike, double r, double q, double sigma)
    {
        var integrand = new LowerBoundaryInterestIntegrand(tau, boundary, boundaryFunction,
                                                         strike, r, q, sigma);

        var integrator = new SimpsonIntegral(1e-10, 1000);
        
        try
        {
            return QuantLibApiHelper.CallSimpsonIntegral(integrator, integrand.value, 0.0, effectiveTau);
        }
        catch
        {
            return IntegrateTrapezoidal(integrand.value, 0.0, effectiveTau, 1000);
        }
    }

    private static double ComputeLowerBoundaryDividendIntegral(double tau, double effectiveTau, double boundary,
        BoundaryFunction boundaryFunction, double strike, double r, double q, double sigma)
    {
        var integrand = new LowerBoundaryDividendIntegrand(tau, boundary, boundaryFunction,
                                                         strike, r, q, sigma);

        var integrator = new SimpsonIntegral(1e-10, 1000);
        
        try
        {
            return QuantLibApiHelper.CallSimpsonIntegral(integrator, integrand.value, 0.0, effectiveTau);
        }
        catch
        {
            return IntegrateTrapezoidal(integrand.value, 0.0, effectiveTau, 1000);
        }
    }

    private static double SolveLowerBoundaryNewtonRaphson(double tau, double initialGuess,
        BoundaryFunction boundaryFunction, double strike, double r, double q, double sigma,
        double effectiveTau)
    {
        double boundary = initialGuess;
        const double tolerance = 1e-12;
        const int maxIterations = 100;

        for (int i = 0; i < maxIterations; i++)
        {
            double f = EvaluateLowerBoundaryCondition(tau, boundary, boundaryFunction, strike, r, q, sigma, effectiveTau);
            
            if (Math.Abs(f) < tolerance) break;

            // Numerical derivative
            double h = 1e-8;
            double f_plus = EvaluateLowerBoundaryCondition(tau, boundary + h, boundaryFunction, strike, r, q, sigma, effectiveTau);
            double df = (f_plus - f) / h;

            if (Math.Abs(df) < 1e-15) break; // Avoid division by zero

            double newBoundary = boundary - f / df;
            newBoundary = Math.Max(0.01 * strike, Math.Min(0.99 * strike, newBoundary));

            if (Math.Abs(newBoundary - boundary) < tolerance) break;
            boundary = newBoundary;
        }

        return boundary;
    }

    /// <summary>
    /// Computes Black-Scholes put option price
    /// </summary>
    private static double ComputeBlackScholesPut(double spot, double strike, double tau, double r, double q, double sigma)
    {
        if (tau <= 0) return Math.Max(strike - spot, 0.0);

        double d1 = ComputeD(1, tau, spot / strike, r, q, sigma);
        double d2 = ComputeD(-1, tau, spot / strike, r, q, sigma);

        return strike * Math.Exp(-r * tau) * QuantLibApiHelper.CallCumNorm(_normalCdf, -d2) - 
               spot * Math.Exp(-q * tau) * QuantLibApiHelper.CallCumNorm(_normalCdf, -d1);
    }

    /// <summary>
    /// Computes the d± functions used in Black-Scholes formulas
    /// d±(τ, S/K) = [ln(S/K) + (r-q±σ²/2)τ] / (σ√τ)
    /// </summary>
    private static double ComputeD(int sign, double tau, double moneyness, double r, double q, double sigma)
    {
        if (tau <= 0 || sigma <= 0)
        {
            return sign > 0 ? double.PositiveInfinity : double.NegativeInfinity;
        }

        return (Math.Log(moneyness) + (r - q + sign * 0.5 * sigma * sigma) * tau) / (sigma * Math.Sqrt(tau));
    }

    /// <summary>
    /// Fallback trapezoidal integration for cases where adaptive methods fail
    /// </summary>
    private static double IntegrateTrapezoidal(Func<double, double> f, double a, double b, int n)
    {
        double h = (b - a) / n;
        double sum = 0.5 * (f(a) + f(b));

        for (int i = 1; i < n; i++)
        {
            try
            {
                sum += f(a + i * h);
            }
            catch
            {
                // Skip problematic points
            }
        }

        return h * sum;
    }
}

/// <summary>
/// Integrand for the interest premium calculation
/// </summary>
internal class InterestPremiumIntegrand
{
    private readonly double _tau, _currentBoundary, _strike, _r, _q, _sigma;
    private readonly BoundaryFunction _boundaryFunction;
    private static readonly CumulativeNormalDistribution _normalCdf = new CumulativeNormalDistribution();

    public InterestPremiumIntegrand(double tau, double currentBoundary, BoundaryFunction boundaryFunction,
                                  double strike, double r, double q, double sigma)
    {
        _tau = tau;
        _currentBoundary = currentBoundary;
        _boundaryFunction = boundaryFunction;
        _strike = strike;
        _r = r;
        _q = q;
        _sigma = sigma;
    }

    public double value(double u)
    {
        if (u >= _tau || u < 0) return 0.0;

        double boundaryAtU = _boundaryFunction.Evaluate(u);
        double timeStep = _tau - u;
        
        if (timeStep <= 0 || boundaryAtU <= 1e-10) return 0.0;

        double d_minus = (Math.Log(_currentBoundary / boundaryAtU) + (_r - _q - 0.5 * _sigma * _sigma) * timeStep) / 
                        (_sigma * Math.Sqrt(timeStep));
        
        return _r * _strike * Math.Exp(-_r * timeStep) * QuantLibApiHelper.CallCumNorm(_normalCdf, -d_minus);
    }
}

/// <summary>
/// Integrand for the dividend premium calculation
/// </summary>
internal class DividendPremiumIntegrand
{
    private readonly double _tau, _currentBoundary, _strike, _r, _q, _sigma;
    private readonly BoundaryFunction _boundaryFunction;
    private static readonly CumulativeNormalDistribution _normalCdf = new CumulativeNormalDistribution();

    public DividendPremiumIntegrand(double tau, double currentBoundary, BoundaryFunction boundaryFunction,
                                  double strike, double r, double q, double sigma)
    {
        _tau = tau;
        _currentBoundary = currentBoundary;
        _boundaryFunction = boundaryFunction;
        _strike = strike;
        _r = r;
        _q = q;
        _sigma = sigma;
    }

    public double value(double u)
    {
        if (u >= _tau || u < 0) return 0.0;

        double boundaryAtU = _boundaryFunction.Evaluate(u);
        double timeStep = _tau - u;
        
        if (timeStep <= 0 || boundaryAtU <= 1e-10) return 0.0;

        double d_plus = (Math.Log(_currentBoundary / boundaryAtU) + (_r - _q + 0.5 * _sigma * _sigma) * timeStep) / 
                       (_sigma * Math.Sqrt(timeStep));
        
        return _q * _currentBoundary * Math.Exp(-_q * timeStep) * QuantLibApiHelper.CallCumNorm(_normalCdf, -d_plus);
    }
}

/// <summary>
/// Integrand for lower boundary interest term in smooth-pasting condition
/// </summary>
internal class LowerBoundaryInterestIntegrand
{
    private readonly double _tau, _boundary, _strike, _r, _q, _sigma;
    private readonly BoundaryFunction _boundaryFunction;
    private static readonly NormalDistribution _normalPdf = new NormalDistribution();

    public LowerBoundaryInterestIntegrand(double tau, double boundary, BoundaryFunction boundaryFunction,
                                        double strike, double r, double q, double sigma)
    {
        _tau = tau;
        _boundary = boundary;
        _boundaryFunction = boundaryFunction;
        _strike = strike;
        _r = r;
        _q = q;
        _sigma = sigma;
    }

    public double value(double u)
    {
        if (u >= _tau || u < 0) return 0.0;

        double boundaryAtU = _boundaryFunction.Evaluate(u);
        double timeStep = _tau - u;
        
        if (timeStep <= 0 || boundaryAtU <= 1e-10) return 0.0;

        double d_minus = (Math.Log(_boundary / boundaryAtU) + (_r - _q - 0.5 * _sigma * _sigma) * timeStep) / 
                        (_sigma * Math.Sqrt(timeStep));
        
        return (_r * _strike / _boundary) * Math.Exp(-_r * timeStep) * 
               QuantLibApiHelper.CallNormPdf(_normalPdf, -d_minus) / (_sigma * Math.Sqrt(timeStep));
    }
}

/// <summary>
/// Integrand for lower boundary dividend term in smooth-pasting condition
/// </summary>
internal class LowerBoundaryDividendIntegrand
{
    private readonly double _tau, _boundary, _strike, _r, _q, _sigma;
    private readonly BoundaryFunction _boundaryFunction;
    private static readonly NormalDistribution _normalPdf = new NormalDistribution();
    private static readonly CumulativeNormalDistribution _normalCdf = new CumulativeNormalDistribution();

    public LowerBoundaryDividendIntegrand(double tau, double boundary, BoundaryFunction boundaryFunction,
                                        double strike, double r, double q, double sigma)
    {
        _tau = tau;
        _boundary = boundary;
        _boundaryFunction = boundaryFunction;
        _strike = strike;
        _r = r;
        _q = q;
        _sigma = sigma;
    }

    public double value(double u)
    {
        if (u >= _tau || u < 0) return 0.0;

        double boundaryAtU = _boundaryFunction.Evaluate(u);
        double timeStep = _tau - u;
        
        if (timeStep <= 0 || boundaryAtU <= 1e-10) return 0.0;

        double d_plus = (Math.Log(_boundary / boundaryAtU) + (_r - _q + 0.5 * _sigma * _sigma) * timeStep) / 
                       (_sigma * Math.Sqrt(timeStep));
        
        double densityTerm = QuantLibApiHelper.CallNormPdf(_normalPdf, -d_plus) / (_sigma * Math.Sqrt(timeStep));
        double cdfTerm = QuantLibApiHelper.CallCumNorm(_normalCdf, -d_plus);
        
        return _q * Math.Exp(-_q * timeStep) * (densityTerm + cdfTerm);
    }
}