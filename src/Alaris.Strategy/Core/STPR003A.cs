// STPR003A.cs - production-grade Heston model pricing using characteristic function integrati...

using System.Numerics;
using Alaris.Strategy.Core.Numerical;

namespace Alaris.Strategy.Core;

/// <summary>
/// Production-grade Heston model pricing using characteristic function integration.
/// Implements the semi-analytical pricing formula from Heston (1993).
/// Uses Carr-Madan Fourier inversion or Lewis (2001) approach for option pricing,
/// then backs out implied volatility using Newton-Raphson iteration.
/// </summary>
public static class STPR003A
{
    private const double MinIV = 0.001;
    private const double MaxIV = 5.0;
    private const double IVTolerance = 1e-6;

    /// <summary>
    /// Computes call option price using Heston semi-analytical formula.
    /// Uses the characteristic function approach with numerical integration.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <param name="params">Heston model parameters.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <returns>Option price.</returns>
    public static double ComputePrice(
        double spot,
        double strike,
        double timeToExpiry,
        HestonParameters @params,
        bool isCall = true)
    {
        ArgumentNullException.ThrowIfNull(@params);

        if (spot <= 0 || strike <= 0 || timeToExpiry <= 0)
        {
            throw new ArgumentException("Spot, strike, and time to expiry must be positive.");
        }

        // Compute using Heston semi-analytical formula
        // Call price = S*exp(-qT)*P1 - K*exp(-rT)*P2
        // where P1 and P2 are probabilities computed via characteristic function

        double discountFactor = Math.Exp(-@params.RiskFreeRate * timeToExpiry);
        double forwardFactor = Math.Exp(-@params.DividendYield * timeToExpiry);

        double p1 = ComputeProbability(spot, strike, timeToExpiry, @params, 1);
        double p2 = ComputeProbability(spot, strike, timeToExpiry, @params, 2);

        double callPrice = (spot * forwardFactor * p1) - (strike * discountFactor * p2);

        if (isCall)
        {
            return Math.Max(callPrice, 0);
        }
        else
        {
            // Put-call parity: P = C - S*exp(-dT) + K*exp(-rT)
            double putPrice = callPrice - (spot * forwardFactor) + (strike * discountFactor);
            return Math.Max(putPrice, 0);
        }
    }

    /// <summary>
    /// Computes implied volatility from Heston model using Brent's method.
    /// Replaces Newton-Bisection cascade with unified Brent algorithm.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <param name="params">Heston model parameters.</param>
    /// <returns>Implied volatility.</returns>
    public static double ComputeImpliedVolatility(
        double spot,
        double strike,
        double timeToExpiry,
        HestonParameters @params)
    {
        // Get theoretical Heston price
        bool isCall = strike >= spot;
        double hestonPrice = ComputePrice(spot, strike, timeToExpiry, @params, isCall);

        // Use Brent's method - no fallback cascade needed
        return STPR007A.SolveImpliedVolatility(
            iv => BlackScholesPrice(spot, strike, timeToExpiry,
                @params.RiskFreeRate, @params.DividendYield, iv, isCall),
            hestonPrice,
            MinIV,
            MaxIV,
            IVTolerance);
    }

    /// <summary>
    /// Computes P_j probability using characteristic function integration.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <param name="params">Heston model parameters.</param>
    /// <param name="j">Probability index (1 or 2).</param>
    private static double ComputeProbability(
        double spot,
        double strike,
        double timeToExpiry,
        HestonParameters @params,
        int j)
    {
        double logMoneyness = Math.Log(spot / strike);

        // Integrate characteristic function
        // P_j = 0.5 + (1/pi) * integral from 0 to inf of Re[(exp(i*phi*log(S/K)) * f_j(phi)) / (i*phi)]
        // where f_j is the characteristic function of the log-return

        double Integrand(double phi)
        {
            // UPDATED: Removed the "if (phi < 1e-10) return 0;" check to ensure smoothness
            // for high-order quadrature. The integration lower bound handles the singularity.

            Complex iPhi = new Complex(0, phi);
            Complex charFunc = CharacteristicFunction(phi, timeToExpiry, @params, j);
            Complex exponent = Complex.Exp(iPhi * logMoneyness);

            return (exponent * charFunc / iPhi).Real;
        }

        // Numerical integration from slightly above 0 to infinity using adaptive quadrature
        // Uses STPR002C unified facade for automatic AVX2 dispatch
        (double integralValue, double _) = STPR002C.IntegrateToInfinity(
            Integrand,
            1e-8,
            absoluteTolerance: 1e-8,
            relativeTolerance: 1e-6);

        double probability = 0.5 + (integralValue / Math.PI);

        return Math.Clamp(probability, 0, 1);
    }

