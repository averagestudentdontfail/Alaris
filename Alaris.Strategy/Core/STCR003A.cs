using System.Buffers;
using Alaris.Strategy.Bridge;
using MathNet.Numerics.Statistics;

namespace Alaris.Strategy.Core;

/// <summary>
/// Implements the Yang-Zhang (2000) realized volatility estimator.
/// This estimator is an efficient method that uses OHLC data and accounts for opening jumps.
/// Formula: RV² = σ_o² + k·σ_c² + (1-k)·σ_rs²
/// </summary>
/// <remarks>
/// Optimized for Rule 5 (Zero-Allocation Hot Paths) using ArrayPool and Span&lt;T&gt;.
/// </remarks>
public sealed class STCR003AEstimator
{
    private const int TradingDaysPerYear = 252;

    /// <summary>
    /// Calculates the Yang-Zhang realized volatility estimate.
    /// </summary>
    /// <param name="priceBars">Historical OHLC price data.</param>
    /// <param name="window">The rolling window size (typically 30 days).</param>
    /// <param name="annualized">Whether to return annualized volatility.</param>
    /// <returns>The Yang-Zhang volatility estimate.</returns>
    public double Calculate(IReadOnlyList<PriceBar> priceBars, int window, bool annualized = true)
    {
        ArgumentNullException.ThrowIfNull(priceBars);

        if (priceBars.Count < (window + 1))
        {
            throw new ArgumentException($"Need at least {window + 1} price bars", nameof(priceBars));
        }

        if (window < 2)
        {
            throw new ArgumentException("Window must be at least 2", nameof(window));
        }

        // Rule 5: Use ArrayPool for returns arrays to avoid heap allocations
        double[] openReturns = ArrayPool<double>.Shared.Rent(window);
        double[] closeReturns = ArrayPool<double>.Shared.Rent(window);
        double[] rogersReturns = ArrayPool<double>.Shared.Rent(window);

        try
        {
            // Calculate returns directly from the last (window + 1) bars
            int startIdx = priceBars.Count - window - 1;
            CalculateLogReturnsInPlace(priceBars, startIdx, window, openReturns, closeReturns, rogersReturns);

            // Calculate Yang-Zhang variance using spans
            double yangZhangVariance = CalculateSTCR003AVarianceFromSpan(
                openReturns.AsSpan(0, window),
                closeReturns.AsSpan(0, window),
                rogersReturns.AsSpan(0, window),
                window);

            // Standard deviation (volatility)
            double volatility = Math.Sqrt(Math.Max(0, yangZhangVariance));

            // Annualize if requested
            if (annualized)
            {
                volatility *= Math.Sqrt(TradingDaysPerYear);
            }

            return volatility;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(openReturns);
            ArrayPool<double>.Shared.Return(closeReturns);
            ArrayPool<double>.Shared.Return(rogersReturns);
        }
    }

    /// <summary>
    /// Calculates log returns directly into provided arrays (zero allocation).
    /// </summary>
    private static void CalculateLogReturnsInPlace(
        IReadOnlyList<PriceBar> priceBars,
        int startIdx,
        int window,
        double[] openReturns,
        double[] closeReturns,
        double[] rogersReturns)
    {
        for (int i = 0; i < window; i++)
        {
            PriceBar current = priceBars[startIdx + i + 1];
            PriceBar previous = priceBars[startIdx + i];

            // o_i = ln(Open_i / Close_{i-1})
            openReturns[i] = Math.Log(current.Open / previous.Close);

            // c_i = ln(Close_i / Open_i)
            double c = Math.Log(current.Close / current.Open);
            closeReturns[i] = c;

            // Rogers-Satchell component: RS_i = u_i(u_i - c_i) + d_i(d_i - c_i)
            double u = Math.Log(current.High / current.Open);
            double d = Math.Log(current.Low / current.Open);
            rogersReturns[i] = (u * (u - c)) + (d * (d - c));
        }
    }

    /// <summary>
    /// Calculates Yang-Zhang variance from Span return components (zero allocation).
    /// </summary>
    private static double CalculateSTCR003AVarianceFromSpan(
        ReadOnlySpan<double> openReturns,
        ReadOnlySpan<double> closeReturns,
        ReadOnlySpan<double> rogersReturns,
        int window)
    {
        // Calculate variance components using span-based operations
        double openVariance = VarianceFromSpan(openReturns);
        double closeVariance = VarianceFromSpan(closeReturns);
        double rogersVariance = AverageFromSpan(rogersReturns);

        // Calculate the weight k = 0.34 / (1.34 + (n+1)/(n-1))
        double k = 0.34 / (1.34 + ((window + 1.0) / (window - 1.0)));

        // Yang-Zhang variance: σ_yz² = σ_o² + k·σ_c² + (1-k)·σ_rs²
        return openVariance + (k * closeVariance) + ((1 - k) * rogersVariance);
    }

    /// <summary>
    /// Calculates sample variance using (n-1) denominator from Span.
    /// </summary>
    private static double VarianceFromSpan(ReadOnlySpan<double> values)
    {
        if (values.Length < 2)
        {
            return 0;
        }

        double mean = AverageFromSpan(values);
        double sumSquaredDeviations = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double deviation = values[i] - mean;
            sumSquaredDeviations += deviation * deviation;
        }
        return sumSquaredDeviations / (values.Length - 1);
    }

    /// <summary>
    /// Calculates average from Span (zero allocation).
    /// </summary>
    private static double AverageFromSpan(ReadOnlySpan<double> values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }
        return sum / values.Length;
    }

    /// <summary>
    /// Calculates a rolling Yang-Zhang volatility series.
    /// Optimized to reuse buffers across rolling window calculations.
    /// </summary>
    public IReadOnlyList<(DateTime Date, double Volatility)> CalculateRolling(
        IReadOnlyList<PriceBar> priceBars,
        int window,
        bool annualized = true)
    {
        ArgumentNullException.ThrowIfNull(priceBars);

        if (priceBars.Count < (window + 1))
        {
            return Array.Empty<(DateTime, double)>();
        }

        int resultCount = priceBars.Count - window;
        List<(DateTime Date, double Volatility)> results = new(resultCount);

        // Rule 5: Rent buffers once and reuse for entire rolling calculation
        double[] openReturns = ArrayPool<double>.Shared.Rent(window);
        double[] closeReturns = ArrayPool<double>.Shared.Rent(window);
        double[] rogersReturns = ArrayPool<double>.Shared.Rent(window);

        try
        {
            double annualizationFactor = annualized ? Math.Sqrt(TradingDaysPerYear) : 1.0;

            for (int i = window; i < priceBars.Count; i++)
            {
                int startIdx = i - window;

                // Calculate returns directly into pooled arrays
                CalculateLogReturnsInPlace(priceBars, startIdx, window, openReturns, closeReturns, rogersReturns);

                // Calculate variance from spans
                double yangZhangVariance = CalculateSTCR003AVarianceFromSpan(
                    openReturns.AsSpan(0, window),
                    closeReturns.AsSpan(0, window),
                    rogersReturns.AsSpan(0, window),
                    window);

                double volatility = Math.Sqrt(Math.Max(0, yangZhangVariance)) * annualizationFactor;
                results.Add((priceBars[i].Date, volatility));
            }

            return results;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(openReturns);
            ArrayPool<double>.Shared.Return(closeReturns);
            ArrayPool<double>.Shared.Return(rogersReturns);
        }
    }
}