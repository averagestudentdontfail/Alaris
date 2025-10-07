using Alaris.Strategy.Bridge;
using MathNet.Numerics.Statistics;

namespace Alaris.Strategy.Core;

/// <summary>
/// Implements the Yang-Zhang (2000) realized volatility estimator.
/// This estimator is an efficient method that uses OHLC data and accounts for opening jumps.
/// Formula: RV² = σ_o² + k·σ_c² + (1-k)·σ_rs²
/// </summary>
public sealed class YangZhangEstimator
{
    private const int TradingDaysPerYear = 252;

    /// <summary>
    /// Calculates the Yang-Zhang realized volatility estimate.
    /// </summary>
    /// <param name="priceBars">Historical OHLC price data.</param>
    /// <param name="window">The rolling window size (typically 30 days).</param>
    /// <param name="annualized">Whether to return annualized volatility.</param>
    /// <returns>The Yang-Zhang volatility estimate.</returns>
    public double Calculate(List<PriceBar> priceBars, int window, bool annualized = true)
    {
        ArgumentNullException.ThrowIfNull(priceBars);

        if (priceBars.Count < window + 1)
            throw new ArgumentException($"Need at least {window + 1} price bars", nameof(priceBars));

        if (window < 2)
            throw new ArgumentException("Window must be at least 2", nameof(window));

        // Take the most recent 'window' bars plus one for calculating opening returns
        var recentBars = priceBars.TakeLast(window + 1).ToList();

        // Calculate normalized log returns
        var n = window;
        var openReturns = new List<double>(n);
        var closeReturns = new List<double>(n);
        var rogersReturns = new List<double>(n);

        for (int i = 1; i <= n; i++)
        {
            var current = recentBars[i];
            var previous = recentBars[i - 1];

            // o_i = ln(Open_i / Close_{i-1})
            var o = Math.Log(current.Open / previous.Close);
            openReturns.Add(o);

            // c_i = ln(Close_i / Open_i)
            var c = Math.Log(current.Close / current.Open);
            closeReturns.Add(c);

            // Rogers-Satchell component for intraday volatility
            // u_i = ln(High_i / Open_i)
            var u = Math.Log(current.High / current.Open);
            
            // d_i = ln(Low_i / Open_i)
            var d = Math.Log(current.Low / current.Open);

            // RS_i = u_i(u_i - c_i) + d_i(d_i - c_i)
            var rs = u * (u - c) + d * (d - c);
            rogersReturns.Add(rs);
        }

        // Calculate variance components
        // σ_o² = (1/(n-1)) * Σ(o_i - ō)²
        var openVariance = Variance(openReturns);

        // σ_c² = (1/(n-1)) * Σ(c_i - c̄)²
        var closeVariance = Variance(closeReturns);

        // σ_rs² = (1/n) * Σ(RS_i)
        var rogersVariance = rogersReturns.Average();

        // Calculate the weight k
        // k = 0.34 / (1.34 + (n+1)/(n-1))
        var k = 0.34 / (1.34 + (n + 1.0) / (n - 1.0));

        // Yang-Zhang variance estimator
        // σ_yz² = σ_o² + k·σ_c² + (1-k)·σ_rs²
        var yangZhangVariance = openVariance + k * closeVariance + (1 - k) * rogersVariance;

        // Standard deviation (volatility)
        var volatility = Math.Sqrt(Math.Max(0, yangZhangVariance));

        // Annualize if requested
        if (annualized)
        {
            volatility *= Math.Sqrt(TradingDaysPerYear);
        }

        return volatility;
    }

    /// <summary>
    /// Calculates sample variance using (n-1) denominator.
    /// </summary>
    private static double Variance(List<double> values)
    {
        if (values.Count < 2)
            return 0;

        var mean = values.Average();
        var sumSquaredDeviations = values.Sum(v => Math.Pow(v - mean, 2));
        return sumSquaredDeviations / (values.Count - 1);
    }

    /// <summary>
    /// Calculates a rolling Yang-Zhang volatility series.
    /// </summary>
    public List<(DateTime Date, double Volatility)> CalculateRolling(
        List<PriceBar> priceBars, 
        int window,
        bool annualized = true)
    {
        ArgumentNullException.ThrowIfNull(priceBars);

        if (priceBars.Count < window + 1)
            return new List<(DateTime, double)>();

        var results = new List<(DateTime Date, double Volatility)>();

        for (int i = window; i < priceBars.Count; i++)
        {
            var windowBars = priceBars.Skip(i - window).Take(window + 1).ToList();
            var volatility = Calculate(windowBars, window, annualized);
            results.Add((priceBars[i].Date, volatility));
        }

        return results;
    }
}