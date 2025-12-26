// CRMF001A.cs - Core mathematical functions (Erf, NormalCDF, Black-Scholes)

namespace Alaris.Core.Math;

/// <summary>
/// Core mathematical functions. Thread-safe, zero-allocation, bounded error (Erf max 1.5e-7).
/// </summary>
public static class CRMF001A
{
    /// <summary>
    /// Machine epsilon for double precision (2.22e-16).
    /// </summary>
    public const double MachineEpsilon = 2.220446049250313e-16;

    /// <summary>
    /// √(2π) for normal distribution calculations.
    /// </summary>
    public const double SqrtTwoPi = 2.5066282746310005;

    /// <summary>
    /// 1/√(2π) for PDF calculations.
    /// </summary>
    public const double InvSqrtTwoPi = 0.3989422804014327;

    /// <summary>
    /// √2 for erf to CDF conversion.
    /// </summary>
    public const double Sqrt2 = 1.4142135623730951;

    /// <summary>
    /// Default tolerance for root-finding algorithms.
    /// </summary>
    public const double DefaultTolerance = 1e-10;

    /// <summary>
    /// Maximum IV bound (500% annualized).
    /// </summary>
    public const double MaxVolatility = 5.0;

    /// <summary>
    /// Minimum IV bound (0.1% annualized).
    /// </summary>
    public const double MinVolatility = 0.001;

    /// <summary>
    /// Minimum time to expiry (1 trading day).
    /// </summary>
    public const double MinTimeToExpiry = 1.0 / 252.0;

    /// <summary>
    /// Trading days per year.
    /// </summary>
    public const int TradingDaysPerYear = 252;

    /// <summary>
    /// Error function erf(x) using Abramowitz and Stegun formula 7.1.26.
    /// </summary>
    /// <param name="x">Input value.</param>
    /// <returns>erf(x) ∈ [-1, 1]</returns>
    /// <remarks>
    /// Maximum error: 1.5e-7 (Abramowitz & Stegun 7.1.26)
    /// 
    /// Coefficients:
    /// a₁ = 0.254829592,  a₂ = -0.284496736, a₃ = 1.421413741
    /// a₄ = -1.453152027, a₅ = 1.061405429,  p = 0.3275911
    /// </remarks>
    public static double Erf(double x)
    {
        // Save sign
        int sign = x >= 0 ? 1 : -1;
        x = System.Math.Abs(x);

        // Abramowitz & Stegun constants
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        double t = 1.0 / (1.0 + (p * x));
        double t2 = t * t;
        double t3 = t2 * t;
        double t4 = t3 * t;
        double t5 = t4 * t;

        // Horner's form for the polynomial
        double poly = (a1 * t) + (a2 * t2) + (a3 * t3) + (a4 * t4) + (a5 * t5);
        double y = 1.0 - (poly * System.Math.Exp(-x * x));

        return sign * y;
    }

    /// <summary>
    /// Standard normal cumulative distribution function Φ(x).
    /// </summary>
    /// <param name="x">Input value.</param>
    /// <returns>Φ(x) = P(Z ≤ x) ∈ [0, 1]</returns>
    /// <remarks>
    /// Computes Φ(x) = (1 + erf(x/√2))/2
    /// Accuracy: inherits from Erf (max error 1.5e-7)
    /// </remarks>
    public static double NormalCDF(double x)
    {
        return 0.5 * (1.0 + Erf(x / Sqrt2));
    }

    /// <summary>
    /// Standard normal probability density function φ(x).
    /// </summary>
    /// <param name="x">Input value.</param>
    /// <returns>φ(x) = exp(-x²/2)/√(2π)</returns>
    /// <remarks>
    /// Full double precision. For |x| > 38, returns 0 (underflow).
    /// </remarks>
    public static double NormalPDF(double x)
    {
        return InvSqrtTwoPi * System.Math.Exp(-0.5 * x * x);
    }

    /// <summary>
    /// Black-Scholes d1 parameter.
    /// </summary>
    /// <param name="S">Spot price (must be > 0).</param>
    /// <param name="K">Strike price (must be > 0).</param>
    /// <param name="tau">Time to expiry in years (must be > 0).</param>
    /// <param name="sigma">Volatility (must be > 0).</param>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <returns>d1 = (ln(S/K) + (r - q + σ²/2)τ) / (σ√τ)</returns>
    public static double BSd1(double S, double K, double tau, double sigma, double r, double q)
    {
        double sqrtTau = System.Math.Sqrt(tau);
        return (System.Math.Log(S / K) + ((r - q + (0.5 * sigma * sigma)) * tau)) / (sigma * sqrtTau);
    }

    /// <summary>
    /// Black-Scholes d2 parameter.
    /// </summary>
    /// <param name="d1">The d1 parameter.</param>
    /// <param name="sigma">Volatility.</param>
    /// <param name="tau">Time to expiry in years.</param>
    /// <returns>d2 = d1 - σ√τ</returns>
    public static double BSd2(double d1, double sigma, double tau)
    {
        return d1 - (sigma * System.Math.Sqrt(tau));
    }

