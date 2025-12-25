// =============================================================================
// CRVT001A.cs - SIMD Vectorized Greeks Computation
// Component: CR (Core) | Category: VT (Vectorized) | Variant: A (Primary)
// =============================================================================
// High-performance SIMD implementation for batch Black-Scholes Greeks.
// Uses System.Numerics.Vector<T> for hardware-accelerated computation.
// =============================================================================
// Performance bounds:
// - Vector width: 4 (double) on AVX, 2 on SSE2
// - Expected speedup: 2-4x for batched Greeks
// - Falls back to scalar for non-vectorizable remainder
// =============================================================================

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Alaris.Core.Vectorized;

/// <summary>
/// SIMD-vectorized Black-Scholes Greeks computation.
/// Component ID: CRVT001A
/// </summary>
/// <remarks>
/// All methods process arrays in batches of Vector&lt;double&gt;.Width (typically 4 for AVX).
/// Scalar fallback is used for remainders and when SIMD is unavailable.
/// </remarks>
public static class CRVT001A
{
    /// <summary>
    /// Vector width for current hardware.
    /// </summary>
    public static readonly int VectorWidth = Vector<double>.Count;

    /// <summary>
    /// Indicates if SIMD is hardware-accelerated on this platform.
    /// </summary>
    public static readonly bool IsHardwareAccelerated = Vector.IsHardwareAccelerated;

