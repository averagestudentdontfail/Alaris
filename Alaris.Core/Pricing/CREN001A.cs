// CREN001A.cs - Crank-Nicolson Finite Difference American Option Engine
// Component ID: CREN001A
//
// Replaces: QuantLib.FdBlackScholesVanillaEngine
//
// Mathematical Specification:
// - Black-Scholes PDE: ∂V/∂t + (r-q-σ²/2)∂V/∂x + ½σ²∂²V/∂x² - rV = 0
// - Crank-Nicolson: θ = 0.5 (implicit-explicit average)
// - Early exercise: V = max(V, intrinsic) at each time step
// - Thomas algorithm for tridiagonal system O(N) complexity
//
// Grid Construction:
// - Log-spot grid: x ∈ [ln(S/K) - 5σ√T, ln(S/K) + 5σ√T]
// - Uniform time steps from T to 0 (backward induction)
//
// References:
// - Wilmott, P. "Paul Wilmott on Quantitative Finance" Chapter 77-79
// - Hull, J.C. "Options, Futures, and Other Derivatives" Chapter 21
// - QuantLib fdblackscholesvanillaengine.cpp
// - Alaris.Governance/Coding.md Rule 3 (Bounded Loops), Rule 5 (Zero-allocation)

using Alaris.Core.Options;
using System.Buffers;

namespace Alaris.Core.Pricing;

/// <summary>
/// Crank-Nicolson finite difference engine for American options.
/// Provides numerical pricing matching QuantLib FdBlackScholesVanillaEngine accuracy.
/// </summary>
/// <remarks>
/// This engine uses:
/// - Log-spot transformation for uniform grid spacing
/// - Crank-Nicolson scheme (θ=0.5) for stability and second-order accuracy
/// - Thomas algorithm for O(N) tridiagonal system solution
/// - Early exercise enforcement at each time step
/// </remarks>
public sealed class CREN001A
{
    private readonly int _timeSteps;
    private readonly int _spotSteps;
    private readonly double _theta;

    /// <summary>
    /// Default number of time steps for standard accuracy.
    /// </summary>
    public const int DefaultTimeSteps = 100;

    /// <summary>
    /// Default number of spot grid points.
    /// </summary>
    public const int DefaultSpotSteps = 200;

    /// <summary>
    /// Initialises the finite difference engine.
    /// </summary>
    /// <param name="timeSteps">Number of time steps (default: 100).</param>
    /// <param name="spotSteps">Number of spot grid points (default: 200).</param>
    /// <param name="theta">Theta for time stepping (0.5 = Crank-Nicolson, 1.0 = Implicit, 0.0 = Explicit).</param>
    public CREN001A(int timeSteps = DefaultTimeSteps, int spotSteps = DefaultSpotSteps, double theta = 0.5)
    {
        if (timeSteps <= 0)
        {
            throw new ArgumentException("Time steps must be positive", nameof(timeSteps));
        }
        if (spotSteps <= 0)
        {
            throw new ArgumentException("Spot steps must be positive", nameof(spotSteps));
        }
        if (theta < 0.0 || theta > 1.0)
        {
            throw new ArgumentException("Theta must be in [0, 1]", nameof(theta));
        }

        _timeSteps = timeSteps;
        _spotSteps = spotSteps;
        _theta = theta;
    }