    /// <summary>
    /// Black-Scholes European option price.
    /// </summary>
    /// <param name="S">Spot price (must be > 0).</param>
    /// <param name="K">Strike price (must be > 0).</param>
    /// <param name="tau">Time to expiry in years (must be > 0).</param>
    /// <param name="sigma">Volatility (must be > 0).</param>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <returns>Option price.</returns>
    /// <remarks>
    /// Uses put-call parity for puts: P = C - S*e^(-qτ) + K*e^(-rτ)
    /// </remarks>
    public static double BSPrice(double S, double K, double tau, double sigma, double r, double q, bool isCall)
    {
        double d1 = BSd1(S, K, tau, sigma, r, q);
        double d2 = BSd2(d1, sigma, tau);

        double discountDividend = System.Math.Exp(-q * tau);
        double discountRate = System.Math.Exp(-r * tau);

        if (isCall)
        {
            return (S * discountDividend * NormalCDF(d1)) - (K * discountRate * NormalCDF(d2));
        }
        else
        {
            return (K * discountRate * NormalCDF(-d2)) - (S * discountDividend * NormalCDF(-d1));
        }
    }

    /// <summary>
    /// Black-Scholes Vega (∂V/∂σ).
    /// </summary>
    /// <param name="S">Spot price.</param>
    /// <param name="K">Strike price.</param>
    /// <param name="tau">Time to expiry in years.</param>
    /// <param name="sigma">Volatility.</param>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <returns>Vega = S*e^(-qτ)*φ(d1)*√τ</returns>
    /// <remarks>
    /// Vega is identical for calls and puts.
    /// </remarks>
    public static double BSVega(double S, double K, double tau, double sigma, double r, double q)
    {
        double d1 = BSd1(S, K, tau, sigma, r, q);
        double sqrtTau = System.Math.Sqrt(tau);
        double discountDividend = System.Math.Exp(-q * tau);

        return S * discountDividend * NormalPDF(d1) * sqrtTau;
    }

    /// <summary>
    /// Black-Scholes Delta (∂V/∂S).
    /// </summary>
    /// <param name="S">Spot price.</param>
    /// <param name="K">Strike price.</param>
    /// <param name="tau">Time to expiry in years.</param>
    /// <param name="sigma">Volatility.</param>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <returns>Delta: Call = e^(-qτ)*Φ(d1), Put = -e^(-qτ)*Φ(-d1)</returns>
    public static double BSDelta(double S, double K, double tau, double sigma, double r, double q, bool isCall)
    {
        double d1 = BSd1(S, K, tau, sigma, r, q);
        double discountDividend = System.Math.Exp(-q * tau);

        if (isCall)
        {
            return discountDividend * NormalCDF(d1);
        }
        else
        {
            return -discountDividend * NormalCDF(-d1);
        }
    }

    /// <summary>
    /// Black-Scholes Gamma (∂²V/∂S²).
    /// </summary>
    /// <param name="S">Spot price.</param>
    /// <param name="K">Strike price.</param>
    /// <param name="tau">Time to expiry in years.</param>
    /// <param name="sigma">Volatility.</param>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <returns>Gamma = e^(-qτ)*φ(d1)/(S*σ*√τ)</returns>
    /// <remarks>
    /// Gamma is identical for calls and puts.
    /// </remarks>
    public static double BSGamma(double S, double K, double tau, double sigma, double r, double q)
    {
        double d1 = BSd1(S, K, tau, sigma, r, q);
        double sqrtTau = System.Math.Sqrt(tau);
        double discountDividend = System.Math.Exp(-q * tau);

        return discountDividend * NormalPDF(d1) / (S * sigma * sqrtTau);
    }

    /// <summary>
    /// Black-Scholes Theta (∂V/∂τ).
    /// </summary>
    /// <param name="S">Spot price.</param>
    /// <param name="K">Strike price.</param>
    /// <param name="tau">Time to expiry in years.</param>
    /// <param name="sigma">Volatility.</param>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <returns>Theta (per year, divide by 252 for daily).</returns>
    public static double BSTheta(double S, double K, double tau, double sigma, double r, double q, bool isCall)
    {
        double d1 = BSd1(S, K, tau, sigma, r, q);
        double d2 = BSd2(d1, sigma, tau);
        double sqrtTau = System.Math.Sqrt(tau);
        double discountDividend = System.Math.Exp(-q * tau);
        double discountRate = System.Math.Exp(-r * tau);

        double term1 = -(S * discountDividend * NormalPDF(d1) * sigma) / (2 * sqrtTau);

        if (isCall)
        {
            return term1 + (q * S * discountDividend * NormalCDF(d1)) 
                         - (r * K * discountRate * NormalCDF(d2));
        }
        else
        {
            return term1 - (q * S * discountDividend * NormalCDF(-d1)) 
                         + (r * K * discountRate * NormalCDF(-d2));
        }
    }

