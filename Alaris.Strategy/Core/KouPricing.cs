using System.Numerics;
using Alaris.Strategy.Core.Numerical;

namespace Alaris.Strategy.Core;

/// <summary>
/// Production-grade Kou model pricing using characteristic function integration.
/// Implements semi-analytical pricing for double-exponential jump-diffusion model.
///
/// The Kou (2002) model extends Black-Scholes with asymmetric jumps:
/// - Uses Fourier inversion for option pricing
/// - Handles fat tails and skewness
/// - Calibrates to market implied volatility smile
///
/// References:
/// Kou (2002) "A Jump-Diffusion Model for Option Pricing", Management Science
/// Carr and Madan (1999) "Option Valuation Using the Fast Fourier Transform"
/// </summary>
public static class KouPricing
{
    private const double MinIV = 0.001;
    private const double MaxIV = 5.0;
    private const int MaxNewtonIterations = 50;
    private const double IVTolerance = 1e-6;

    /// <summary>
    /// Computes call option price using Kou model characteristic function.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <param name="params">Kou model parameters.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <returns>Option price.</returns>
    public static double ComputePrice(
        double spot,
        double strike,
        double timeToExpiry,
        KouParameters @params,
        bool isCall = true)
    {
        ArgumentNullException.ThrowIfNull(@params);

        if (spot <= 0 || strike <= 0 || timeToExpiry <= 0)
        {
            throw new ArgumentException("Spot, strike, and time to expiry must be positive.");
        }

        // Compute using characteristic function approach
        double discountFactor = Math.Exp(-@params.RiskFreeRate * timeToExpiry);
        double forwardFactor = Math.Exp(-@params.DividendYield * timeToExpiry);

        // Use Carr-Madan formula: C(K) = exp(-alpha*k) / pi * integral...
        // where k = log(K), alpha is damping parameter
        double logStrike = Math.Log(strike);
        double alpha = 1.5; // Damping parameter for call options

        Complex Integrand(double v)
        {
            if (v < 1e-10)
            {
                return Complex.Zero;
            }

            Complex iV = new Complex(0, v);
            Complex charFunc = CharacteristicFunction(v - ((alpha + 1) * Complex.ImaginaryOne), spot, timeToExpiry, @params);

            // Carr-Madan denominator: α² + α - v² + i*v*(2α + 1)
            Complex denominator = (alpha * alpha) + alpha - (v * v) + (iV * ((2 * alpha) + 1));

            return Complex.Exp(-iV * logStrike) * charFunc / denominator;
        }

        // Numerical integration
        (double integralValue, double _) = AdaptiveIntegration.IntegrateToInfinity(
            v => Integrand(v).Real,
            0,
            absoluteTolerance: 1e-6,
            relativeTolerance: 1e-4);

        double callPrice = discountFactor * Math.Exp(-alpha * logStrike) * integralValue / Math.PI;

        if (isCall)
        {
            return Math.Max(callPrice, 0);
        }
        else
        {
            // Put-call parity
            double putPrice = callPrice - (spot * forwardFactor) + (strike * discountFactor);
            return Math.Max(putPrice, 0);
        }
    }

    /// <summary>
    /// Computes implied volatility from Kou model using Newton-Raphson iteration.
    /// Production implementation replacing moment-matching approximation.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <param name="params">Kou model parameters.</param>
    /// <returns>Implied volatility.</returns>
    public static double ComputeImpliedVolatility(
        double spot,
        double strike,
        double timeToExpiry,
        KouParameters @params)
    {
        // Get theoretical Kou price
        bool isCall = strike >= spot;
        double kouPrice = ComputePrice(spot, strike, timeToExpiry, @params, isCall);

        // Use Newton-Raphson to find IV that matches this price
        double iv = @params.Sigma; // Initial guess from diffusion volatility

        for (int iter = 0; iter < MaxNewtonIterations; iter++)
        {
            // Compute Black-Scholes price and vega at current IV
            double bsPrice = BlackScholesPrice(spot, strike, timeToExpiry,
                @params.RiskFreeRate, @params.DividendYield, iv, isCall);
            double vega = BlackScholesVega(spot, strike, timeToExpiry,
                @params.RiskFreeRate, @params.DividendYield, iv);

            double priceDiff = bsPrice - kouPrice;

            // Check convergence
            if (Math.Abs(priceDiff) < IVTolerance)
            {
                return Math.Clamp(iv, MinIV, MaxIV);
            }

            // Newton step
            if (vega > 1e-10)
            {
                iv -= priceDiff / vega;
                iv = Math.Clamp(iv, MinIV, MaxIV);
            }
            else
            {
                break;
            }
        }

        // Fallback to bisection
        return BisectionIVSolver(spot, strike, timeToExpiry, @params, kouPrice, isCall);
    }

