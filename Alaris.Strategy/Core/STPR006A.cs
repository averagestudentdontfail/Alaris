// STPR006A.cs - production-grade Kou model pricing using characteristic function integration....

using System.Numerics;
using Alaris.Strategy.Core.Numerical;

namespace Alaris.Strategy.Core;

/// <summary>
/// Production-grade Kou model pricing using characteristic function integration.
/// Implements semi-analytical pricing for double-exponential jump-diffusion model.
/// The Kou (2002) model extends Black-Scholes with asymmetric jumps:
/// - Uses Fourier inversion for option pricing
/// - Handles fat tails and skewness
/// - Calibrates to market implied volatility smile
/// </summary>
public static class STPR006A
{
    private const double MinIV = 0.001;
    private const double MaxIV = 5.0;
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

        double Integrand(double v)
        {
            if (v < 1e-10)
            {
                return 0;
            }

            Complex iV = new Complex(0, v);
            Complex charFunc = CharacteristicFunction(v - ((alpha + 1) * Complex.ImaginaryOne), spot, timeToExpiry, @params);

            // Carr-Madan denominator: α² + α - v² + i*v*(2α + 1)
            Complex denominator = (alpha * alpha) + alpha - (v * v) + (iV * ((2 * alpha) + 1));

            return (Complex.Exp(-iV * logStrike) * charFunc / denominator).Real;
        }

        // Numerical integration
        (double integralValue, double _) = STPR002A.IntegrateToInfinity(
            Integrand,
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
    /// Computes implied volatility from Kou model using Brent's method.
    /// Replaces Newton-Bisection cascade with unified Brent algorithm.
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

        // Use Brent's method - no fallback cascade needed
        return STPR007A.SolveImpliedVolatility(
            iv => BlackScholesPrice(spot, strike, timeToExpiry,
                @params.RiskFreeRate, @params.DividendYield, iv, isCall),
            kouPrice,
            MinIV,
            MaxIV,
            IVTolerance);
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
    /// Black-Scholes vega for gradient-based methods (retained for reference).
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

    // BisectionIVSolver removed - replaced by STPR007A.SolveImpliedVolatility (Brent's method)

    // Use centralised CRMF001A for math utilities
    private static double NormalCDF(double x) => Alaris.Core.Math.CRMF001A.NormalCDF(x);
    private static double NormalPDF(double x) => Alaris.Core.Math.CRMF001A.NormalPDF(x);
}