    /// <summary>
    /// Computes Black-Scholes implied volatility using Newton-Raphson method.
    /// </summary>
    /// <param name="S">Spot price.</param>
    /// <param name="K">Strike price.</param>
    /// <param name="tau">Time to expiry in years.</param>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <param name="marketPrice">Market price of the option.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <param name="tolerance">Convergence tolerance (default 1e-7).</param>
    /// <param name="maxIterations">Maximum iterations (default 100).</param>
    /// <returns>Implied volatility, or NaN if not converged.</returns>
    /// <remarks>
    /// Uses Newton-Raphson: σ_{n+1} = σ_n - (C(σ_n) - C_market) / Vega(σ_n)
    /// 
    /// References:
    /// - Orlando G, Taglialatela G. A review on implied volatility calculation. 
    ///   Journal of Computational and Applied Mathematics. 2017;320:202-20.
    /// </remarks>
    public static double BSImpliedVolatility(
        double S, double K, double tau, double r, double q,
        double marketPrice, bool isCall,
        double tolerance = 1e-7,
        int maxIterations = 100)
    {
        // Validate inputs
        if (marketPrice <= 0 || S <= 0 || K <= 0 || tau <= 0)
        {
            return double.NaN;
        }

        // Check intrinsic value bounds
        double intrinsic = isCall 
            ? System.Math.Max((S * System.Math.Exp(-q * tau)) - (K * System.Math.Exp(-r * tau)), 0)
            : System.Math.Max((K * System.Math.Exp(-r * tau)) - (S * System.Math.Exp(-q * tau)), 0);
        
        if (marketPrice < intrinsic)
        {
            return double.NaN; // Price below intrinsic value - no valid IV
        }
        
        // Initial guess: Brenner-Subrahmanyam approximation
        // σ ≈ √(2π/T) * C / S
        double sigma = System.Math.Sqrt(2 * System.Math.PI / tau) * marketPrice / S;
        sigma = ClampVolatility(sigma);
        
        for (int i = 0; i < maxIterations; i++)
        {
            double price = BSPrice(S, K, tau, sigma, r, q, isCall);
            double vega = BSVega(S, K, tau, sigma, r, q);
            
            if (vega < MachineEpsilon)
            {
                // Vega too small - try bisection fallback
                return BSImpliedVolatilityBisection(S, K, tau, r, q, marketPrice, isCall, tolerance);
            }
            
            double priceDiff = price - marketPrice;
            
            if (System.Math.Abs(priceDiff) < tolerance)
            {
                return sigma;
            }
            
            double newSigma = sigma - (priceDiff / vega);
            
            // Ensure sigma stays in valid range
            if (newSigma <= 0 || newSigma > MaxVolatility)
            {
                // Use bisection fallback
                return BSImpliedVolatilityBisection(S, K, tau, r, q, marketPrice, isCall, tolerance);
            }
            
            sigma = newSigma;
        }
        
        // Did not converge
        return double.NaN;
    }

    /// <summary>
    /// Bisection fallback for IV calculation when Newton-Raphson fails.
    /// </summary>
    private static double BSImpliedVolatilityBisection(
        double S, double K, double tau, double r, double q,
        double marketPrice, bool isCall,
        double tolerance = 1e-7,
        int maxIterations = 100)
    {
        double low = MinVolatility;
        double high = MaxVolatility;
        
        for (int i = 0; i < maxIterations; i++)
        {
            double mid = (low + high) / 2;
            double price = BSPrice(S, K, tau, mid, r, q, isCall);
            
            if (System.Math.Abs(price - marketPrice) < tolerance)
            {
                return mid;
            }
            
            if (price < marketPrice)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        
        return (low + high) / 2; // Return best estimate
    }

    /// <summary>
    /// Validates that a volatility value is within bounds.
    /// </summary>
    /// <param name="sigma">Volatility to validate.</param>
    /// <returns>True if valid.</returns>
    public static bool IsValidVolatility(double sigma)
    {
        return sigma >= MinVolatility && sigma <= MaxVolatility && !double.IsNaN(sigma);
    }

    /// <summary>
    /// Clamps volatility to valid bounds.
    /// </summary>
    /// <param name="sigma">Volatility to clamp.</param>
    /// <returns>Clamped volatility.</returns>
    public static double ClampVolatility(double sigma)
    {
        return System.Math.Clamp(sigma, MinVolatility, MaxVolatility);
    }

    /// <summary>
    /// Converts days to expiry to years.
    /// </summary>
    /// <param name="dte">Days to expiry.</param>
    /// <returns>Time in years.</returns>
    public static double DteToYears(int dte)
    {
        return System.Math.Max(dte, 1) / (double)TradingDaysPerYear;
    }

    /// <summary>
    /// Converts years to days to expiry.
    /// </summary>
    /// <param name="tau">Time in years.</param>
    /// <returns>Days to expiry (rounded).</returns>
    public static int YearsToDte(double tau)
    {
        return (int)System.Math.Round(tau * TradingDaysPerYear);
    }
}
