using System.Numerics;
using Alaris.Strategy.Core.Numerical;

namespace Alaris.Strategy.Core;

/// <summary>
/// Production-grade Heston model pricing using characteristic function integration.
/// Implements the semi-analytical pricing formula from Heston (1993).
///
/// Uses Carr-Madan Fourier inversion or Lewis (2001) approach for option pricing,
/// then backs out implied volatility using Newton-Raphson iteration.
///
/// References:
/// Heston (1993) "A Closed-Form Solution for Options with Stochastic Volatility"
/// Carr and Madan (1999) "Option Valuation Using the Fast Fourier Transform"
/// Lewis (2001) "A Simple Option Formula for General Jump-Diffusion and Other
/// Exponential Levy Processes"
/// </summary>
public static class HestonPricing
{
    private const double MinIV = 0.001;
    private const double MaxIV = 5.0;
    private const int MaxNewtonIterations = 50;
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
        // Call price = S*P1 - K*exp(-rT)*P2
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
    /// Computes implied volatility from Heston model using Newton-Raphson iteration.
    /// This is the production implementation replacing moment-matching approximation.
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

        // Use Newton-Raphson to find IV that matches this price
        double iv = Math.Sqrt(@params.V0); // Initial guess from current variance

        for (int iter = 0; iter < MaxNewtonIterations; iter++)
        {
            // Compute Black-Scholes price and vega at current IV
            double bsPrice = BlackScholesPrice(spot, strike, timeToExpiry,
                @params.RiskFreeRate, @params.DividendYield, iv, isCall);
            double vega = BlackScholesVega(spot, strike, timeToExpiry,
                @params.RiskFreeRate, @params.DividendYield, iv);

            double priceDiff = bsPrice - hestonPrice;

            // Check convergence
            if (Math.Abs(priceDiff) < IVTolerance)
            {
                return Math.Clamp(iv, MinIV, MaxIV);
            }

            // Newton step: iv_new = iv - f(iv) / f'(iv)
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

        // If Newton-Raphson didn't converge, fall back to bisection
        return BisectionIVSolver(spot, strike, timeToExpiry, @params, hestonPrice, isCall);
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
            if (phi < 1e-10)
            {
                return 0;
            }

            Complex iPhi = new Complex(0, phi);
            Complex charFunc = CharacteristicFunction(phi, spot, timeToExpiry, @params, j);
            Complex exponent = Complex.Exp(iPhi * logMoneyness);

            return (exponent * charFunc / iPhi).Real;
        }

        // Numerical integration from 0 to infinity
        (double integralValue, double _) = AdaptiveIntegration.IntegrateToInfinity(
            Integrand,
            0,
            absoluteTolerance: 1e-6,
            relativeTolerance: 1e-4);

        double probability = 0.5 + (integralValue / Math.PI);

        return Math.Clamp(probability, 0, 1);
    }

    /// <summary>
    /// Heston characteristic function for probability P_j.
    /// This is the core of the semi-analytical pricing formula.
    /// </summary>
    private static Complex CharacteristicFunction(
        double phi,
        double spot,
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
        double d = @params.DividendYield;

        // Parameters u_j and b_j depend on which probability we're computing
        double u = j == 1 ? 0.5 : -0.5;
        double b = j == 1 ? kappa - (rho * sigmaV) : kappa;

        // Complex components
        // Standard Heston: d = sqrt((ρσiφ - b)² - σ²(2*u*iφ - φ²))
        // For j=1 (u=0.5): φ² - 2*0.5*iφ = φ² - iφ
        // For j=2 (u=-0.5): φ² - 2*(-0.5)*iφ = φ² + iφ
        Complex d_h = Complex.Sqrt(
            (((rho * sigmaV * i * phi) - b) * ((rho * sigmaV * i * phi) - b)) +
            (sigmaV * sigmaV * ((phi * phi) - (2 * u * i * phi))));

        Complex g = (b - (rho * sigmaV * i * phi) - d_h) /
                    (b - (rho * sigmaV * i * phi) + d_h);

        Complex exp_dt = Complex.Exp(-d_h * timeToExpiry);

        Complex C = ((r - d) * i * phi * timeToExpiry) +
                    (kappa * theta / (sigmaV * sigmaV) *
                     (((b - (rho * sigmaV * i * phi) - d_h) * timeToExpiry) -
                      (2 * Complex.Log((1 - (g * exp_dt)) / (1 - g)))));

        // D term
        Complex D = ((b - (rho * sigmaV * i * phi) - d_h) / (sigmaV * sigmaV) *
                     ((1 - exp_dt) / (1 - (g * exp_dt))));

        // Characteristic function of log-return ln(S_T/S_0), not ln(S_T)
        Complex charFunc = Complex.Exp(C + (D * v0));

        return charFunc;
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
    /// Bisection method for IV solving (fallback if Newton-Raphson fails).
    /// </summary>
    private static double BisectionIVSolver(
        double spot,
        double strike,
        double timeToExpiry,
        HestonParameters @params,
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

    /// <summary>
    /// Cumulative distribution function for standard normal.
    /// </summary>
    private static double NormalCDF(double x)
    {
        return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
    }

    /// <summary>
    /// Probability density function for standard normal.
    /// </summary>
    private static double NormalPDF(double x)
    {
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
    }

    /// <summary>
    /// Error function approximation.
    /// </summary>
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