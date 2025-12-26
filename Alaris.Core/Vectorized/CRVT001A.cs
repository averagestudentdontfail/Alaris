// CRVT001A.cs - SIMD-vectorized Black-Scholes Greeks computation

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Alaris.Core.Vectorized;

/// <summary>
/// SIMD-vectorized Greeks. Processes batches of Vector&lt;double&gt;.Width with scalar fallback.
/// </summary>
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
    /// Uses AVX2 intrinsics when available for maximum throughput.
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

        // Use AVX2 accelerated path for batches of 4
        if (CRVT002A.IsAvx2Supported && count >= 4)
        {
            int vectorEnd = count - (count % 4);
            for (int i = 0; i < vectorEnd; i += 4)
            {
                ComputePricesVector256(
                    spots.Slice(i, 4),
                    strikes.Slice(i, 4),
                    taus.Slice(i, 4),
                    sigmas.Slice(i, 4),
                    r, q,
                    isCalls.Slice(i, 4),
                    results.Slice(i, 4));
            }

            // Scalar fallback for remainder
            for (int i = vectorEnd; i < count; i++)
            {
                results[i] = Math.CRMF001A.BSPrice(
                    spots[i], strikes[i], taus[i], sigmas[i], r, q, isCalls[i]);
            }
        }
        else
        {
            // Scalar path for non-AVX2 or small batches
            for (int i = 0; i < count; i++)
            {
                results[i] = Math.CRMF001A.BSPrice(
                    spots[i], strikes[i], taus[i], sigmas[i], r, q, isCalls[i]);
            }
        }
    }

    /// <summary>
    /// AVX2-accelerated price computation for 4 options.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputePricesVector256(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        ReadOnlySpan<bool> isCalls,
        Span<double> results)
    {
        // Load data into Vector256
        Vector256<double> S = Vector256.Create(spots[0], spots[1], spots[2], spots[3]);
        Vector256<double> K = Vector256.Create(strikes[0], strikes[1], strikes[2], strikes[3]);
        Vector256<double> tau = Vector256.Create(taus[0], taus[1], taus[2], taus[3]);
        Vector256<double> sigma = Vector256.Create(sigmas[0], sigmas[1], sigmas[2], sigmas[3]);
        Vector256<double> rVec = Vector256.Create(r);
        Vector256<double> qVec = Vector256.Create(q);
        Vector256<double> half = Vector256.Create(0.5);
        Vector256<double> one = Vector256.Create(1.0);
        Vector256<double> negOne = Vector256.Create(-1.0);

        // Compute sqrt(tau)
        Vector256<double> sqrtTau = System.Runtime.Intrinsics.X86.Avx.Sqrt(tau);

        // Compute d1 = (ln(S/K) + (r - q + σ²/2)τ) / (σ√τ)
        Vector256<double> logMoneyness = CRVT002A.VectorLog256(
            System.Runtime.Intrinsics.X86.Avx.Divide(S, K));
        Vector256<double> sigmaSq = System.Runtime.Intrinsics.X86.Avx.Multiply(sigma, sigma);
        Vector256<double> drift = System.Runtime.Intrinsics.X86.Avx.Add(
            System.Runtime.Intrinsics.X86.Avx.Subtract(rVec, qVec),
            System.Runtime.Intrinsics.X86.Avx.Multiply(sigmaSq, half));
        drift = System.Runtime.Intrinsics.X86.Avx.Multiply(drift, tau);
        Vector256<double> sigmaRootT = System.Runtime.Intrinsics.X86.Avx.Multiply(sigma, sqrtTau);
        Vector256<double> d1 = System.Runtime.Intrinsics.X86.Avx.Divide(
            System.Runtime.Intrinsics.X86.Avx.Add(logMoneyness, drift), sigmaRootT);

        // d2 = d1 - σ√τ
        Vector256<double> d2 = System.Runtime.Intrinsics.X86.Avx.Subtract(d1, sigmaRootT);

        // Discount factors
        Vector256<double> discountQ = CRVT002A.VectorExp256(
            System.Runtime.Intrinsics.X86.Avx.Multiply(
                System.Runtime.Intrinsics.X86.Avx.Multiply(qVec, tau), negOne));
        Vector256<double> discountR = CRVT002A.VectorExp256(
            System.Runtime.Intrinsics.X86.Avx.Multiply(
                System.Runtime.Intrinsics.X86.Avx.Multiply(rVec, tau), negOne));

        // N(d1) and N(d2)
        Vector256<double> Nd1 = CRVT002A.VectorNormalCDF256(d1);
        Vector256<double> Nd2 = CRVT002A.VectorNormalCDF256(d2);

        // N(-d1) and N(-d2)
        Vector256<double> Nmd1 = System.Runtime.Intrinsics.X86.Avx.Subtract(one, Nd1);
        Vector256<double> Nmd2 = System.Runtime.Intrinsics.X86.Avx.Subtract(one, Nd2);

        // Call price = S*e^(-qτ)*N(d1) - K*e^(-rτ)*N(d2)
        Vector256<double> term1 = System.Runtime.Intrinsics.X86.Avx.Multiply(
            System.Runtime.Intrinsics.X86.Avx.Multiply(S, discountQ), Nd1);
        Vector256<double> term2 = System.Runtime.Intrinsics.X86.Avx.Multiply(
            System.Runtime.Intrinsics.X86.Avx.Multiply(K, discountR), Nd2);
        Vector256<double> callPrice = System.Runtime.Intrinsics.X86.Avx.Subtract(term1, term2);

        // Put price = K*e^(-rτ)*N(-d2) - S*e^(-qτ)*N(-d1)
        Vector256<double> putTerm1 = System.Runtime.Intrinsics.X86.Avx.Multiply(
            System.Runtime.Intrinsics.X86.Avx.Multiply(K, discountR), Nmd2);
        Vector256<double> putTerm2 = System.Runtime.Intrinsics.X86.Avx.Multiply(
            System.Runtime.Intrinsics.X86.Avx.Multiply(S, discountQ), Nmd1);
        Vector256<double> putPrice = System.Runtime.Intrinsics.X86.Avx.Subtract(putTerm1, putTerm2);

        // Select call or put based on flags (element-wise)
        for (int i = 0; i < 4; i++)
        {
            results[i] = isCalls[i] ? callPrice.GetElement(i) : putPrice.GetElement(i);
        }
    }

    /// <summary>
    /// Computes Black-Scholes deltas for multiple options in vectorized batches.
    /// Uses AVX2 intrinsics when available.
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
        if (count == 0)
        {
            return;
        }

        // Use AVX2 accelerated path for batches of 4
        if (CRVT002A.IsAvx2Supported && count >= 4)
        {
            int vectorEnd = count - (count % 4);
            for (int i = 0; i < vectorEnd; i += 4)
            {
                ComputeDeltasVector256(
                    spots.Slice(i, 4),
                    strikes.Slice(i, 4),
                    taus.Slice(i, 4),
                    sigmas.Slice(i, 4),
                    r, q,
                    isCalls.Slice(i, 4),
                    results.Slice(i, 4));
            }

            // Scalar fallback for remainder
            for (int i = vectorEnd; i < count; i++)
            {
                results[i] = Math.CRMF001A.BSDelta(
                    spots[i], strikes[i], taus[i], sigmas[i], r, q, isCalls[i]);
            }
        }
        else
        {
            // Scalar path for non-AVX2 or small batches
            for (int i = 0; i < count; i++)
            {
                results[i] = Math.CRMF001A.BSDelta(
                    spots[i], strikes[i], taus[i], sigmas[i], r, q, isCalls[i]);
            }
        }
    }

    /// <summary>
    /// AVX2-accelerated delta computation for 4 options.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeDeltasVector256(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        ReadOnlySpan<bool> isCalls,
        Span<double> results)
    {
        Vector256<double> S = Vector256.Create(spots[0], spots[1], spots[2], spots[3]);
        Vector256<double> K = Vector256.Create(strikes[0], strikes[1], strikes[2], strikes[3]);
        Vector256<double> tau = Vector256.Create(taus[0], taus[1], taus[2], taus[3]);
        Vector256<double> sigma = Vector256.Create(sigmas[0], sigmas[1], sigmas[2], sigmas[3]);
        Vector256<double> rVec = Vector256.Create(r);
        Vector256<double> qVec = Vector256.Create(q);
        Vector256<double> half = Vector256.Create(0.5);
        Vector256<double> negOne = Vector256.Create(-1.0);

        // sqrt(tau)
        Vector256<double> sqrtTau = System.Runtime.Intrinsics.X86.Avx.Sqrt(tau);

        // d1 = (ln(S/K) + (r - q + σ²/2)τ) / (σ√τ)
        Vector256<double> logMoneyness = CRVT002A.VectorLog256(
            System.Runtime.Intrinsics.X86.Avx.Divide(S, K));
        Vector256<double> sigmaSq = System.Runtime.Intrinsics.X86.Avx.Multiply(sigma, sigma);
        Vector256<double> drift = System.Runtime.Intrinsics.X86.Avx.Add(
            System.Runtime.Intrinsics.X86.Avx.Subtract(rVec, qVec),
            System.Runtime.Intrinsics.X86.Avx.Multiply(sigmaSq, half));
        drift = System.Runtime.Intrinsics.X86.Avx.Multiply(drift, tau);
        Vector256<double> sigmaRootT = System.Runtime.Intrinsics.X86.Avx.Multiply(sigma, sqrtTau);
        Vector256<double> d1 = System.Runtime.Intrinsics.X86.Avx.Divide(
            System.Runtime.Intrinsics.X86.Avx.Add(logMoneyness, drift), sigmaRootT);

        // exp(-qτ) discount factor
        Vector256<double> discountDiv = CRVT002A.VectorExp256(
            System.Runtime.Intrinsics.X86.Avx.Multiply(
                System.Runtime.Intrinsics.X86.Avx.Multiply(qVec, tau), negOne));

        // N(d1)
        Vector256<double> Nd1 = CRVT002A.VectorNormalCDF256(d1);

        // N(-d1)
        Vector256<double> negD1 = System.Runtime.Intrinsics.X86.Avx.Multiply(d1, negOne);
        Vector256<double> Nmd1 = CRVT002A.VectorNormalCDF256(negD1);

        // Call delta = e^(-qτ) * N(d1)
        // Put delta = -e^(-qτ) * N(-d1)
        Vector256<double> callDelta = System.Runtime.Intrinsics.X86.Avx.Multiply(discountDiv, Nd1);
        Vector256<double> putDelta = System.Runtime.Intrinsics.X86.Avx.Multiply(
            System.Runtime.Intrinsics.X86.Avx.Multiply(discountDiv, Nmd1), negOne);

        for (int i = 0; i < 4; i++)
        {
            results[i] = isCalls[i] ? callDelta.GetElement(i) : putDelta.GetElement(i);
        }
    }

    /// <summary>
    /// Computes Black-Scholes vegas for multiple options in vectorized batches.
    /// Uses AVX2 intrinsics when available.
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
        if (count == 0)
        {
            return;
        }

        // Use AVX2 accelerated path for batches of 4
        if (CRVT002A.IsAvx2Supported && count >= 4)
        {
            int vectorEnd = count - (count % 4);
            for (int i = 0; i < vectorEnd; i += 4)
            {
                ComputeVegasVector256(
                    spots.Slice(i, 4),
                    strikes.Slice(i, 4),
                    taus.Slice(i, 4),
                    sigmas.Slice(i, 4),
                    r, q,
                    results.Slice(i, 4));
            }

            // Scalar fallback for remainder
            for (int i = vectorEnd; i < count; i++)
            {
                results[i] = Math.CRMF001A.BSVega(
                    spots[i], strikes[i], taus[i], sigmas[i], r, q);
            }
        }
        else
        {
            // Scalar path for non-AVX2 or small batches
            for (int i = 0; i < count; i++)
            {
                results[i] = Math.CRMF001A.BSVega(
                    spots[i], strikes[i], taus[i], sigmas[i], r, q);
            }
        }
    }

    /// <summary>
    /// AVX2-accelerated vega computation for 4 options.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeVegasVector256(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        Span<double> results)
    {
        Vector256<double> S = Vector256.Create(spots[0], spots[1], spots[2], spots[3]);
        Vector256<double> K = Vector256.Create(strikes[0], strikes[1], strikes[2], strikes[3]);
        Vector256<double> tau = Vector256.Create(taus[0], taus[1], taus[2], taus[3]);
        Vector256<double> sigma = Vector256.Create(sigmas[0], sigmas[1], sigmas[2], sigmas[3]);
        Vector256<double> rVec = Vector256.Create(r);
        Vector256<double> qVec = Vector256.Create(q);
        Vector256<double> half = Vector256.Create(0.5);
        Vector256<double> negOne = Vector256.Create(-1.0);

        // sqrt(tau)
        Vector256<double> sqrtTau = System.Runtime.Intrinsics.X86.Avx.Sqrt(tau);

        // d1 = (ln(S/K) + (r - q + σ²/2)τ) / (σ√τ)
        Vector256<double> logMoneyness = CRVT002A.VectorLog256(
            System.Runtime.Intrinsics.X86.Avx.Divide(S, K));
        Vector256<double> sigmaSq = System.Runtime.Intrinsics.X86.Avx.Multiply(sigma, sigma);
        Vector256<double> drift = System.Runtime.Intrinsics.X86.Avx.Add(
            System.Runtime.Intrinsics.X86.Avx.Subtract(rVec, qVec),
            System.Runtime.Intrinsics.X86.Avx.Multiply(sigmaSq, half));
        drift = System.Runtime.Intrinsics.X86.Avx.Multiply(drift, tau);
        Vector256<double> sigmaRootT = System.Runtime.Intrinsics.X86.Avx.Multiply(sigma, sqrtTau);
        Vector256<double> d1 = System.Runtime.Intrinsics.X86.Avx.Divide(
            System.Runtime.Intrinsics.X86.Avx.Add(logMoneyness, drift), sigmaRootT);

        // exp(-qτ) discount factor
        Vector256<double> discountDiv = CRVT002A.VectorExp256(
            System.Runtime.Intrinsics.X86.Avx.Multiply(
                System.Runtime.Intrinsics.X86.Avx.Multiply(qVec, tau), negOne));

        // φ(d1) - standard normal PDF
        Vector256<double> pdf = CRVT002A.VectorNormalPDF256(d1);

        // Vega = S * e^(-qτ) * φ(d1) * √τ
        Vector256<double> vega = System.Runtime.Intrinsics.X86.Avx.Multiply(
            System.Runtime.Intrinsics.X86.Avx.Multiply(
                System.Runtime.Intrinsics.X86.Avx.Multiply(S, discountDiv), pdf), sqrtTau);

        // Store results
        results[0] = vega.GetElement(0);
        results[1] = vega.GetElement(1);
        results[2] = vega.GetElement(2);
        results[3] = vega.GetElement(3);
    }

    /// <summary>
    /// Computes Black-Scholes gammas for multiple options in vectorized batches.
    /// Uses AVX2 intrinsics when available.
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
        if (count == 0)
        {
            return;
        }

        // Use AVX2 accelerated path for batches of 4
        if (CRVT002A.IsAvx2Supported && count >= 4)
        {
            int vectorEnd = count - (count % 4);
            for (int i = 0; i < vectorEnd; i += 4)
            {
                ComputeGammasVector256(
                    spots.Slice(i, 4),
                    strikes.Slice(i, 4),
                    taus.Slice(i, 4),
                    sigmas.Slice(i, 4),
                    r, q,
                    results.Slice(i, 4));
            }

            // Scalar fallback for remainder
            for (int i = vectorEnd; i < count; i++)
            {
                results[i] = Math.CRMF001A.BSGamma(
                    spots[i], strikes[i], taus[i], sigmas[i], r, q);
            }
        }
        else
        {
            // Scalar path
            for (int i = 0; i < count; i++)
            {
                results[i] = Math.CRMF001A.BSGamma(
                    spots[i], strikes[i], taus[i], sigmas[i], r, q);
            }
        }
    }

    /// <summary>
    /// AVX2-accelerated gamma computation for 4 options.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeGammasVector256(
        ReadOnlySpan<double> spots,
        ReadOnlySpan<double> strikes,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> sigmas,
        double r,
        double q,
        Span<double> results)
    {
        Vector256<double> S = Vector256.Create(spots[0], spots[1], spots[2], spots[3]);
        Vector256<double> K = Vector256.Create(strikes[0], strikes[1], strikes[2], strikes[3]);
        Vector256<double> tau = Vector256.Create(taus[0], taus[1], taus[2], taus[3]);
        Vector256<double> sigma = Vector256.Create(sigmas[0], sigmas[1], sigmas[2], sigmas[3]);
        Vector256<double> rVec = Vector256.Create(r);
        Vector256<double> qVec = Vector256.Create(q);
        Vector256<double> half = Vector256.Create(0.5);
        Vector256<double> negOne = Vector256.Create(-1.0);

        // sqrt(tau)
        Vector256<double> sqrtTau = System.Runtime.Intrinsics.X86.Avx.Sqrt(tau);

        // d1 = (ln(S/K) + (r - q + σ²/2)τ) / (σ√τ)
        Vector256<double> logMoneyness = CRVT002A.VectorLog256(
            System.Runtime.Intrinsics.X86.Avx.Divide(S, K));
        Vector256<double> sigmaSq = System.Runtime.Intrinsics.X86.Avx.Multiply(sigma, sigma);
        Vector256<double> drift = System.Runtime.Intrinsics.X86.Avx.Add(
            System.Runtime.Intrinsics.X86.Avx.Subtract(rVec, qVec),
            System.Runtime.Intrinsics.X86.Avx.Multiply(sigmaSq, half));
        drift = System.Runtime.Intrinsics.X86.Avx.Multiply(drift, tau);
        Vector256<double> sigmaRootT = System.Runtime.Intrinsics.X86.Avx.Multiply(sigma, sqrtTau);
        Vector256<double> d1 = System.Runtime.Intrinsics.X86.Avx.Divide(
            System.Runtime.Intrinsics.X86.Avx.Add(logMoneyness, drift), sigmaRootT);

        // exp(-qτ) discount factor
        Vector256<double> discountDiv = CRVT002A.VectorExp256(
            System.Runtime.Intrinsics.X86.Avx.Multiply(
                System.Runtime.Intrinsics.X86.Avx.Multiply(qVec, tau), negOne));

        // φ(d1) - standard normal PDF
        Vector256<double> pdf = CRVT002A.VectorNormalPDF256(d1);

        // Gamma = e^(-qτ) * φ(d1) / (S * σ * √τ)
        Vector256<double> denom = System.Runtime.Intrinsics.X86.Avx.Multiply(
            System.Runtime.Intrinsics.X86.Avx.Multiply(S, sigma), sqrtTau);
        Vector256<double> gamma = System.Runtime.Intrinsics.X86.Avx.Divide(
            System.Runtime.Intrinsics.X86.Avx.Multiply(discountDiv, pdf), denom);

        // Store results
        results[0] = gamma.GetElement(0);
        results[1] = gamma.GetElement(1);
        results[2] = gamma.GetElement(2);
        results[3] = gamma.GetElement(3);
    }

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

    /// <summary>
    /// Vectorized natural logarithm (element-wise).
    /// Uses AVX2 intrinsics when available, otherwise falls back to scalar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<double> VectorLog(Vector<double> v)
    {
        // Use AVX2 intrinsics if available and vector width matches
        if (CRVT002A.IsAvx2Supported && VectorWidth == 4)
        {
            // Convert Vector<double> to Vector256<double>
            System.Runtime.Intrinsics.Vector256<double> v256 =
                System.Runtime.Intrinsics.Vector256.Create(v[0], v[1], v[2], v[3]);
            System.Runtime.Intrinsics.Vector256<double> result256 = CRVT002A.VectorLog256(v256);

            // Extract results back to Vector<double>
            Span<double> buffer = stackalloc double[4];
            buffer[0] = result256.GetElement(0);
            buffer[1] = result256.GetElement(1);
            buffer[2] = result256.GetElement(2);
            buffer[3] = result256.GetElement(3);
            return new Vector<double>(buffer);
        }

        // Scalar fallback
        Span<double> result = stackalloc double[VectorWidth];
        for (int i = 0; i < VectorWidth; i++)
        {
            result[i] = System.Math.Log(v[i]);
        }
        return new Vector<double>(result);
    }

    /// <summary>
    /// Vectorized exponential (element-wise).
    /// Uses AVX2 intrinsics when available, otherwise falls back to scalar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<double> VectorExp(Vector<double> v)
    {
        // Use AVX2 intrinsics if available and vector width matches
        if (CRVT002A.IsAvx2Supported && VectorWidth == 4)
        {
            // Convert Vector<double> to Vector256<double>
            System.Runtime.Intrinsics.Vector256<double> v256 =
                System.Runtime.Intrinsics.Vector256.Create(v[0], v[1], v[2], v[3]);
            System.Runtime.Intrinsics.Vector256<double> result256 = CRVT002A.VectorExp256(v256);

            // Extract results back to Vector<double>
            Span<double> buffer = stackalloc double[4];
            buffer[0] = result256.GetElement(0);
            buffer[1] = result256.GetElement(1);
            buffer[2] = result256.GetElement(2);
            buffer[3] = result256.GetElement(3);
            return new Vector<double>(buffer);
        }

        // Scalar fallback
        Span<double> result = stackalloc double[VectorWidth];
        for (int i = 0; i < VectorWidth; i++)
        {
            result[i] = System.Math.Exp(v[i]);
        }
        return new Vector<double>(result);
    }
}
