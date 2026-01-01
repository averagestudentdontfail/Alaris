// CRSL002A.cs - Kim integral equation solver with FP-B' stabilized fixed point
// Component ID: CRSL002A
// Migrated from: Alaris.Double.DBSL002A

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Alaris.Core.Validation;

namespace Alaris.Core.Pricing;

/// <summary>
/// Refines QD+ boundaries using Kim integral equation with stabilized FP-B' iteration.
/// </summary>
public sealed class CRSL002A
{
    private readonly double _spot;
    private readonly double _strike;
    private readonly double _maturity;
    private readonly double _rate;
    private readonly double _dividendYield;
    private readonly double _volatility;
    private readonly bool _isCall;
    private readonly int _collocationPoints;

    /// <summary>
    /// Number of collocation points used for boundary refinement.
    /// </summary>
    public int CollocationPoints => _collocationPoints;

    private const double Tolerance = 1e-6;
    private const int MaxIterations = 100;
    private const int IntegrationPoints = 50;
    private const double NumericalEpsilon = 1e-10;
    private const double CrossingTimeThreshold = 1e-2; // Healy recommendation: Δt < 10^-2

    /// <summary>
    /// Initializes a new Kim solver for double boundaries.
    /// </summary>
    public CRSL002A(
        double spot,
        double strike,
        double maturity,
        double rate,
        double dividendYield,
        double volatility,
        bool isCall,
        int collocationPoints = 50)
    {
        // Standardised bounds validation (Rule 9)
        AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot, strike, maturity, rate, dividendYield, volatility);

        if (collocationPoints < 2)
        {
            throw new ArgumentException("Collocation points must be at least 2", nameof(collocationPoints));
        }