    /// <summary>
    /// Standard Heston characteristic function for probability P_j.
    /// This is the core of the semi-analytical pricing formula.
    /// Based on Heston (1993) equations (17) and (18).
    /// </summary>
    private static Complex CharacteristicFunction(
        double phi,
        double timeToExpiry,
        HestonParameters @params,
        int j)
    {
        Complex i = Complex.ImaginaryOne;

        double kappa = @params.Kappa;
        double theta = @params.Theta;
        double sigmaV = @params.SigmaV;
        double rho = @params.Rho;
        double v0 = @params.V0;
        double r = @params.RiskFreeRate;
        double q = @params.DividendYield;
        double sigmaSq = sigmaV * sigmaV;

        // Parameters u_j and b_j depend on which probability we're computing
        // For P1 (stock measure): u = 0.5, b = kappa - rho*sigmaV
        // For P2 (risk-neutral): u = -0.5, b = kappa
        double u = j == 1 ? 0.5 : -0.5;
        double b = j == 1 ? kappa - (rho * sigmaV) : kappa;

        // a = b - rho*sigma*i*phi
        Complex a = b - (rho * sigmaV * i * phi);

        // d² = a² + sigma²*(phi² - 2*u*i*phi)
        Complex dSquared = (a * a) + (sigmaSq * ((phi * phi) - (2 * u * i * phi)));
        Complex d = Complex.Sqrt(dSquared);

        // Branch cut handling: ensure Re(d) > 0 for numerical stability
        if (d.Real < 0)
        {
            d = -d;
        }

        // g = (a - d) / (a + d)
        Complex g = (a - d) / (a + d);

        Complex expMinusDT = Complex.Exp(-d * timeToExpiry);

        // C term: drift contribution + variance mean-reversion contribution
        // C = (r-q)*i*phi*T + (kappa*theta/sigma²) * [(a-d)*T - 2*ln((1-g*exp(-dT))/(1-g))]
        Complex logTerm = Complex.Log((1 - (g * expMinusDT)) / (1 - g));
        Complex C = ((r - q) * i * phi * timeToExpiry) +
                    (kappa * theta / sigmaSq *
                     (((a - d) * timeToExpiry) - (2 * logTerm)));

        // D term: initial variance contribution
        // D = (a-d)/sigma² * (1 - exp(-dT)) / (1 - g*exp(-dT))
        Complex D = (a - d) / sigmaSq *
                    ((1 - expMinusDT) / (1 - (g * expMinusDT)));

        // Characteristic function of log-return ln(S_T/S_0)
        return Complex.Exp(C + (D * v0));
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
        double sqrtT = Math.Sqrt(timeToExpiry);
        double d1 = (Math.Log(spot / strike) +
                    ((riskFreeRate - dividendYield + (0.5 * volatility * volatility)) * timeToExpiry)) /
                   (volatility * sqrtT);
        double d2 = d1 - (volatility * sqrtT);

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
        double sqrtT = Math.Sqrt(timeToExpiry);
        double d1 = (Math.Log(spot / strike) +
                    ((riskFreeRate - dividendYield + (0.5 * volatility * volatility)) * timeToExpiry)) /
                   (volatility * sqrtT);

        double forward = spot * Math.Exp(-dividendYield * timeToExpiry);
        return forward * sqrtT * NormalPDF(d1);
    }

    // BisectionIVSolver removed - replaced by STPR007A.SolveImpliedVolatility (Brent's method)

    /// <summary>
    /// Cumulative distribution function for standard normal.
    /// Delegates to unified CRMF001A implementation.
    /// </summary>
    private static double NormalCDF(double x) => Alaris.Core.Math.CRMF001A.NormalCDF(x);

    /// <summary>
    /// Probability density function for standard normal.
    /// Delegates to unified CRMF001A implementation.
    /// </summary>
    private static double NormalPDF(double x) => Alaris.Core.Math.CRMF001A.NormalPDF(x);
}