    /// <summary>
    /// Prices an American option using finite differences.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <param name="riskFreeRate">Risk-free rate (continuously compounded).</param>
    /// <param name="dividendYield">Continuous dividend yield.</param>
    /// <param name="volatility">Volatility (annualised).</param>
    /// <param name="optionType">Call or Put.</param>
    /// <returns>American option price.</returns>
    public double Price(
        double spot,
        double strike,
        double timeToExpiry,
        double riskFreeRate,
        double dividendYield,
        double volatility,
        OptionType optionType)
    {
        // Guard clauses (Rule 10: specific exceptions)
        if (spot <= 0)
        {
            throw new ArgumentException("Spot must be positive", nameof(spot));
        }
        if (strike <= 0)
        {
            throw new ArgumentException("Strike must be positive", nameof(strike));
        }
        if (volatility <= 0)
        {
            throw new ArgumentException("Volatility must be positive", nameof(volatility));
        }

        // Handle expired options
        if (timeToExpiry <= 0)
        {
            return CalculatePayoff(spot, strike, optionType);
        }

        // Grid parameters
        double sigma = volatility;
        double sigma2 = sigma * sigma;
        double dt = timeToExpiry / _timeSteps;
        double r = riskFreeRate;
        double q = dividendYield;
        
        // Log-spot grid: x = ln(S)
        // Grid centered at ln(spot) with range ±5σ√T
        double gridWidth = 5.0 * sigma * System.Math.Sqrt(timeToExpiry);
        double xMin = System.Math.Log(spot) - gridWidth;
        double xMax = System.Math.Log(spot) + gridWidth;
        double dx = (xMax - xMin) / (_spotSteps - 1);

        int n = _spotSteps;
        
        // Rent arrays from pool for zero-allocation (Rule 5)
        double[] x = ArrayPool<double>.Shared.Rent(n);
        double[] v = ArrayPool<double>.Shared.Rent(n);
        double[] vNew = ArrayPool<double>.Shared.Rent(n);
        double[] a = ArrayPool<double>.Shared.Rent(n);
        double[] b = ArrayPool<double>.Shared.Rent(n);
        double[] c = ArrayPool<double>.Shared.Rent(n);
        double[] d = ArrayPool<double>.Shared.Rent(n);

        try
        {
            // Initialize log-spot grid
            for (int i = 0; i < n; i++)
            {
                x[i] = xMin + (i * dx);
            }

            // Calculate FD coefficients
            // PDE: ∂V/∂t = -[(r-q-σ²/2)∂V/∂x + ½σ²∂²V/∂x² - rV]
            // After discretization with centered differences:
            double alpha = 0.5 * dt * ((sigma2 / (dx * dx)) - ((r - q - (0.5 * sigma2)) / dx));
            double beta = -dt * ((sigma2 / (dx * dx)) + r);
            double gamma = 0.5 * dt * ((sigma2 / (dx * dx)) + ((r - q - (0.5 * sigma2)) / dx));

            // Terminal condition: V(T, S) = payoff(S)
            for (int i = 0; i < n; i++)
            {
                double spotAtNode = System.Math.Exp(x[i]);
                v[i] = CalculatePayoff(spotAtNode, strike, optionType);
            }

            // Backward induction through time (Rule 3: bounded loop)
            for (int step = 0; step < _timeSteps; step++)
            {
                // Build tridiagonal system for Crank-Nicolson
                // (I - θ·dt·A)·V^(n) = (I + (1-θ)·dt·A)·V^(n+1)
                BuildTridiagonalSystem(n, alpha, beta, gamma, v, a, b, c, d);

                // Solve tridiagonal system using Thomas algorithm
                SolveTridiagonal(n, a, b, c, d, vNew);

                // Apply boundary conditions
                ApplyBoundaryConditions(n, x, vNew, strike, r, dt, optionType);

                // Early exercise check for American options
                for (int i = 0; i < n; i++)
                {
                    double spotAtNode = System.Math.Exp(x[i]);
                    double intrinsic = CalculatePayoff(spotAtNode, strike, optionType);
                    vNew[i] = System.Math.Max(vNew[i], intrinsic);
                }

                // Swap arrays for next iteration
                (v, vNew) = (vNew, v);
            }

            // Interpolate to get price at current spot
            double logSpot = System.Math.Log(spot);
            return InterpolatePrice(x, v, n, logSpot);
        }
        finally
        {
            // Return arrays to pool
            ArrayPool<double>.Shared.Return(x);
            ArrayPool<double>.Shared.Return(v);
            ArrayPool<double>.Shared.Return(vNew);
            ArrayPool<double>.Shared.Return(a);
            ArrayPool<double>.Shared.Return(b);
            ArrayPool<double>.Shared.Return(c);
            ArrayPool<double>.Shared.Return(d);
        }
    }

    /// <summary>
    /// Builds the tridiagonal system for Crank-Nicolson.
    /// </summary>
    private void BuildTridiagonalSystem(
        int n,
        double alpha,
        double beta,
        double gamma,
        double[] v,
        double[] aOut,
        double[] bOut,
        double[] cOut,
        double[] dOut)
    {
        double oneMinusTheta = 1.0 - _theta;

        for (int i = 1; i < n - 1; i++)
        {
            // Left hand side: (I - θ·dt·A)
            aOut[i] = -_theta * alpha;
            bOut[i] = 1.0 - (_theta * beta);
            cOut[i] = -_theta * gamma;

            // Right hand side: (I + (1-θ)·dt·A)·V
            dOut[i] = (oneMinusTheta * alpha * v[i - 1])
                    + ((1.0 + (oneMinusTheta * beta)) * v[i])
                    + (oneMinusTheta * gamma * v[i + 1]);
        }

        // Boundary conditions will be set separately
        aOut[0] = 0;
        bOut[0] = 1;
        cOut[0] = 0;
        dOut[0] = v[0];

        aOut[n - 1] = 0;
        bOut[n - 1] = 1;
        cOut[n - 1] = 0;
        dOut[n - 1] = v[n - 1];
    }

