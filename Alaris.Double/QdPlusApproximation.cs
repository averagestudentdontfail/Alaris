namespace Alaris.Double;

/// <summary>
/// QD+ approximation for American option early exercise boundaries under negative interest rates.
/// Provides initial boundary estimates for the double boundary regime where q &lt; r &lt; 0.
/// </summary>
/// <remarks>
/// <para>
/// Implements Healy (2021) adaptation of Li (2005) QD+ algorithm for negative rates.
/// This class provides initial boundary approximations that can be refined using Kim's integral equation.
/// </para>
/// <para>
/// Architecture parallel to QuantLib:
/// - Single boundary (r ≥ 0): QdPlus → QdFp with Chebyshev polynomials
/// - Double boundary (q &lt; r &lt; 0): QdPlusApproximation → Kim solver with fixed point iteration
/// </para>
/// <para>
/// Reference: Healy, J. (2021). "Pricing American Options Under Negative Rates", Equations 8-17.
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
    
    private const double TOLERANCE = 1e-6;
    private const int MAX_ITERATIONS = 50;
    private const double NUMERICAL_EPSILON = 1e-10;
    
    /// <summary>
    /// Initializes the QD+ approximation solver for double boundaries.
    /// </summary>
    /// <param name="spot">Current asset price S</param>
    /// <param name="strike">Strike price K</param>
    /// <param name="maturity">Time to maturity T</param>
    /// <param name="rate">Risk-free interest rate r</param>
    /// <param name="dividendYield">Dividend yield q</param>
    /// <param name="volatility">Volatility σ</param>
    /// <param name="isCall">True for call options, false for put options</param>
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
    /// Computes both exercise boundaries using the QD+ approximation.
    /// </summary>
    /// <returns>Tuple of (upper boundary, lower boundary). Returns (K, K) if boundaries cross.</returns>
    public (double Upper, double Lower) CalculateBoundaries()
    {
        var (lambda1, lambda2, h, alpha, beta) = CalculateLambdas();
        double dLambda1Dh = CalculateLambdaDerivative(lambda1, alpha, beta, h);
        double dLambda2Dh = CalculateLambdaDerivative(lambda2, alpha, beta, h);
        
        double upperInitial = GetUpperBoundaryInitialGuess();
        double upperBoundary = SolveBoundary(upperInitial, lambda1, dLambda1Dh, h, alpha, beta);
        
        double lowerInitial = GetLowerBoundaryInitialGuess();
        double lowerBoundary = SolveBoundary(lowerInitial, lambda2, dLambda2Dh, h, alpha, beta);
        
        if (BoundariesCross(upperBoundary, lowerBoundary))
        {
            return (_strike, _strike);
        }
        
        return (upperBoundary, lowerBoundary);
    }
    
    /// <summary>
    /// Calculates λ₁ (negative) and λ₂ (positive) from Healy (2021) Equation 9.
    /// </summary>
    private (double lambda1, double lambda2, double h, double alpha, double beta) CalculateLambdas()
    {
        double h = 1.0 - System.Math.Exp(-_rate * _maturity);
        double sigma2 = _volatility * _volatility;
        
        double alpha = 2.0 * _rate / sigma2;
        double beta = 2.0 * (_rate - _dividendYield) / sigma2;
        
        double discriminant = System.Math.Sqrt((beta - 1) * (beta - 1) + 4.0 * alpha / h);
        
        double lambda1 = (-(beta - 1) - discriminant) / 2.0;
        double lambda2 = (-(beta - 1) + discriminant) / 2.0;
        
        return (lambda1, lambda2, h, alpha, beta);
    }
    
    /// <summary>
    /// Calculates ∂λ/∂h needed for c₀ in Healy (2021) Equation 14.
    /// </summary>
    private double CalculateLambdaDerivative(double lambda, double alpha, double beta, double h)
    {
        double discriminant = System.Math.Sqrt((beta - 1) * (beta - 1) + 4.0 * alpha / h);
        double sign = lambda < 0 ? -1.0 : 1.0;
        return -sign * alpha / (h * h * discriminant);
    }
    
    /// <summary>
    /// Gets the initial guess for the upper boundary from Healy (2021) page 5.
    /// </summary>
    private double GetUpperBoundaryInitialGuess()
    {
        if (_isCall)
        {
            return _strike;
        }
        else
        {
            return _strike * System.Math.Min(1.0, _rate / _dividendYield);
        }
    }
    
    /// <summary>
    /// Gets the initial guess for the lower boundary from Healy (2021) page 5.
    /// </summary>
    private double GetLowerBoundaryInitialGuess()
    {
        if (_isCall)
        {
            return _strike * System.Math.Max(1.0, _rate / _dividendYield);
        }
        else
        {
            return _strike;
        }
    }
    
    /// <summary>
    /// Solves for a single boundary using Super Halley's method (Healy 2021 Equation 17).
    /// </summary>
    private double SolveBoundary(double initialGuess, double lambda, double dLambdaDh, 
        double h, double alpha, double beta)
    {
        double S = initialGuess;
        
        for (int iter = 0; iter < MAX_ITERATIONS; iter++)
        {
            double f = EvaluateRefinementEquation(S, lambda, dLambdaDh, h, alpha, beta);
            
            if (System.Math.Abs(f) < TOLERANCE)
                return S;
            
            double fPrime = EvaluateRefinementDerivative(S, lambda, dLambdaDh, h, alpha, beta);
            double fPrimePrime = EvaluateRefinementSecondDerivative(S, lambda, dLambdaDh, h, alpha, beta);
            
            if (System.Math.Abs(fPrime) < NUMERICAL_EPSILON)
                break;
            
            double Lf = f * fPrimePrime / (fPrime * fPrime);
            double correction = f / fPrime;
            double factor = (1.0 + 0.5 * Lf / (1.0 - Lf));
            
            S = S - factor * correction;
            
            if (S <= 0 || S > 10 * _strike)
            {
                S = S + factor * correction - correction;
            }
            
            if (System.Math.Abs(correction) < TOLERANCE * System.Math.Abs(S))
                return S;
        }
        
        return S;
    }
    
    /// <summary>
    /// Evaluates the QD+ refinement equation from Healy (2021) Equation 14.
    /// </summary>
    private double EvaluateRefinementEquation(double S, double lambda, double dLambdaDh,
        double h, double alpha, double beta)
    {
        double eta = _isCall ? 1.0 : -1.0;
        
        double VE = CalculateEuropeanPrice(S);
        double theta = CalculateEuropeanTheta(S);
        
        double d1 = CalculateD1(S, _strike, _maturity);
        
        double c0 = CalculateC0(S, lambda, dLambdaDh, h, alpha, beta, theta, VE, eta);
        
        double lhs = eta;
        double term1 = eta * System.Math.Exp(-_dividendYield * _maturity) * NormalCDF(eta * d1);
        double intrinsic = eta * (S - _strike);
        double term2 = (lambda + c0) * (intrinsic - VE) / S;
        
        return lhs - term1 - term2;
    }
    
    /// <summary>
    /// Calculates c₀ from Healy (2021) page 6 after Equation 14.
    /// </summary>
    private double CalculateC0(double S, double lambda, double dLambdaDh, double h,
        double alpha, double beta, double theta, double VE, double eta)
    {
        double intrinsic = eta * (S - _strike);
        double denominator = intrinsic - VE;
        
        if (System.Math.Abs(denominator) < NUMERICAL_EPSILON)
            return 0.0;
        
        double factor1 = -((1.0 - h) * alpha) / (2.0 * lambda + beta - 1.0);
        double term1 = 1.0 / h;
        double term2 = theta / (_rate * denominator);
        double part1 = factor1 * (term1 - term2);
        
        double part2 = dLambdaDh / (2.0 * lambda + beta - 1.0);
        
        return part1 + part2;
    }
    
    /// <summary>
    /// First derivative of the refinement equation.
    /// </summary>
    private double EvaluateRefinementDerivative(double S, double lambda, double dLambdaDh,
        double h, double alpha, double beta)
    {
        double dS = S * 0.0001;
        double f1 = EvaluateRefinementEquation(S + dS, lambda, dLambdaDh, h, alpha, beta);
        double f0 = EvaluateRefinementEquation(S, lambda, dLambdaDh, h, alpha, beta);
        return (f1 - f0) / dS;
    }
    
    /// <summary>
    /// Second derivative of the refinement equation.
    /// </summary>
    private double EvaluateRefinementSecondDerivative(double S, double lambda, double dLambdaDh,
        double h, double alpha, double beta)
    {
        double dS = S * 0.0001;
        double fPlus = EvaluateRefinementDerivative(S + dS, lambda, dLambdaDh, h, alpha, beta);
        double fMinus = EvaluateRefinementDerivative(S - dS, lambda, dLambdaDh, h, alpha, beta);
        return (fPlus - fMinus) / (2.0 * dS);
    }
    
    /// <summary>
    /// Calculates European option price using Black-Scholes formula.
    /// </summary>
    private double CalculateEuropeanPrice(double S)
    {
        double d1 = CalculateD1(S, _strike, _maturity);
        double d2 = d1 - _volatility * System.Math.Sqrt(_maturity);
        
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
    /// Calculates theta (time derivative) of European option price.
    /// </summary>
    private double CalculateEuropeanTheta(double S)
    {
        double d1 = CalculateD1(S, _strike, _maturity);
        double d2 = d1 - _volatility * System.Math.Sqrt(_maturity);
        
        double sqrtT = System.Math.Sqrt(_maturity);
        double discountFactor = System.Math.Exp(-_rate * _maturity);
        double dividendFactor = System.Math.Exp(-_dividendYield * _maturity);
        
        double term1 = -(S * dividendFactor * NormalPDF(d1) * _volatility) / (2.0 * sqrtT);
        
        if (_isCall)
        {
            double term2 = _dividendYield * S * dividendFactor * NormalCDF(d1);
            double term3 = -_rate * _strike * discountFactor * NormalCDF(d2);
            return term1 + term2 + term3;
        }
        else
        {
            double term2 = -_dividendYield * S * dividendFactor * NormalCDF(-d1);
            double term3 = _rate * _strike * discountFactor * NormalCDF(-d2);
            return term1 + term2 + term3;
        }
    }
    
    /// <summary>
    /// Calculates d₁ from Black-Scholes formula.
    /// </summary>
    private double CalculateD1(double S, double K, double T)
    {
        if (T < NUMERICAL_EPSILON)
            return S > K ? 10.0 : -10.0;
        
        double sqrtT = System.Math.Sqrt(T);
        double numerator = System.Math.Log(S / K) + (_rate - _dividendYield + 0.5 * _volatility * _volatility) * T;
        return numerator / (_volatility * sqrtT);
    }
    
    /// <summary>
    /// Checks if boundaries cross (invalid configuration).
    /// </summary>
    private bool BoundariesCross(double upper, double lower)
    {
        if (_isCall)
        {
            return upper <= lower;
        }
        else
        {
            return lower >= upper;
        }
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
    /// Standard normal probability density function.
    /// </summary>
    private double NormalPDF(double x)
    {
        return System.Math.Exp(-0.5 * x * x) / System.Math.Sqrt(2.0 * System.Math.PI);
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
        x = System.Math.Abs(x);
        
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * System.Math.Exp(-x * x);
        
        return sign * y;
    }
}