        _spot = spot;
        _strike = strike;
        _maturity = maturity;
        _rate = rate;
        _dividendYield = dividendYield;
        _volatility = volatility;
        _isCall = isCall;
        _collocationPoints = collocationPoints;
    }

    /// <summary>
    /// Solves for refined boundaries using Kim's integral equation with FP-B' stabilized iteration.
    /// </summary>
    /// <param name="upperInitial">Initial upper boundary from QD+.</param>
    /// <param name="lowerInitial">Initial lower boundary from QD+.</param>
    /// <returns>Refined (upper, lower) boundaries and crossing time.</returns>
    public (double[] Upper, double[] Lower, double CrossingTime) SolveBoundaries(
        double upperInitial, double lowerInitial)
    {
        int m = _collocationPoints;
        double[] upper = new double[m];
        double[] lower = new double[m];

        // Initialize with QD+ values as constant starting guess
        for (int i = 0; i < m; i++)
        {
            upper[i] = upperInitial;
            lower[i] = lowerInitial;
        }

        // Find initial crossing time estimate
        double crossingTime = FindCrossingTime(upper, lower);

        // Refine crossing time by subdivision (Healy p.12)
        crossingTime = RefineCrossingTime(upper, lower, crossingTime);

        // Adjust initial guess if boundaries cross (Healy p.12 procedure)
        AdjustCrossingInitialGuess(upper, lower, crossingTime);

        // Refine using FP-B' stabilized iteration
        (double[] refinedUpper, double[] refinedLower) = RefineUsingFpbPrime(upper, lower, crossingTime);

        return (refinedUpper, refinedLower, crossingTime);
    }

    /// <summary>
    /// Refines boundaries using FP-B' stabilized fixed point iteration (Healy Equations 33-35).
    /// Uses ArrayPool for zero-allocation iteration buffers (Rule 5: Zero-Allocation Hot Paths).
    /// </summary>
    private (double[] Upper, double[] Lower) RefineUsingFpbPrime(
        double[] upperInitial, double[] lowerInitial, double crossingTime)
    {
        int m = _collocationPoints;

        // Rule 13: Extract validation
        if (!ValidateInitialInputs(upperInitial, lowerInitial))
        {
            return FallbackToQdPlusConstant(m);
        }

        // Rule 5: Use ArrayPool to avoid heap allocations in hot path
        double[] upper = ArrayPool<double>.Shared.Rent(m);
        double[] lower = ArrayPool<double>.Shared.Rent(m);
        double[] upperNew = ArrayPool<double>.Shared.Rent(m);
        double[] lowerNew = ArrayPool<double>.Shared.Rent(m);
        double[] tempUpper = ArrayPool<double>.Shared.Rent(m);

        try
        {
            // Copy initial values
            Array.Copy(upperInitial, upper, m);
            Array.Copy(lowerInitial, lower, m);

            double previousMaxChange = double.MaxValue;
            int stagnationCount = 0;

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                double maxChange = 0.0;
                double maxUpperChange = 0.0;
                double maxLowerChange = 0.0;

                // FP-B' iteration (Equation 33)
                for (int i = 0; i < m; i++)
                {
                    double ti = i * _maturity / (m - 1);

                    // Skip points before crossing time
                    if (ti < crossingTime - NumericalEpsilon)
                    {
                        upperNew[i] = upper[i];
                        lowerNew[i] = lower[i];
                        continue;
                    }

                    // Update upper boundary using FP-B
                    upperNew[i] = SolveUpperBoundaryPoint(ti, upper, lower, crossingTime);

                    // CRITICAL: Update lower using just-computed upper (FP-B' stabilization)
                    // Use pooled tempUpper instead of cloning
                    Array.Copy(upper, tempUpper, m);
                    tempUpper[i] = upperNew[i];
                    lowerNew[i] = SolveLowerBoundaryPointStabilized(ti, lower, tempUpper, crossingTime);

                    // Enforce constraints
                    (upperNew[i], lowerNew[i]) = EnforceConstraints(upperNew[i], lowerNew[i]);

                    maxUpperChange = System.Math.Max(maxUpperChange, System.Math.Abs(upperNew[i] - upper[i]));
                    maxLowerChange = System.Math.Max(maxLowerChange, System.Math.Abs(lowerNew[i] - lower[i]));
                    maxChange = System.Math.Max(maxChange, System.Math.Max(maxUpperChange, maxLowerChange));
                }

                // Check for early convergence on first iteration
                if (iter == 0 && maxUpperChange < Tolerance * 10 && maxLowerChange < Tolerance * 10)
                {
                    // Return copies of initial (don't return pooled arrays)
                    return ((double[])upperInitial.Clone(), (double[])lowerInitial.Clone());
                }

                // Rule 13: Extract stagnation check
                if (CheckStagnation(iter, maxChange, ref previousMaxChange, ref stagnationCount))
                {
                    return FallbackToQdPlusConstant(m);
                }

                // Swap buffers instead of allocating new arrays
                (upper, upperNew) = (upperNew, upper);
                (lower, lowerNew) = (lowerNew, lower);

                if (maxChange < Tolerance)
                {
                    break;
                }
            }

            // 1. Enforce Monotonicity (PAV Algorithm)
            double[] upperMono = EnforceMonotonicity(upper, m, isIncreasing: false);
            double[] lowerMono = EnforceMonotonicity(lower, m, isIncreasing: false);

            // 2. Apply Smoothing (Savitzky-Golay)
            double[] upperSmooth = SmoothBoundary(upperMono);
            double[] lowerSmooth = SmoothBoundary(lowerMono);

            // Rule 13: Extract result validation
            if (!ValidateRefinementResult(upperSmooth, lowerSmooth, upperInitial, lowerInitial))
            {
                return ((double[])upperInitial.Clone(), (double[])lowerInitial.Clone());
            }

            return (upperSmooth, lowerSmooth);
        }
        finally
        {
            // Always return pooled arrays
            ArrayPool<double>.Shared.Return(upper);
            ArrayPool<double>.Shared.Return(lower);
            ArrayPool<double>.Shared.Return(upperNew);
            ArrayPool<double>.Shared.Return(lowerNew);
            ArrayPool<double>.Shared.Return(tempUpper);
        }
    }

    private bool ValidateInitialInputs(double[] upper, double[] lower)
    {
        int lastIdx = _collocationPoints - 1;
        double upperRatio = upper[lastIdx] / _strike;
        double lowerRatio = lower[lastIdx] / _strike;

        bool upperReasonable = upperRatio >= 0.60 && upperRatio < 0.90;
        bool lowerReasonable = lowerRatio >= 0.45 && lowerRatio < 0.85;
        bool orderingCorrect = upper[lastIdx] > lower[lastIdx];

        return upperReasonable && lowerReasonable && orderingCorrect;
    }

    private (double[] Upper, double[] Lower) FallbackToQdPlusConstant(int m)
    {
        CRAP001A qdplus = new CRAP001A(
            _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall);
        (double qdUpper, double qdLower) = qdplus.CalculateBoundaries();

        return (Enumerable.Repeat(qdUpper, m).ToArray(),
                Enumerable.Repeat(qdLower, m).ToArray());
    }

    private static bool CheckStagnation(int iter, double maxChange, ref double previousMaxChange, ref int stagnationCount)
    {
        if (iter > 3 && System.Math.Abs(maxChange - previousMaxChange) < Tolerance)
        {
            stagnationCount++;
        }
        else
        {
            stagnationCount = 0;
        }

        previousMaxChange = maxChange;
        return stagnationCount > 3;
    }

    private bool ValidateRefinementResult(double[] upper, double[] lower, double[] upperInit, double[] lowerInit)
    {
        int lastIdx = _collocationPoints - 1;
        double upperChange = System.Math.Abs(upper[lastIdx] - upperInit[lastIdx]);
        double lowerChange = System.Math.Abs(lower[lastIdx] - lowerInit[lastIdx]);

        // If minimal changes, accept (QD+ was optimal)
        if (upperChange < 1e-4 && lowerChange < 1e-4)
        {
            return true;
        }

        // Reject suspicious large deviations (> 0.2% relative AND > 0.1 absolute)
        double upperRel = upperChange / System.Math.Abs(upperInit[lastIdx]);
        double lowerRel = lowerChange / System.Math.Abs(lowerInit[lastIdx]);

        bool upperSuspicious = upperRel > 0.002 && upperChange > 0.1;
        bool lowerSuspicious = lowerRel > 0.002 && lowerChange > 0.1;

        return !upperSuspicious && !lowerSuspicious;
    }

    private double SolveUpperBoundaryPoint(double ti, double[] upper, double[] lower, double crossingTime)
    {
        double Ni = CalculateNumerator(ti, upper, lower, crossingTime, isUpper: true);
        double Di = CalculateDenominator(ti, upper, lower, crossingTime, isUpper: true);

        if (IsInvalidRatio(Ni, Di))
        {
            return InterpolateBoundary(upper, ti);
        }

        double ratio = Ni / Di;

        // Put constraint: ratio < 1
        if (!_isCall && ratio >= 1.0)
        {
            return InterpolateBoundary(upper, ti);
        }

        double result = _strike * ratio;
        return SanitizeBoundaryValue(result, isUpper: true);
    }

    private double SolveLowerBoundaryPointStabilized(
        double ti, double[] lowerOld, double[] upperNew, double crossingTime)
    {
        double NiPrime = CalculateNumeratorPrime(ti, lowerOld, upperNew, crossingTime);
        double DiPrime = CalculateDenominatorPrime(ti, lowerOld);

        if (IsInvalidRatio(NiPrime, DiPrime))
        {
            return InterpolateBoundary(lowerOld, ti);
        }

        double ratio = NiPrime / DiPrime;
        double upperAtTi = InterpolateBoundary(upperNew, ti);

        double result = _strike * ratio;
        
        // Put constraint: lower < upper
        if (!_isCall && result >= upperAtTi)
        {
             return InterpolateBoundary(lowerOld, ti);
        }

        return SanitizeBoundaryValue(result, isUpper: false);
    }

    private double SanitizeBoundaryValue(double value, bool isUpper)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return double.NaN; // Caller handles NaN via fallback
        }

        if (!_isCall)
        {
            // Put Constraints
            if (isUpper)
            {
                value = System.Math.Min(value, _strike * 0.999);
            }
            value = System.Math.Max(value, NumericalEpsilon);
        }
        else
        {
            // Call Constraints
            if (isUpper)
            {
                value = System.Math.Max(value, _strike * 1.001);
            }
            value = System.Math.Max(value, NumericalEpsilon);
        }

        return value;
    }

    private static bool IsInvalidRatio(double numerator, double denominator)
    {
        return double.IsNaN(numerator) || 
               double.IsNaN(denominator) || 
               System.Math.Abs(denominator) < NumericalEpsilon || 
               numerator < 0 || 
               denominator < 0;
    }

    private double CalculateNumerator(double ti, double[] upper, double[] lower,
        double crossingTime, bool isUpper)
    {
        double[] boundary = isUpper ? upper : lower;
        double Bi = InterpolateBoundary(boundary, ti);

        if (double.IsNaN(Bi) || Bi <= 0)
        {
            return double.NaN;
        }

        double tau = _maturity - ti;
        if (tau < NumericalEpsilon)
        {
            return 1.0;
        }

        double d2Term = CalculateD2(Bi, _strike, tau);
        double nonIntegral = 1.0 - (System.Math.Exp(-_rate * tau) * NormalCDF(-d2Term));

        double integral = CalculateIntegralTerm(ti, Bi, upper, lower, crossingTime, isNumerator: true);

        return double.IsNaN(integral) ? nonIntegral : nonIntegral - integral;
    }

    private double CalculateDenominator(double ti, double[] upper, double[] lower,
        double crossingTime, bool isUpper)
    {
        double[] boundary = isUpper ? upper : lower;
        double Bi = InterpolateBoundary(boundary, ti);

        if (double.IsNaN(Bi) || Bi <= 0)
        {
            return double.NaN;
        }

        double tau = _maturity - ti;
        if (tau < NumericalEpsilon)
        {
            return 1.0;
        }

        double d1Term = CalculateD1(Bi, _strike, tau);
        double nonIntegral = 1.0 - (System.Math.Exp(-_dividendYield * tau) * NormalCDF(-d1Term));

        double integral = CalculateIntegralTerm(ti, Bi, upper, lower, crossingTime, isNumerator: false);

        return double.IsNaN(integral) ? nonIntegral : nonIntegral - integral;
    }

    private double CalculateNumeratorPrime(double ti, double[] lower, double[] upper, double crossingTime)
    {
        double Bi = InterpolateBoundary(lower, ti);
        double N = CalculateNumerator(ti, upper, lower, crossingTime, isUpper: false);
        
        double integralD = CalculateIntegralTerm(ti, Bi, upper, lower, crossingTime, isNumerator: false);
        
        return double.IsNaN(integralD) ? N : N + (Bi / _strike * integralD);
    }

    private double CalculateDenominatorPrime(double ti, double[] lower)
    {
        double Bi = InterpolateBoundary(lower, ti);
        double tau = _maturity - ti;
        double d1Term = CalculateD1(Bi, _strike, tau);

        return 1.0 - (System.Math.Exp(-_dividendYield * tau) * NormalCDF(-d1Term));
    }

    /// <summary>
    /// Unified integral calculation for N (r-weighted) and D (q-weighted).
    /// </summary>
    private double CalculateIntegralTerm(double ti, double Bi, double[] upper, double[] lower,
        double crossingTime, bool isNumerator)
    {
        double tStart = System.Math.Max(ti, crossingTime);
        if (tStart >= _maturity)
        {
            return 0.0;
        }

        double integral = 0.0;
        double dt = (_maturity - tStart) / IntegrationPoints;

        for (int j = 0; j <= IntegrationPoints; j++)
        {
            double t = tStart + (j * dt);
            double upperVal = InterpolateBoundary(upper, t);
            double lowerVal = InterpolateBoundary(lower, t);

            if (IsInvalidBoundary(upperVal, lowerVal))
            {
                continue;
            }

            double tau = t - ti;
            if (tau < NumericalEpsilon)
            {
                continue;
            }

            double termUpper, termLower;
            if (isNumerator)
            {
                termUpper = CalculateD2(Bi, upperVal, tau);
                termLower = CalculateD2(Bi, lowerVal, tau);
            }
            else
            {
                termUpper = CalculateD1(Bi, upperVal, tau);
                termLower = CalculateD1(Bi, lowerVal, tau);
            }

            double factor = isNumerator ? 
                _rate * System.Math.Exp(-_rate * tau) : 
                _dividendYield * System.Math.Exp(-_dividendYield * tau);

            double integrand = factor * (NormalCDF(-termUpper) - NormalCDF(-termLower));

            if (double.IsNaN(integrand) || double.IsInfinity(integrand))
            {
                continue;
            }

            double weight = (j == 0 || j == IntegrationPoints) ? 0.5 : 1.0;
            integral += weight * integrand * dt;
        }

        return integral;
    }

    private static bool IsInvalidBoundary(double upper, double lower)
    {
        return double.IsNaN(upper) || double.IsNaN(lower) ||
               upper <= 0 || lower <= 0 || lower >= upper;
    }

    private double FindCrossingTime(double[] upper, double[] lower)
    {
        for (int i = 1; i < _collocationPoints; i++)
        {
            if (upper[i] <= lower[i])
            {
                return i * _maturity / (_collocationPoints - 1);
            }
        }
        return 0.0;
    }

    private double RefineCrossingTime(double[] upper, double[] lower, double initialCrossing)
    {
        if (initialCrossing <= 0 || initialCrossing >= _maturity)
        {
            return initialCrossing;
        }

        double left = System.Math.Max(0, initialCrossing - 0.1);
        double right = System.Math.Min(_maturity, initialCrossing + 0.1);

        while (right - left > CrossingTimeThreshold)
        {
            double mid = (left + right) / 2.0;
            double upperVal = InterpolateBoundary(upper, mid);
            double lowerVal = InterpolateBoundary(lower, mid);

            if (upperVal > lowerVal)
            {
                left = mid;
            }
            else
            {
                right = mid;
            }
        }

        return (left + right) / 2.0;
    }

    private void AdjustCrossingInitialGuess(double[] upper, double[] lower, double crossingTime)
    {
        if (crossingTime <= 0 || crossingTime >= _maturity)
        {
            return;
        }

        int crossingIndex = (int)(crossingTime / _maturity * (_collocationPoints - 1));
        double crossingValue = (upper[crossingIndex] + lower[crossingIndex]) / 2.0;

        for (int i = 0; i <= crossingIndex; i++)
        {
            upper[i] = crossingValue;
            lower[i] = crossingValue;
        }
    }

    private (double Upper, double Lower) EnforceConstraints(double upper, double lower)
    {
        if (!_isCall)
        {
            upper = System.Math.Min(upper, _strike);
            lower = System.Math.Max(lower, 0.0);
        }
        else
        {
            upper = System.Math.Max(upper, _strike);
            lower = System.Math.Max(lower, 0.0);
        }

        // Ordering constraint
        if (lower >= upper)
        {
            double mid = (upper + lower) / 2.0;
            upper = mid + NumericalEpsilon;
            lower = mid - NumericalEpsilon;
        }

        return (upper, lower);
    }

    private double InterpolateBoundary(double[] boundary, double t)
    {
        if (boundary.Length == 1)
        {
            return boundary[0];
        }

        double index = t / _maturity * (boundary.Length - 1);
        int i0 = (int)System.Math.Floor(index);
        int i1 = System.Math.Min(i0 + 1, boundary.Length - 1);
        double alpha = index - i0;

        return (boundary[i0] * (1.0 - alpha)) + (boundary[i1] * alpha);
    }

    private double CalculateD1(double S, double K, double tau)
    {
        if (tau <= NumericalEpsilon)
        {
            return 0.0;
        }
        return (System.Math.Log(S / K) + ((_rate - _dividendYield + (0.5 * _volatility * _volatility)) * tau)) 
             / (_volatility * System.Math.Sqrt(tau));
    }

    private double CalculateD2(double S, double K, double tau)
    {
        if (tau <= NumericalEpsilon)
        {
            return 0.0;
        }
        return CalculateD1(S, K, tau) - (_volatility * System.Math.Sqrt(tau));
    }

    private double[] EnforceMonotonicity(double[] boundary, int length, bool isIncreasing)
    {
        // Create properly sized array for PAV algorithm
        double[] values = new double[length];
        Array.Copy(boundary, values, length);
        return PoolAdjacentViolators(values, isIncreasing);
    }

    /// <summary>
    /// Pool Adjacent Violators algorithm for isotonic regression.
    /// Optimized to use stack-allocated spans for small arrays (Rule 5).
    /// </summary>
    private static double[] PoolAdjacentViolators(double[] values, bool increasing)
    {
        int n = values.Length;
        double[] result = new double[n];
        Array.Copy(values, result, n);

        if (!increasing)
        {
            Array.Reverse(result);
            result = PoolAdjacentViolators(result, increasing: true);
            Array.Reverse(result);
            return result;
        }

        // Use fixed-size arrays instead of Lists to avoid heap allocations
        // Maximum pools = n (worst case: each element is its own pool)
        double[] poolValues = ArrayPool<double>.Shared.Rent(n);
        int[] poolSizes = ArrayPool<int>.Shared.Rent(n);
        int poolCount = 0;

        try
        {
            for (int i = 0; i < n; i++)
            {
                poolValues[poolCount] = result[i];
                poolSizes[poolCount] = 1;
                poolCount++;

                // Merge adjacent pools while they violate monotonicity
                while (poolCount > 1 && poolValues[poolCount - 1] < poolValues[poolCount - 2])
                {
                    double sum1 = poolValues[poolCount - 1] * poolSizes[poolCount - 1];
                    double sum2 = poolValues[poolCount - 2] * poolSizes[poolCount - 2];
                    int totalSize = poolSizes[poolCount - 1] + poolSizes[poolCount - 2];

                    poolCount--;
                    poolValues[poolCount - 1] = (sum1 + sum2) / totalSize;
                    poolSizes[poolCount - 1] = totalSize;
                }
            }

            // Expand pools back to result array
            int idx = 0;
            for (int i = 0; i < poolCount; i++)
            {
                for (int j = 0; j < poolSizes[i]; j++)
                {
                    result[idx++] = poolValues[i];
                }
            }

            return result;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(poolValues);
            ArrayPool<int>.Shared.Return(poolSizes);
        }
    }

    /// <summary>
    /// Apply Savitzky-Golay-style smoothing to reduce second-order oscillations.
    /// Uses weighted 5-point moving average for quadratic smoothing.
    /// </summary>
    private static double[] SmoothBoundary(double[] boundary)
    {
        int n = boundary.Length;
        if (n < 5)
        {
            return (double[])boundary.Clone();
        }

        double[] smoothed = new double[n];

        // Savitzky-Golay weights: [-3, 12, 17, 12, -3] / 35
        const double w0 = -3.0 / 35.0;
        const double w1 = 12.0 / 35.0;
        const double w2 = 17.0 / 35.0;

        // Interior points (5-point window)
        for (int i = 2; i < n - 2; i++)
        {
            smoothed[i] = (w0 * boundary[i - 2]) +
                          (w1 * boundary[i - 1]) +
                          (w2 * boundary[i]) +
                          (w1 * boundary[i + 1]) +
                          (w0 * boundary[i + 2]);
        }

        // Edge handling (3-point smoothing)
        smoothed[0] = ((5.0 * boundary[0]) + (2.0 * boundary[1]) - boundary[2]) / 6.0;
        smoothed[1] = (boundary[0] + boundary[1] + boundary[2]) / 3.0;

        smoothed[n - 2] = (boundary[n - 3] + boundary[n - 2] + boundary[n - 1]) / 3.0;
        smoothed[n - 1] = (-boundary[n - 3] + (2.0 * boundary[n - 2]) + (5.0 * boundary[n - 1])) / 6.0;

        return smoothed;
    }

    // Use centralised CRMF001A for math utilities
    private static double NormalCDF(double x) => Alaris.Core.Math.CRMF001A.NormalCDF(x);
}