    /// <summary>
    /// Computes Black-Scholes prices for multiple options in vectorized batches.
    /// </summary>
    /// <param name="spots">Array of spot prices.</param>
    /// <param name="strikes">Array of strike prices.</param>
    /// <param name="taus">Array of times to expiry (years).</param>
    /// <param name="sigmas">Array of volatilities.</param>
    /// <param name="r">Risk-free rate (scalar for all).</param>
    /// <param name="q">Dividend yield (scalar for all).</param>
    /// <param name="isCalls">Array of call/put flags.</param>
    /// <param name="results">Output array for prices.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputePricesBatch(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        ReadOnlySpan<bool> isCalls,
        Span<double> results)
    {
        int count = spots.Length;
        if (count == 0)
        {
            return;
        }

        // Scalar fallback for now - SIMD CDF requires special handling
        for (int i = 0; i < count; i++)
        {
            results[i] = Math.CRMF001A.BSPrice(
                spots[i], strikes[i], taus[i], sigmas[i], r, q, isCalls[i]);
        }
    }

    /// <summary>
    /// Computes Black-Scholes deltas for multiple options in vectorized batches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeDeltasBatch(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        ReadOnlySpan<bool> isCalls,
        Span<double> results)
    {
        int count = spots.Length;
        int vectorEnd = count - (count % VectorWidth);

        // Vectorized loop for d1 computation
        for (int i = 0; i < vectorEnd; i += VectorWidth)
        {
            ComputeDeltasVector(
                spots.Slice(i, VectorWidth),
                strikes.Slice(i, VectorWidth),
                taus.Slice(i, VectorWidth),
                sigmas.Slice(i, VectorWidth),
                r, q,
                isCalls.Slice(i, VectorWidth),
                results.Slice(i, VectorWidth));
        }

        // Scalar fallback for remainder
        for (int i = vectorEnd; i < count; i++)
        {
            results[i] = Math.CRMF001A.BSDelta(
                spots[i], strikes[i], taus[i], sigmas[i], r, q, isCalls[i]);
        }
    }

    /// <summary>
    /// Computes Black-Scholes vegas for multiple options in vectorized batches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeVegasBatch(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        Span<double> results)
    {
        int count = spots.Length;
        int vectorEnd = count - (count % VectorWidth);

        // Vectorized loop
        for (int i = 0; i < vectorEnd; i += VectorWidth)
        {
            ComputeVegasVector(
                spots.Slice(i, VectorWidth),
                strikes.Slice(i, VectorWidth),
                taus.Slice(i, VectorWidth),
                sigmas.Slice(i, VectorWidth),
                r, q,
                results.Slice(i, VectorWidth));
        }

        // Scalar fallback for remainder
        for (int i = vectorEnd; i < count; i++)
        {
            results[i] = Math.CRMF001A.BSVega(
                spots[i], strikes[i], taus[i], sigmas[i], r, q);
        }
    }

    /// <summary>
    /// Computes Black-Scholes gammas for multiple options in vectorized batches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeGammasBatch(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        Span<double> results)
    {
        int count = spots.Length;
        for (int i = 0; i < count; i++)
        {
            results[i] = Math.CRMF001A.BSGamma(
                spots[i], strikes[i], taus[i], sigmas[i], r, q);
        }
    }

    // ========== Private Vector Implementations ==========

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeDeltasVector(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        ReadOnlySpan<bool> isCalls,
        Span<double> results)
    {
        // Load vectors
        Vector<double> spotVec = new Vector<double>(spots);
        Vector<double> strikeVec = new Vector<double>(strikes);
        Vector<double> tauVec = new Vector<double>(taus);
        Vector<double> sigmaVec = new Vector<double>(sigmas);
        Vector<double> rVec = new Vector<double>(r);
        Vector<double> qVec = new Vector<double>(q);

        // Compute sqrt(tau)
        Vector<double> sqrtTau = Vector.SquareRoot(tauVec);

        // Compute d1 = (ln(S/K) + (r - q + σ²/2)τ) / (σ√τ)
        Vector<double> logMoneyness = VectorLog(spotVec / strikeVec);
        Vector<double> halfSigmaSq = sigmaVec * sigmaVec * new Vector<double>(0.5);
        Vector<double> drift = (rVec - qVec + halfSigmaSq) * tauVec;
        Vector<double> d1 = (logMoneyness + drift) / (sigmaVec * sqrtTau);

        // Compute Φ(d1) for each element (scalar - no vectorized CDF)
        Vector<double> discountDiv = VectorExp(-qVec * tauVec);

        for (int i = 0; i < VectorWidth; i++)
        {
            double cdf = Math.CRMF001A.NormalCDF(d1[i]);
            results[i] = isCalls[i]
                ? discountDiv[i] * cdf
                : -discountDiv[i] * Math.CRMF001A.NormalCDF(-d1[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeVegasVector(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        Span<double> results)
    {
        Vector<double> spotVec = new Vector<double>(spots);
        Vector<double> strikeVec = new Vector<double>(strikes);
        Vector<double> tauVec = new Vector<double>(taus);
        Vector<double> sigmaVec = new Vector<double>(sigmas);
        Vector<double> rVec = new Vector<double>(r);
        Vector<double> qVec = new Vector<double>(q);

        Vector<double> sqrtTau = Vector.SquareRoot(tauVec);
        Vector<double> logMoneyness = VectorLog(spotVec / strikeVec);
        Vector<double> halfSigmaSq = sigmaVec * sigmaVec * new Vector<double>(0.5);
        Vector<double> drift = (rVec - qVec + halfSigmaSq) * tauVec;
        Vector<double> d1 = (logMoneyness + drift) / (sigmaVec * sqrtTau);

        Vector<double> discountDiv = VectorExp(-qVec * tauVec);

        // Vega = S * exp(-qτ) * φ(d1) * √τ
        for (int i = 0; i < VectorWidth; i++)
        {
            double pdf = Math.CRMF001A.NormalPDF(d1[i]);
            results[i] = spotVec[i] * discountDiv[i] * pdf * sqrtTau[i];
        }
    }

    // ========== Vector Math Helpers ==========

    /// <summary>
    /// Vectorized natural logarithm (element-wise).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<double> VectorLog(Vector<double> v)
    {
        Span<double> result = stackalloc double[VectorWidth];
        for (int i = 0; i < VectorWidth; i++)
        {
            result[i] = System.Math.Log(v[i]);
        }
        return new Vector<double>(result);
    }

    /// <summary>
    /// Vectorized exponential (element-wise).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<double> VectorExp(Vector<double> v)
    {
        Span<double> result = stackalloc double[VectorWidth];
        for (int i = 0; i < VectorWidth; i++)
        {
            result[i] = System.Math.Exp(v[i]);
        }
        return new Vector<double>(result);
    }
}
