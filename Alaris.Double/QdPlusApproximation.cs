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
/// Uses Super Halley's method (Equation 17) for robust convergence.
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
    /// <returns>Tuple of (upper boundary, lower boundary) at t=0</returns>
    public (double Upper, double Lower) CalculateBoundaries()
    {
        // Calculate characteristic equation roots
        double h = 1.0 - System.Math.Exp(-_rate * _maturity);
        double omega = 2.0 * (_rate - _dividendYield) / (_volatility * _volatility);
        
        // Lambda values from characteristic equation (Healy Equation 9)
        double lambda1 = CalculateLambda1(h, omega);
        double lambda2 = CalculateLambda2(h, omega);
        
        // Derivatives of lambda w.r.t. h
        double dLambda1Dh = CalculateLambdaDerivative(h, omega, lambda1);
        double dLambda2Dh = CalculateLambdaDerivative(h, omega, lambda2);
        
        // Alpha and beta parameters
        double alpha = 0.5 - (_rate - _dividendYield) / (_volatility * _volatility);
        double beta = alpha * alpha + 2.0 * _rate / (_volatility * _volatility);
        
        // Initial guesses from Ju-Zhong formula
        double upperInitial = CalculateJuZhongInitialGuess(true, lambda1);
        double lowerInitial = CalculateJuZhongInitialGuess(false, lambda2);
        
        // Solve for both boundaries using Super Halley's method
        double upperBoundary = SolveBoundary(upperInitial, lambda1, dLambda1Dh, h, alpha, beta);
        double lowerBoundary = SolveBoundary(lowerInitial, lambda2, dLambda2Dh, h, alpha, beta);
        
        return (upperBoundary, lowerBoundary);
    }
    
    /// <summary>
    /// Calculates lambda1 (upper boundary characteristic root) from Healy (2021) Equation 9.
    /// </summary>
    private double CalculateLambda1(double h, double omega)
    {
        double discriminant = (omega - 1.0) * (omega - 1.0) + 8.0 * _rate / (_volatility * _volatility * h);
        double sqrtDiscriminant = System.Math.Sqrt(System.Math.Max(0.0, discriminant));
        
        double lambda1 = (-(omega - 1.0) + sqrtDiscriminant) / 2.0;
        return lambda1;
    }
    
    /// <summary>
    /// Calculates lambda2 (lower boundary characteristic root) from Healy (2021) Equation 9.
    /// </summary>
    private double CalculateLambda2(double h, double omega)
    {
        double discriminant = (omega - 1.0) * (omega - 1.0) + 8.0 * _rate / (_volatility * _volatility * h);
        double sqrtDiscriminant = System.Math.Sqrt(System.Math.Max(0.0, discriminant));
        
        double lambda2 = (-(omega - 1.0) - sqrtDiscriminant) / 2.0;
        return lambda2;
    }
    
    /// <summary>
    /// Calculates derivative of lambda w.r.t. h for lambda'(h) term in c0.
    /// </summary>
    private double CalculateLambdaDerivative(double h, double omega, double lambda)
    {
        if (System.Math.Abs(h) < NUMERICAL_EPSILON)
            return 0.0;
        
        double discriminant = (omega - 1.0) * (omega - 1.0) + 8.0 * _rate / (_volatility * _volatility * h);
        
        if (discriminant < NUMERICAL_EPSILON)
            return 0.0;
        
        double sqrtDiscriminant = System.Math.Sqrt(discriminant);
        
        // d(lambda)/dh = -4r/(σ²h² * sqrt(discriminant))
        double derivative = -4.0 * _rate / (_volatility * _volatility * h * h * sqrtDiscriminant);
        
        return derivative;
    }
    
    /// <summary>
    /// Calculates initial guess using Ju-Zhong formula (Healy Equation 11-12).
    /// </summary>
    private double CalculateJuZhongInitialGuess(bool isUpper, double lambda)
    {
        double h = 1.0 - System.Math.Exp(-_rate * _maturity);
        
        // A_∞ calculation
        double AInf = _strike * lambda / (lambda - 1.0);
        
        if (System.Math.Abs(h) < NUMERICAL_EPSILON)
            return AInf;
        
        // Ju-Zhong formula coefficients
        double term1 = (1.0 - System.Math.Exp(-_dividendYield * _maturity)) * _strike / _dividendYield;
        double term2 = (1.0 - System.Math.Exp(-_rate * _maturity)) * _strike * lambda / (_rate * (lambda - 1.0));
        
        double guess = AInf + (term1 - term2);
        
        // Ensure reasonable bounds
        if (isUpper)
            guess = System.Math.Max(guess, _strike);
        else
            guess = System.Math.Min(guess, _strike);
        
        return guess;
    }
    
    /// <summary>
    /// Solves for boundary using Super Halley's method (Healy 2021 Equation 17).
    /// </summary>
    /// <remarks>
    /// Super Halley iteration: S*_{n+1} = S*_n - (1 + 0.5*Lf/(1-Lf)) * f/f'
    /// where Lf = f*f''/(f')²
    /// This is more stable than standard Halley's method for negative rates.
    /// </remarks>
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
            {
                // Fall back to bisection if derivative too small
                break;
            }
            
            // Super Halley's method (Equation 17)
            double Lf = f * fPrimePrime / (fPrime * fPrime);
            double correction = f / fPrime;
            
            // Prevent division by zero in denominator
            double denominator = 1.0 - Lf;
            if (System.Math.Abs(denominator) < NUMERICAL_EPSILON)
                denominator = NUMERICAL_EPSILON;
            
            double factor = 1.0 + 0.5 * Lf / denominator;
            
            double newS = S - factor * correction;
            
            // Boundary guards: ensure S stays in reasonable range
            if (newS <= 0.0 || newS > 10.0 * _strike)
            {
                // Fall back to Newton step if Super Halley goes out of bounds
                newS = S - correction;
            }
            
            // Additional guard against negative values
            if (newS <= 0.0)
            {
                newS = S * 0.5;
            }
            
            if (System.Math.Abs(newS - S) < TOLERANCE * System.Math.Abs(S))
                return newS;
            
            S = newS;
        }
        
        return S;
    }
    
    /// <summary>
    /// Evaluates the QD+ refinement equation from Healy (2021) Equation 14.
    /// </summary>
    /// <remarks>
    /// Equation 14: η = η*exp(-qT)*Φ(η*d1) + (λ + c0) * (η(S* - K) - VE) / S*
    /// Rearranged to: f(S*) = η - η*exp(-qT)*Φ(η*d1) - (λ + c0) * (η(S* - K) - VE) / S* = 0
    /// </remarks>
    private double EvaluateRefinementEquation(double S, double lambda, double dLambdaDh,
        double h, double alpha, double beta)
    {
        if (S <= 0.0)
            return double.NaN;
        
        double eta = _isCall ? 1.0 : -1.0;
        
        double VE = CalculateEuropeanPrice(S);
        double theta = CalculateEuropeanTheta(S);
        
        double d1 = CalculateD1(S, _strike, _maturity);
        
        double c0 = CalculateC0(S, lambda, dLambdaDh, h, alpha, beta, theta, VE, eta);
        
        // Left-hand side: η
        double lhs = eta;
        
        // First term on right: η*exp(-qT)*Φ(η*d1)
        double term1 = eta * System.Math.Exp(-_dividendYield * _maturity) * NormalCDF(eta * d1);
        
        // Second term on right: (λ + c0) * (η(S* - K) - VE) / S*
        double intrinsic = eta * (S - _strike);
        double term2 = (lambda + c0) * (intrinsic - VE) / S;
        
        // Return f(S*) = lhs - term1 - term2
        return lhs - term1 - term2;
    }
    
    /// <summary>
    /// Calculates c₀ from Healy (2021) page 6 after Equation 14.
    /// </summary>
    /// <remarks>
    /// c0 = -((1-h)α)/(2λ + β - 1) * (1/h - Θ(S*)/(r(η(S* - K) - VE))) + λ'(h)/(2λ + β - 1)
    /// where Θ is the time derivative (theta) of the European option price.
    /// </remarks>
    private double CalculateC0(double S, double lambda, double dLambdaDh, double h,
        double alpha, double beta, double theta, double VE, double eta)
    {
        double intrinsic = eta * (S - _strike);
        double denominator = intrinsic - VE;
        
        double lambdaDenom = 2.0 * lambda + beta - 1.0;
        
        // Prevent division by zero
        if (System.Math.Abs(lambdaDenom) < NUMERICAL_EPSILON)
            return 0.0;
        
        // When at-the-money or denominator near zero, c0 simplifies
        if (System.Math.Abs(denominator) < NUMERICAL_EPSILON || System.Math.Abs(_rate) < NUMERICAL_EPSILON)
        {
            return dLambdaDh / lambdaDenom;
        }
        
        // Prevent division by zero in h
        if (System.Math.Abs(h) < NUMERICAL_EPSILON)
        {
            return dLambdaDh / lambdaDenom;
        }
        
        // First part: -((1-h)α)/(2λ + β - 1) * (1/h - Θ/(r*(intrinsic - VE)))
        double factor1 = -((1.0 - h) * alpha) / lambdaDenom;
        double term1 = 1.0 / h;
        double term2 = theta / (_rate * denominator);
        double part1 = factor1 * (term1 - term2);
        
        // Second part: λ'(h)/(2λ + β - 1)
        double part2 = dLambdaDh / lambdaDenom;
        
        return part1 + part2;
    }
    
    /// <summary>
    /// First derivative of the refinement equation (numerical).
    /// </summary>
    private double EvaluateRefinementDerivative(double S, double lambda, double dLambdaDh,
        double h, double alpha, double beta)
    {
        double dS = System.Math.Max(S * 1e-5, 1e-8);
        double f1 = EvaluateRefinementEquation(S + dS, lambda, dLambdaDh, h, alpha, beta);
        double f0 = EvaluateRefinementEquation(S, lambda, dLambdaDh, h, alpha, beta);
        
        if (double.IsNaN(f1) || double.IsNaN(f0))
            return 0.0;
        
        return (f1 - f0) / dS;
    }
    
    /// <summary>
    /// Second derivative of the refinement equation (numerical).
    /// </summary>
    private double EvaluateRefinementSecondDerivative(double S, double lambda, double dLambdaDh,
        double h, double alpha, double beta)
    {
        double dS = System.Math.Max(S * 1e-5, 1e-8);
        double fPlus = EvaluateRefinementDerivative(S + dS, lambda, dLambdaDh, h, alpha, beta);
        double fMinus = EvaluateRefinementDerivative(S - dS, lambda, dLambdaDh, h, alpha, beta);
        
        if (double.IsNaN(fPlus) || double.IsNaN(fMinus))
            return 0.0;
        
        return (fPlus - fMinus) / (2.0 * dS);
    }
    
    /// <summary>
    /// Calculates European option price using Black-Scholes formula.
    /// </summary>
    private double CalculateEuropeanPrice(double S)
    {
        if (S <= 0.0)
            return 0.0;
        
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
    /// <remarks>
    /// Theta = -S*exp(-qT)*φ(d1)*σ/(2√T) + q*S*exp(-qT)*Φ(±d1) - r*K*exp(-rT)*Φ(±d2)
    /// where + is for call, - is for put in the Φ terms.
    /// </remarks>
    private double CalculateEuropeanTheta(double S)
    {
        if (S <= 0.0 || _maturity < NUMERICAL_EPSILON)
            return 0.0;
        
        double d1 = CalculateD1(S, _strike, _maturity);
        double d2 = d1 - _volatility * System.Math.Sqrt(_maturity);
        
        double sqrtT = System.Math.Sqrt(_maturity);
        double discountFactor = System.Math.Exp(-_rate * _maturity);
        double dividendFactor = System.Math.Exp(-_dividendYield * _maturity);
        
        // First term (same for call and put)
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
    /// <remarks>
    /// d1 = (ln(S/K) + (r - q + 0.5σ²)T) / (σ√T)
    /// </remarks>
    private double CalculateD1(double S, double K, double T)
    {
        if (T < NUMERICAL_EPSILON)
        {
            // At maturity, use limit behavior
            return S > K ? 10.0 : (S < K ? -10.0 : 0.0);
        }
        
        if (S <= 0.0 || K <= 0.0)
            return -10.0;
        
        double sqrtT = System.Math.Sqrt(T);
        double numerator = System.Math.Log(S / K) + 
                          (_rate - _dividendYield + 0.5 * _volatility * _volatility) * T;
        return numerator / (_volatility * sqrtT);
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
        // Abramowitz and Stegun approximation
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