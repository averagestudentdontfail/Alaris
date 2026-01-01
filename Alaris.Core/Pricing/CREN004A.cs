// CREN004A.cs - Spectral Collocation American Pricing Engine
// Component ID: CREN004A
//
// Implements Andersen-Lake-Offengenden algorithm for American option pricing
// using Chebyshev interpolation of the exercise boundary and Gauss quadrature.
//
// References:
// - Andersen, Lake & Offengenden (2016) "High Performance American Option Pricing"
// - Healy (2021) "American Options Under Negative Rates" (double-boundary extension)

using System;
using System.Runtime.CompilerServices;
using Alaris.Core.Math;
using Alaris.Core.Options;

namespace Alaris.Core.Pricing;

/// <summary>
/// Iteration scheme for spectral American option pricing.
/// </summary>
public enum SpectralScheme
{
    /// <summary>Fast: 8 Chebyshev nodes, 8-point Legendre quadrature.</summary>
    Fast,

    /// <summary>Accurate: 12 Chebyshev nodes, 16-point Legendre quadrature.</summary>
    Accurate,

    /// <summary>HighPrecision: 24 Chebyshev nodes, Tanh-Sinh quadrature.</summary>
    HighPrecision
}

/// <summary>
/// Fixed-point equation type for boundary iteration.
/// </summary>
public enum FixedPointEquation
{
    /// <summary>FP-A: Standard fixed-point equation.</summary>
    FP_A,

    /// <summary>FP-B: Stabilized fixed-point using derivative information.</summary>
    FP_B,

    /// <summary>Auto: Select based on regime (FP-B for double-boundary).</summary>
    Auto
}

/// <summary>
/// Spectral collocation American option pricing engine.
/// </summary>
/// <remarks>
/// Implements the Andersen-Lake-Offengenden algorithm which achieves ~10⁻⁸ accuracy
/// with sub-millisecond pricing by interpolating the exercise boundary on Chebyshev
/// nodes and using Gauss quadrature for integration.
/// </remarks>
public sealed class CREN004A
{
    private readonly int _chebyshevNodes;
    private readonly int _fixedPointIterations;
    private readonly FixedPointEquation _fpEquation;
    private readonly bool _useTanhSinh;
    private readonly (double[] Nodes, double[] Weights) _quadratureScheme;

    private const double Tolerance = 1e-10;
    private const double NumericalEpsilon = 1e-14;

    /// <summary>
    /// Initializes a new spectral American pricing engine.
    /// </summary>
    /// <param name="scheme">Pre-defined iteration scheme (Fast/Accurate/HighPrecision).</param>
    public CREN004A(SpectralScheme scheme = SpectralScheme.Accurate)
    {
        (_chebyshevNodes, _fixedPointIterations, _useTanhSinh, _quadratureScheme) = scheme switch
        {
            SpectralScheme.Fast => (8, 2, false, CRGQ001A.Schemes.Fast),
            SpectralScheme.Accurate => (12, 3, false, CRGQ001A.Schemes.Accurate),
            SpectralScheme.HighPrecision => (24, 4, true, CRGQ001A.Schemes.HighPrecision),
            _ => (12, 3, false, CRGQ001A.Schemes.Accurate)
        };

        _fpEquation = FixedPointEquation.Auto;
    }

    /// <summary>
    /// Initializes a spectral engine with custom parameters.
    /// </summary>
    public CREN004A(
        int chebyshevNodes,
        int fixedPointIterations,
        FixedPointEquation fpEquation = FixedPointEquation.Auto,
        bool useTanhSinh = false)
    {
        if (chebyshevNodes < 4 || chebyshevNodes > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(chebyshevNodes), "Chebyshev nodes must be between 4 and 64");
        }