    /// <summary>
    /// Solves a tridiagonal system using the Thomas algorithm.
    /// O(N) complexity, numerically stable for diagonally dominant matrices.
    /// </summary>
    private static void SolveTridiagonal(
        int n,
        double[] a,  // subdiagonal
        double[] b,  // diagonal
        double[] c,  // superdiagonal
        double[] d,  // right-hand side
        double[] x)  // solution
    {
        // Forward sweep
        double[] cPrime = ArrayPool<double>.Shared.Rent(n);
        double[] dPrime = ArrayPool<double>.Shared.Rent(n);

        try
        {
            cPrime[0] = c[0] / b[0];
            dPrime[0] = d[0] / b[0];

            for (int i = 1; i < n; i++)
            {
                double denom = b[i] - (a[i] * cPrime[i - 1]);
                if (System.Math.Abs(denom) < 1e-15)
                {
                    denom = 1e-15; // Prevent division by zero
                }
                cPrime[i] = c[i] / denom;
                dPrime[i] = (d[i] - (a[i] * dPrime[i - 1])) / denom;
            }

            // Back substitution
            x[n - 1] = dPrime[n - 1];
            for (int i = n - 2; i >= 0; i--)
            {
                x[i] = dPrime[i] - (cPrime[i] * x[i + 1]);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(cPrime);
            ArrayPool<double>.Shared.Return(dPrime);
        }
    }

    /// <summary>
    /// Applies Dirichlet boundary conditions.
    /// </summary>
    private static void ApplyBoundaryConditions(
        int n,
        double[] x,
        double[] v,
        double strike,
        double r,
        double dt,
        OptionType optionType)
    {
        // For puts: V(0) → K (as S → 0, put is worth K)
        // For calls: V(∞) → 0 (as S → ∞, we use extrapolation)

        if (optionType == OptionType.Put)
        {
            // At S = 0, put is worth K·e^(-rT)
            v[0] = strike * System.Math.Exp(-r * dt);
        }
        else
        {
            // At S = ∞, call value grows linearly (Neumann condition)
            // Use linear extrapolation from interior
            v[n - 1] = (2 * v[n - 2]) - v[n - 3];
            if (v[n - 1] < 0)
            {
                v[n - 1] = 0;
            }
        }
    }

    /// <summary>
    /// Interpolates the price at the current spot.
    /// </summary>
    private static double InterpolatePrice(double[] x, double[] v, int n, double logSpot)
    {
        // Find bracketing indices
        int i = 0;
        for (int j = 0; j < n - 1; j++)
        {
            if (x[j] <= logSpot && logSpot <= x[j + 1])
            {
                i = j;
                break;
            }
        }

        // Linear interpolation
        double dx = x[i + 1] - x[i];
        if (System.Math.Abs(dx) < 1e-15)
        {
            return v[i];
        }
        double t = (logSpot - x[i]) / dx;
        return ((1 - t) * v[i]) + (t * v[i + 1]);
    }

    /// <summary>
    /// Calculates the option payoff.
    /// </summary>
    private static double CalculatePayoff(double spot, double strike, OptionType optionType)
    {
        return optionType switch
        {
            OptionType.Call => System.Math.Max(spot - strike, 0.0),
            OptionType.Put => System.Math.Max(strike - spot, 0.0),
            _ => 0.0
        };
    }

    /// <summary>
    /// Calculates delta using central difference.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <param name="riskFreeRate">Risk-free rate.</param>
    /// <param name="dividendYield">Dividend yield.</param>
    /// <param name="volatility">Volatility.</param>
    /// <param name="optionType">Call or Put.</param>
    /// <returns>Delta (∂V/∂S).</returns>
    public double Delta(
        double spot,
        double strike,
        double timeToExpiry,
        double riskFreeRate,
        double dividendYield,
        double volatility,
        OptionType optionType)
    {
        const double epsilon = 0.01;
        double up = Price(spot * (1 + epsilon), strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double down = Price(spot * (1 - epsilon), strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        return (up - down) / (2 * spot * epsilon);
    }

    /// <summary>
    /// Calculates gamma using central difference.
    /// </summary>
    public double Gamma(
        double spot,
        double strike,
        double timeToExpiry,
        double riskFreeRate,
        double dividendYield,
        double volatility,
        OptionType optionType)
    {
        const double epsilon = 0.01;
        double up = Price(spot * (1 + epsilon), strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double mid = Price(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double down = Price(spot * (1 - epsilon), strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double h = spot * epsilon;
        return (up - (2 * mid) + down) / (h * h);
    }

    /// <summary>
    /// Calculates vega using central difference.
    /// </summary>
    public double Vega(
        double spot,
        double strike,
        double timeToExpiry,
        double riskFreeRate,
        double dividendYield,
        double volatility,
        OptionType optionType)
    {
        const double epsilon = 0.001;
        double up = Price(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility + epsilon, optionType);
        double down = Price(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility - epsilon, optionType);
        return (up - down) / (2 * epsilon);
    }

    /// <summary>
    /// Calculates theta using forward difference.
    /// </summary>
    public double Theta(
        double spot,
        double strike,
        double timeToExpiry,
        double riskFreeRate,
        double dividendYield,
        double volatility,
        OptionType optionType)
    {
        const double dtDays = 1.0 / 365.0;
        double now = Price(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double later = Price(spot, strike, timeToExpiry - dtDays, riskFreeRate, dividendYield, volatility, optionType);
        return (later - now) / dtDays;
    }

    /// <summary>
    /// Calculates rho using central difference.
    /// </summary>
    public double Rho(
        double spot,
        double strike,
        double timeToExpiry,
        double riskFreeRate,
        double dividendYield,
        double volatility,
        OptionType optionType)
    {
        const double epsilon = 0.0001;
        double up = Price(spot, strike, timeToExpiry, riskFreeRate + epsilon, dividendYield, volatility, optionType);
        double down = Price(spot, strike, timeToExpiry, riskFreeRate - epsilon, dividendYield, volatility, optionType);
        return (up - down) / (2 * epsilon);
    }
}
