// STPR010A.cs - AVX2-accelerated option chain batch pricing for high-throughput Greeks

using System.Runtime.CompilerServices;
using Alaris.Core.Vectorized;

namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// AVX2-accelerated option chain batch pricing for high-throughput Greeks computation.
/// Component ID: STPR010A
/// </summary>
/// <remarks>
/// <para>
/// Provides end-to-end batch pricing for option chains using SIMD acceleration.
/// Processes entire chains of options for a single underlying in one call.
/// </para>
/// <para>
/// Target use case: Computing Greeks for all strikes/expiries in a chain for delta hedging.
/// </para>
/// </remarks>
public static class STPR010A
{
    /// <summary>
    /// Greeks results for a batch of options.
    /// </summary>
    public readonly struct GreeksResult
    {
        /// <summary>Option price.</summary>
        public required double Price { get; init; }
        /// <summary>Delta (∂V/∂S).</summary>
        public required double Delta { get; init; }
        /// <summary>Gamma (∂²V/∂S²).</summary>
        public required double Gamma { get; init; }
        /// <summary>Vega (∂V/∂σ).</summary>
        public required double Vega { get; init; }
        /// <summary>Theta (∂V/∂t) per day.</summary>
        public required double Theta { get; init; }
    }

    /// <summary>
    /// Computes all Greeks for an option chain using AVX2 batch processing.
    /// </summary>
    /// <param name="spot">Current spot price of underlying.</param>
    /// <param name="strikes">Strike prices for each option.</param>
    /// <param name="taus">Times to expiry (years) for each option.</param>
    /// <param name="sigmas">Implied volatilities for each option.</param>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <param name="isCalls">Call/put flags for each option.</param>
    /// <param name="results">Output array for Greeks results.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeChainGreeks(
        double spot,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        ReadOnlySpan<bool> isCalls,
        Span<GreeksResult> results)
    {
        int count = strikes.Length;
        if (count == 0)
        {
            return;
        }

        // Create spot array for batch processing
        Span<double> spots = stackalloc double[count < 256 ? count : 256];
        if (count > 256)
        {
            spots = new double[count];
        }
        spots[..count].Fill(spot);

        // Allocate intermediate results
        Span<double> prices = stackalloc double[count < 256 ? count : 256];
        Span<double> deltas = stackalloc double[count < 256 ? count : 256];
        Span<double> gammas = stackalloc double[count < 256 ? count : 256];
        Span<double> vegas = stackalloc double[count < 256 ? count : 256];

        if (count > 256)
        {
            prices = new double[count];
            deltas = new double[count];
            gammas = new double[count];
            vegas = new double[count];
        }

        // Use AVX2-accelerated batch Greeks from CRVT001A
        CRVT001A.ComputePricesBatch(spots[..count], strikes, taus, sigmas, r, q, isCalls, prices[..count]);
        CRVT001A.ComputeDeltasBatch(spots[..count], strikes, taus, sigmas, r, q, isCalls, deltas[..count]);
        CRVT001A.ComputeGammasBatch(spots[..count], strikes, taus, sigmas, r, q, gammas[..count]);
        CRVT001A.ComputeVegasBatch(spots[..count], strikes, taus, sigmas, r, q, vegas[..count]);

        // Compute theta as time decay (simplified: -∂V/∂τ per day)
        // θ = -(V(τ) - V(τ-Δτ)) / Δτ  approximated
        const double daysPerYear = 252.0;

        // Assemble results
        for (int i = 0; i < count; i++)
        {
            // Simplified theta: assume theta ≈ -vega * σ / (2 * sqrt(τ)) - other terms
            // For accurate theta, would need full BS theta calculation
            double sqrtTau = System.Math.Sqrt(taus[i]);
            double theta = -(sigmas[i] * vegas[i]) / (2 * sqrtTau * daysPerYear);

            results[i] = new GreeksResult
            {
                Price = prices[i],
                Delta = deltas[i],
                Gamma = gammas[i],
                Vega = vegas[i],
                Theta = theta
            };
        }
    }

    /// <summary>
    /// Computes prices only for an option chain (faster than full Greeks).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeChainPrices(
        double spot,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        ReadOnlySpan<bool> isCalls,
        Span<double> prices)
    {
        int count = strikes.Length;
        if (count == 0)
        {
            return;
        }

        // Create spot array
        Span<double> spots = stackalloc double[count < 256 ? count : 256];
        if (count > 256)
        {
            spots = new double[count];
        }
        spots[..count].Fill(spot);

        CRVT001A.ComputePricesBatch(spots[..count], strikes, taus, sigmas, r, q, isCalls, prices);
    }

    /// <summary>
    /// Computes delta hedge ratios for an option chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeChainDeltas(
        double spot,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        ReadOnlySpan<bool> isCalls,
        Span<double> deltas)
    {
        int count = strikes.Length;
        if (count == 0)
        {
            return;
        }

        Span<double> spots = stackalloc double[count < 256 ? count : 256];
        if (count > 256)
        {
            spots = new double[count];
        }
        spots[..count].Fill(spot);

        CRVT001A.ComputeDeltasBatch(spots[..count], strikes, taus, sigmas, r, q, isCalls, deltas);
    }
}