    /// <summary>
    /// Kou model characteristic function.
    /// This captures the jump-diffusion dynamics including asymmetric jumps.
    /// </summary>
    private static Complex CharacteristicFunction(
        Complex u,
        double spot,
        double timeToExpiry,
        KouParameters @params)
    {
        Complex i = Complex.ImaginaryOne;

        double sigma = @params.Sigma;
        double lambda = @params.Lambda;
        double p = @params.P;
        double eta1 = @params.Eta1;
        double eta2 = @params.Eta2;
        double r = @params.RiskFreeRate;
        double d = @params.DividendYield;

        // Martingale correction: kappa = E[e^Y - 1] = E[e^Y] - 1
        // For double exponential: E[e^Y] = p*eta1/(eta1-1) + (1-p)*eta2/(eta2+1)
        double kappa = (p * eta1 / (eta1 - 1)) + ((1 - p) * eta2 / (eta2 + 1)) - 1.0;

        // Characteristic exponent with martingale drift correction
        // psi(u) = (r - d - 0.5*sigma^2 - lambda*kappa)*i*u - 0.5*sigma^2*u^2 + lambda*jump_transform
        Complex diffusionTerm = ((r - d - (lambda * kappa)) * i * u) - (0.5 * sigma * sigma * u * (u + i));

        // Jump transform: E[exp(i*u*Y)] where Y = log(V)
        // For double exponential: integral of exp(i*u*y) * f_Y(y) dy
        Complex jumpTransform = Complex.Zero;
        if (lambda > 0)
        {
            // Upward jump contribution
            Complex upwardTerm = p * eta1 / (eta1 - (i * u));

            // Downward jump contribution
            Complex downwardTerm = (1 - p) * eta2 / (eta2 + (i * u));

            jumpTransform = lambda * (upwardTerm + downwardTerm - 1);
        }

        Complex exponent = (diffusionTerm + jumpTransform) * timeToExpiry;

        return Complex.Exp(exponent + (i * u * Math.Log(spot)));
    }

    /// <summary>
    /// Black-Scholes option price for IV solving.
    /// </summary>
    private static double BlackScholesPrice(
        double spot,
        double strike,
        double timeToExpiry,
        double riskFreeRate,
        double dividendYield,
        double volatility,
        bool isCall)
    {
        double d1 = (Math.Log(spot / strike) +
                    ((riskFreeRate - dividendYield + (0.5 * volatility * volatility)) * timeToExpiry)) /
                   (volatility * Math.Sqrt(timeToExpiry));
        double d2 = d1 - (volatility * Math.Sqrt(timeToExpiry));

        double forward = spot * Math.Exp(-dividendYield * timeToExpiry);
        double discountFactor = Math.Exp(-riskFreeRate * timeToExpiry);

        if (isCall)
        {
            return (forward * NormalCDF(d1)) - (strike * discountFactor * NormalCDF(d2));
        }
        else
        {
            return (strike * discountFactor * NormalCDF(-d2)) - (forward * NormalCDF(-d1));
        }
    }

    /// <summary>
    /// Black-Scholes vega for Newton-Raphson.
    /// </summary>
    private static double BlackScholesVega(
        double spot,
        double strike,
        double timeToExpiry,
        double riskFreeRate,
        double dividendYield,
        double volatility)
    {
        double d1 = (Math.Log(spot / strike) +
                    ((riskFreeRate - dividendYield + (0.5 * volatility * volatility)) * timeToExpiry)) /
                   (volatility * Math.Sqrt(timeToExpiry));

        double forward = spot * Math.Exp(-dividendYield * timeToExpiry);
        return forward * Math.Sqrt(timeToExpiry) * NormalPDF(d1);
    }

    /// <summary>
    /// Bisection method for IV solving (fallback).
    /// </summary>
    private static double BisectionIVSolver(
        double spot,
        double strike,
        double timeToExpiry,
        KouParameters @params,
        double targetPrice,
        bool isCall)
    {
        double ivLow = MinIV;
        double ivHigh = MaxIV;

        for (int iter = 0; iter < MaxNewtonIterations; iter++)
        {
            double ivMid = (ivLow + ivHigh) / 2;

            double price = BlackScholesPrice(spot, strike, timeToExpiry,
                @params.RiskFreeRate, @params.DividendYield, ivMid, isCall);

            if (Math.Abs(price - targetPrice) < IVTolerance)
            {
                return ivMid;
            }

            if (price > targetPrice)
            {
                ivHigh = ivMid;
            }
            else
            {
                ivLow = ivMid;
            }
        }

        return (ivLow + ivHigh) / 2;
    }

    private static double NormalCDF(double x)
    {
        return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
    }

    private static double NormalPDF(double x)
    {
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
    }

    private static double Erf(double x)
    {
        // Abramowitz and Stegun approximation
        double a1 = 0.254829592;
        double a2 = -0.284496736;
        double a3 = 1.421413741;
        double a4 = -1.453152027;
        double a5 = 1.061405429;
        double p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        double t = 1.0 / (1.0 + (p * x));

        double polynomial = (((((((a5 * t) + a4) * t) + a3) * t) + a2) * t) + a1;
        double y = 1.0 - (polynomial * t * Math.Exp(-(x * x)));

        return sign * y;
    }
}