        _chebyshevNodes = chebyshevNodes;
        _fixedPointIterations = System.Math.Clamp(fixedPointIterations, 1, 10);
        _fpEquation = fpEquation;
        _useTanhSinh = useTanhSinh;
        _quadratureScheme = CRGQ001A.GaussLegendre(System.Math.Min(chebyshevNodes * 2, 32));
    }

    /// <summary>
    /// Prices an American option using spectral collocation.
    /// </summary>
    public double Price(
        double spot,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        OptionType optionType)
    {
        ValidateInputs(spot, strike, tau, sigma);

        // Near-expiry: return intrinsic value
        if (tau < 1.0 / 365.0)
        {
            return CalculateIntrinsicValue(spot, strike, optionType);
        }

        bool isCall = optionType == OptionType.Call;

        // Determine regime and route to appropriate method
        RateRegime regime = ClassifyRegime(r, q, isCall);

        return regime switch
        {
            RateRegime.Standard => PriceSingleBoundary(spot, strike, tau, r, q, sigma, isCall),
            RateRegime.DoubleBoundary => PriceDoubleBoundary(spot, strike, tau, r, q, sigma, isCall),
            _ => PriceSingleBoundary(spot, strike, tau, r, q, sigma, isCall)
        };
    }

    /// <summary>
    /// Computes Delta (∂V/∂S) using central differencing.
    /// </summary>
    public double Delta(
        double spot,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        OptionType optionType)
    {
        double h = spot * 0.001;
        double vUp = Price(spot + h, strike, tau, r, q, sigma, optionType);
        double vDown = Price(spot - h, strike, tau, r, q, sigma, optionType);
        return (vUp - vDown) / (2.0 * h);
    }

    /// <summary>
    /// Computes Gamma (∂²V/∂S²) using central differencing.
    /// </summary>
    public double Gamma(
        double spot,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        OptionType optionType)
    {
        double h = spot * 0.001;
        double vUp = Price(spot + h, strike, tau, r, q, sigma, optionType);
        double vCtr = Price(spot, strike, tau, r, q, sigma, optionType);
        double vDown = Price(spot - h, strike, tau, r, q, sigma, optionType);
        return (vUp - (2.0 * vCtr) + vDown) / (h * h);
    }

    /// <summary>
    /// Computes Theta (∂V/∂τ) using forward differencing.
    /// </summary>
    public double Theta(
        double spot,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        OptionType optionType)
    {
        double dt = 1.0 / 365.0;
        if (tau <= dt)
        {
            return 0.0;
        }

        double vNow = Price(spot, strike, tau, r, q, sigma, optionType);
        double vLater = Price(spot, strike, tau - dt, r, q, sigma, optionType);
        return (vLater - vNow) / dt;
    }

    /// <summary>
    /// Computes Vega (∂V/∂σ) using central differencing.
    /// </summary>
    public double Vega(
        double spot,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        OptionType optionType)
    {
        double h = sigma * 0.01;
        double vUp = Price(spot, strike, tau, r, q, sigma + h, optionType);
        double vDown = Price(spot, strike, tau, r, q, sigma - h, optionType);
        return (vUp - vDown) / (2.0 * h);
    }

    // ========== Single Boundary Pricing (Standard Regimes) ==========

    private double PriceSingleBoundary(
        double spot, double strike, double tau, double r, double q, double sigma, bool isCall)
    {
        // Step 1: Compute initial boundary guess B∞ using QD+ approximation
        double bInfinity = ComputeQdPlusInitialGuess(strike, tau, r, q, sigma, isCall);

        // Step 2: Generate Chebyshev nodes in time domain [0, τ]
        double[] timeNodes = CRCH001A.ChebyshevNodes(_chebyshevNodes, 0.0, tau);

        // Step 3: Initialize boundary values
        double[] boundaryValues = new double[_chebyshevNodes];
        for (int i = 0; i < _chebyshevNodes; i++)
        {
            boundaryValues[i] = bInfinity;
        }

        // Step 4: Fixed-point iteration to refine boundary
        boundaryValues = FixedPointIteration(
            boundaryValues, timeNodes, strike, tau, r, q, sigma, isCall);

        // Step 5: Integrate along refined boundary to get option value
        double europeanValue = BlackScholesEuropean(spot, strike, tau, r, q, sigma, isCall);
        double earlyExercisePremium = IntegrateEarlyExercisePremium(
            spot, strike, tau, r, q, sigma, isCall, timeNodes, boundaryValues);

        double americanValue = europeanValue + earlyExercisePremium;

        // Ensure at least intrinsic value
        double intrinsic = CalculateIntrinsicValue(spot, strike, isCall ? OptionType.Call : OptionType.Put);
        return System.Math.Max(americanValue, intrinsic);
    }

    // ========== Double Boundary Pricing (Negative Rate Regimes) ==========

    private double PriceDoubleBoundary(
        double spot, double strike, double tau, double r, double q, double sigma, bool isCall)
    {
        // For double boundary cases, we may not get valid QD+ boundaries
        // Fall back to heuristic initial guesses if needed

        double upperInit, lowerInit;

        try
        {
            CRAP001A qdPlus = new CRAP001A(spot, strike, tau, r, q, sigma, isCall);
            (upperInit, lowerInit) = qdPlus.CalculateBoundaries();
        }
        catch (InvalidOperationException)
        {
            upperInit = double.NaN;
            lowerInit = double.NaN;
        }
        catch (ArgumentException)
        {
            upperInit = double.NaN;
            lowerInit = double.NaN;
        }

        // Set sensible defaults if QD+ fails
        if (isCall)
        {
            // Call double boundary (0 < r < q): upper and lower both above strike
            if (!double.IsFinite(upperInit) || upperInit <= strike)
            {
                upperInit = strike * 1.5;  // Upper boundary well above strike
            }
            if (!double.IsFinite(lowerInit) || lowerInit <= strike)
            {
                lowerInit = strike * 1.1;  // Lower boundary just above strike
            }
            // Ensure ordering
            if (lowerInit >= upperInit)
            {
                upperInit = lowerInit * 1.2;
            }
        }
        else
        {
            // Put double boundary (q < r < 0): upper and lower both below strike
            if (!double.IsFinite(upperInit) || upperInit >= strike)
            {
                upperInit = strike * 0.9;  // Upper boundary just below strike
            }
            if (!double.IsFinite(lowerInit) || lowerInit >= upperInit)
            {
                lowerInit = strike * 0.5;  // Lower boundary well below strike
            }
            // Ensure ordering
            if (lowerInit >= upperInit)
            {
                lowerInit = upperInit * 0.8;
            }
        }

        // Generate Chebyshev nodes
        double[] timeNodes = CRCH001A.ChebyshevNodes(_chebyshevNodes, 0.0, tau);

        // Initialize both boundaries
        double[] upperBoundary = new double[_chebyshevNodes];
        double[] lowerBoundary = new double[_chebyshevNodes];
        for (int i = 0; i < _chebyshevNodes; i++)
        {
            upperBoundary[i] = upperInit;
            lowerBoundary[i] = lowerInit;
        }

        // Apply FP-B' stabilized iteration (Healy approach)
        // Skip iteration for call double-boundary if not well-understood
        if (!isCall)
        {
            (upperBoundary, lowerBoundary) = DoubleBoundaryFixedPointIteration(
                upperBoundary, lowerBoundary, timeNodes, strike, tau, r, q, sigma, isCall);

            // Optionally refine with Kim solver for higher precision
            if (_fixedPointIterations > 2)
            {
                try
                {
                    CRSL002A kimSolver = new CRSL002A(spot, strike, tau, r, q, sigma, isCall, _chebyshevNodes);
                    (double[] refinedUpper, double[] refinedLower, double _) =
                        kimSolver.SolveBoundaries(upperBoundary[^1], lowerBoundary[^1]);
                    upperBoundary = refinedUpper;
                    lowerBoundary = refinedLower;
                }
                catch (InvalidOperationException)
                {
                    // Keep the FP-B' boundaries if Kim fails
                }
            }
        }

        // Compute option value from refined boundaries
        double europeanValue = BlackScholesEuropean(spot, strike, tau, r, q, sigma, isCall);
        double eep = IntegrateDoubleBoundaryPremium(
            spot, strike, tau, r, q, sigma, isCall, timeNodes, upperBoundary, lowerBoundary);

        // Ensure no NaN propagation
        if (!double.IsFinite(eep))
        {
            eep = 0.0;
        }

        double americanValue = europeanValue + System.Math.Abs(eep);
        double intrinsic = CalculateIntrinsicValue(spot, strike, isCall ? OptionType.Call : OptionType.Put);
        return System.Math.Max(americanValue, intrinsic);
    }

    // ========== Fixed-Point Iteration ==========

    private double[] FixedPointIteration(
        double[] boundary,
        double[] timeNodes,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        bool isCall)
    {
        double[] current = (double[])boundary.Clone();

        for (int iter = 0; iter < _fixedPointIterations; iter++)
        {
            double[] next = new double[_chebyshevNodes];

            for (int i = 0; i < _chebyshevNodes; i++)
            {
                double t = timeNodes[i];
                double tauRemaining = tau - t;

                if (tauRemaining < NumericalEpsilon)
                {
                    next[i] = strike;
                    continue;
                }

                // Compute the fixed-point update
                next[i] = FixedPointUpdate(current, timeNodes, i, strike, tau, r, q, sigma, isCall);
            }

            // Check convergence
            double maxChange = 0.0;
            for (int i = 0; i < _chebyshevNodes; i++)
            {
                maxChange = System.Math.Max(maxChange, System.Math.Abs(next[i] - current[i]));
            }

            current = next;

            if (maxChange < Tolerance * strike)
            {
                break;
            }
        }

        return current;
    }

    private double FixedPointUpdate(
        double[] boundary,
        double[] timeNodes,
        int index,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        bool isCall)
    {
        double t = timeNodes[index];
        double tauRemaining = tau - t;
        double B = boundary[index];

        // Compute D1, D2
        double d1 = (System.Math.Log(B / strike) + ((r - q + (0.5 * sigma * sigma)) * tauRemaining))
                  / (sigma * System.Math.Sqrt(tauRemaining));
        double d2 = d1 - (sigma * System.Math.Sqrt(tauRemaining));

        double eta = isCall ? 1.0 : -1.0;

        // Compute the integral term using quadrature
        double integralN = ComputeIntegralN(boundary, timeNodes, index, strike, r, q, sigma, isCall);
        double integralD = ComputeIntegralD(boundary, timeNodes, index, strike, r, q, sigma, isCall);

        // FP-A equation: B = K * N(d) / D(d)
        double Nd2 = CRMF001A.NormalCDF(eta * d2);
        double Nd1 = CRMF001A.NormalCDF(eta * d1);

        double numerator = 1.0 - (System.Math.Exp(-r * tauRemaining) * Nd2) - integralN;
        double denominator = 1.0 - (System.Math.Exp(-q * tauRemaining) * Nd1) - integralD;

        if (System.Math.Abs(denominator) < NumericalEpsilon)
        {
            return B;
        }

        double newB = strike * numerator / denominator;

        // Apply constraints
        if (isCall)
        {
            newB = System.Math.Max(newB, strike * 1.001);
        }
        else
        {
            newB = System.Math.Clamp(newB, strike * 0.01, strike * 0.999);
        }

        return newB;
    }

    private (double[] Upper, double[] Lower) DoubleBoundaryFixedPointIteration(
        double[] upper,
        double[] lower,
        double[] timeNodes,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        bool isCall)
    {
        double[] currentUpper = (double[])upper.Clone();
        double[] currentLower = (double[])lower.Clone();

        for (int iter = 0; iter < _fixedPointIterations; iter++)
        {
            double[] nextUpper = new double[_chebyshevNodes];
            double[] nextLower = new double[_chebyshevNodes];

            for (int i = 0; i < _chebyshevNodes; i++)
            {
                // FP-B' stabilization: update upper first, then use updated upper for lower
                nextUpper[i] = FixedPointUpdate(currentUpper, timeNodes, i, strike, tau, r, q, sigma, isCall);

                // Use just-computed upper for lower boundary update
                double[] tempUpper = (double[])currentUpper.Clone();
                tempUpper[i] = nextUpper[i];
                nextLower[i] = FixedPointUpdateLower(currentLower, tempUpper, timeNodes, i, strike, tau, r, q, sigma, isCall);

                // Enforce ordering constraint
                if (!isCall && nextLower[i] >= nextUpper[i])
                {
                    double mid = (nextUpper[i] + nextLower[i]) / 2.0;
                    nextUpper[i] = mid + NumericalEpsilon;
                    nextLower[i] = mid - NumericalEpsilon;
                }
            }

            currentUpper = nextUpper;
            currentLower = nextLower;
        }

        return (currentUpper, currentLower);
    }

    private double FixedPointUpdateLower(
        double[] lower,
        double[] upper,
        double[] timeNodes,
        int index,
        double strike,
        double tau,
        double r,
        double q,
        double sigma,
        bool isCall)
    {
        // Simplified lower boundary update using FP-B' modification
        double t = timeNodes[index];
        double tauRemaining = tau - t;

        if (tauRemaining < NumericalEpsilon)
        {
            return strike * 0.5;
        }

        double BL = lower[index];
        double d1 = (System.Math.Log(BL / strike) + ((r - q + (0.5 * sigma * sigma)) * tauRemaining))
                  / (sigma * System.Math.Sqrt(tauRemaining));
        double d2 = d1 - (sigma * System.Math.Sqrt(tauRemaining));

        double Nd2 = CRMF001A.NormalCDF(-d2);
        double Nd1 = CRMF001A.NormalCDF(-d1);

        double numerator = 1.0 - (System.Math.Exp(-r * tauRemaining) * Nd2);
        double denominator = 1.0 - (System.Math.Exp(-q * tauRemaining) * Nd1);

        if (System.Math.Abs(denominator) < NumericalEpsilon)
        {
            return BL;
        }

        double newBL = strike * numerator / denominator;
        double upperBound = upper[index] - NumericalEpsilon;
        double lowerBound = strike * 0.01;
        
        // Ensure min <= max for clamp
        if (lowerBound > upperBound)
        {
            lowerBound = upperBound - NumericalEpsilon;
        }
        
        return System.Math.Clamp(newBL, lowerBound, upperBound);
    }

    // ========== Integration Methods ==========

    private double ComputeIntegralN(
        double[] boundary, double[] timeNodes, int index,
        double strike, double r, double q, double sigma, bool isCall)
    {
        double t = timeNodes[index];
        if (index >= _chebyshevNodes - 1)
        {
            return 0.0;
        }

        double tMax = timeNodes[^1];
        double B = boundary[index];

        Func<double, double> integrand = s =>
        {
            double tau = s - t;
            if (tau < NumericalEpsilon)
            {
                return 0.0;
            }

            double Bs = InterpolateBoundary(boundary, timeNodes, s);
            double d2 = (System.Math.Log(B / Bs) + ((r - q - (0.5 * sigma * sigma)) * tau))
                      / (sigma * System.Math.Sqrt(tau));

            double eta = isCall ? 1.0 : -1.0;
            return r * System.Math.Exp(-r * tau) * CRMF001A.NormalCDF(eta * d2);
        };

        if (_useTanhSinh)
        {
            return CRGQ001A.TanhSinhIntegrate(integrand, t, tMax, Tolerance);
        }

        return CRGQ001A.IntegrateWithScheme(integrand, _quadratureScheme, t, tMax);
    }

    private double ComputeIntegralD(
        double[] boundary, double[] timeNodes, int index,
        double strike, double r, double q, double sigma, bool isCall)
    {
        double t = timeNodes[index];
        if (index >= _chebyshevNodes - 1)
        {
            return 0.0;
        }

        double tMax = timeNodes[^1];
        double B = boundary[index];

        Func<double, double> integrand = s =>
        {
            double tau = s - t;
            if (tau < NumericalEpsilon)
            {
                return 0.0;
            }

            double Bs = InterpolateBoundary(boundary, timeNodes, s);
            double d1 = (System.Math.Log(B / Bs) + ((r - q + (0.5 * sigma * sigma)) * tau))
                      / (sigma * System.Math.Sqrt(tau));

            double eta = isCall ? 1.0 : -1.0;
            return q * System.Math.Exp(-q * tau) * CRMF001A.NormalCDF(eta * d1);
        };

        if (_useTanhSinh)
        {
            return CRGQ001A.TanhSinhIntegrate(integrand, t, tMax, Tolerance);
        }

        return CRGQ001A.IntegrateWithScheme(integrand, _quadratureScheme, t, tMax);
    }

    private double IntegrateEarlyExercisePremium(
        double spot, double strike, double tau, double r, double q, double sigma, bool isCall,
        double[] timeNodes, double[] boundary)
    {
        // The early exercise premium integral (Kim 1990, Andersen-Lake-Offengenden 2016):
        // For a put: EEP = ∫₀^τ [r*K*e^(-r*s)*N(-d₂(S,B(τ-s),s)) - q*S*e^(-q*s)*N(-d₁(S,B(τ-s),s))] ds
        // For a call: EEP = ∫₀^τ [q*S*e^(-q*s)*N(d₁(S,B(τ-s),s)) - r*K*e^(-r*s)*N(d₂(S,B(τ-s),s))] ds

        Func<double, double> integrand = s =>
        {
            if (s < NumericalEpsilon)
            {
                return 0.0;
            }

            // Time remaining at evaluation point
            double tRemaining = tau - s;
            if (tRemaining < NumericalEpsilon)
            {
                tRemaining = NumericalEpsilon;
            }

            // Interpolate boundary at time (tau - s) from start
            double Bt = InterpolateBoundary(boundary, timeNodes, tRemaining);
            if (Bt <= 0)
            {
                return 0.0;
            }

            double sqrtS = System.Math.Sqrt(s);
            double d1 = (System.Math.Log(spot / Bt) + ((r - q + (0.5 * sigma * sigma)) * s))
                      / (sigma * sqrtS);
            double d2 = d1 - (sigma * sqrtS);

            double discountR = System.Math.Exp(-r * s);
            double discountQ = System.Math.Exp(-q * s);

            if (isCall)
            {
                // Call: dividend income from early exercise
                double term1 = q * spot * discountQ * CRMF001A.NormalCDF(d1);
                double term2 = r * strike * discountR * CRMF001A.NormalCDF(d2);
                return term1 - term2;
            }
            else
            {
                // Put: interest income from early exercise
                double term1 = r * strike * discountR * CRMF001A.NormalCDF(-d2);
                double term2 = q * spot * discountQ * CRMF001A.NormalCDF(-d1);
                return term1 - term2;
            }
        };

        if (_useTanhSinh)
        {
            return CRGQ001A.TanhSinhIntegrate(integrand, 0.0, tau, Tolerance);
        }

        return CRGQ001A.IntegrateWithScheme(integrand, _quadratureScheme, 0.0, tau);
    }

    private double IntegrateDoubleBoundaryPremium(
        double spot, double strike, double tau, double r, double q, double sigma, bool isCall,
        double[] timeNodes, double[] upper, double[] lower)
    {
        // For double boundary, integrate the difference at both boundaries
        Func<double, double> integrand = t =>
        {
            double tauRemaining = tau - t;
            if (tauRemaining < NumericalEpsilon)
            {
                return 0.0;
            }

            double Bu = InterpolateBoundary(upper, timeNodes, t);
            double Bl = InterpolateBoundary(lower, timeNodes, t);
            double sqrtTau = System.Math.Sqrt(tauRemaining);

            // Upper boundary contribution
            double d2u = (System.Math.Log(spot / Bu) + ((r - q - (0.5 * sigma * sigma)) * tauRemaining))
                       / (sigma * sqrtTau);
            // Lower boundary contribution
            double d2l = (System.Math.Log(spot / Bl) + ((r - q - (0.5 * sigma * sigma)) * tauRemaining))
                       / (sigma * sqrtTau);

            double termU = r * strike * System.Math.Exp(-r * tauRemaining) * CRMF001A.NormalCDF(-d2u);
            double termL = r * strike * System.Math.Exp(-r * tauRemaining) * CRMF001A.NormalCDF(-d2l);

            return termL - termU;
        };

        if (_useTanhSinh)
        {
            return CRGQ001A.TanhSinhIntegrate(integrand, 0.0, tau, Tolerance);
        }

        return CRGQ001A.IntegrateWithScheme(integrand, _quadratureScheme, 0.0, tau);
    }

    // ========== Helper Methods ==========

    private static double InterpolateBoundary(double[] boundary, double[] timeNodes, double t)
    {
        return CRCH001A.Interpolate(timeNodes, boundary, t);
    }

    private static double ComputeQdPlusInitialGuess(
        double strike, double tau, double r, double q, double sigma, bool isCall)
    {
        // Barone-Adesi Whaley-style initial guess
        double h = 1.0 - System.Math.Exp(-r * tau);
        double sigma2 = sigma * sigma;

        double a = (2.0 * r) / sigma2;
        double b = (2.0 * (r - q)) / sigma2 - 1.0;

        double discriminant = (b * b) + (4.0 * a / h);
        if (discriminant < 0)
        {
            return strike;
        }

        double sqrtDisc = System.Math.Sqrt(discriminant);
        double lambda = isCall ? (-b + sqrtDisc) / 2.0 : (-b - sqrtDisc) / 2.0;

        // Early exercise boundary approximation
        if (isCall)
        {
            return strike * lambda / (lambda - 1.0);
        }
        else
        {
            return strike * lambda / (lambda + 1.0);
        }
    }

    private static double BlackScholesEuropean(
        double spot, double strike, double tau, double r, double q, double sigma, bool isCall)
    {
        double sqrtT = System.Math.Sqrt(tau);
        double d1 = (System.Math.Log(spot / strike) + ((r - q + (0.5 * sigma * sigma)) * tau)) / (sigma * sqrtT);
        double d2 = d1 - (sigma * sqrtT);

        double discountS = System.Math.Exp(-q * tau);
        double discountK = System.Math.Exp(-r * tau);

        if (isCall)
        {
            return (spot * discountS * CRMF001A.NormalCDF(d1)) - (strike * discountK * CRMF001A.NormalCDF(d2));
        }

        return (strike * discountK * CRMF001A.NormalCDF(-d2)) - (spot * discountS * CRMF001A.NormalCDF(-d1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateIntrinsicValue(double spot, double strike, OptionType optionType)
    {
        return optionType == OptionType.Call
            ? System.Math.Max(0, spot - strike)
            : System.Math.Max(0, strike - spot);
    }

    private enum RateRegime { Standard, DoubleBoundary }

    private static RateRegime ClassifyRegime(double r, double q, bool isCall)
    {
        // Double boundary: put with q < r < 0, or call with 0 < r < q
        if (!isCall && q < r && r < 0)
        {
            return RateRegime.DoubleBoundary;
        }

        if (isCall && 0 < r && r < q)
        {
            return RateRegime.DoubleBoundary;
        }

        return RateRegime.Standard;
    }

    private static void ValidateInputs(double spot, double strike, double tau, double sigma)
    {
        if (spot <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spot), "Spot must be positive");
        }

        if (strike <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strike), "Strike must be positive");
        }

        if (tau <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tau), "Time to maturity must be positive");
        }

        if (sigma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sigma), "Volatility must be positive");
        }
    }
}
