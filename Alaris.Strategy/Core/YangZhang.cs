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

        // Take the most recent 'window' bars plus one for calculating opening returns
        List<PriceBar> recentBars = priceBars.TakeLast(window + 1).ToList();

        // Calculate log returns
        (List<double> openReturns, List<double> closeReturns, List<double> rogersReturns) =
            CalculateLogReturns(recentBars, window);

        // Calculate Yang-Zhang variance
        double yangZhangVariance = CalculateYangZhangVariance(openReturns, closeReturns, rogersReturns, window);

        // Standard deviation (volatility)
        double volatility = Math.Sqrt(Math.Max(0, yangZhangVariance));

        // Annualize if requested
        if (annualized)
        {
            volatility *= Math.Sqrt(TradingDaysPerYear);
        }

        return volatility;
    }

    /// <summary>
    /// Calculates open, close, and Rogers-Satchell log returns.
    /// </summary>
    private static (List<double> openReturns, List<double> closeReturns, List<double> rogersReturns)
        CalculateLogReturns(List<PriceBar> recentBars, int window)
    {
        List<double> openReturns = new List<double>(window);
        List<double> closeReturns = new List<double>(window);
        List<double> rogersReturns = new List<double>(window);

        for (int i = 1; i <= window; i++)
        {
            PriceBar current = recentBars[i];
            PriceBar previous = recentBars[i - 1];

            // o_i = ln(Open_i / Close_{i-1})
            double o = Math.Log(current.Open / previous.Close);
            openReturns.Add(o);

            // c_i = ln(Close_i / Open_i)
            double c = Math.Log(current.Close / current.Open);
            closeReturns.Add(c);

            // Rogers-Satchell component: RS_i = u_i(u_i - c_i) + d_i(d_i - c_i)
            double u = Math.Log(current.High / current.Open);
            double d = Math.Log(current.Low / current.Open);
            double rs = (u * (u - c)) + (d * (d - c));
            rogersReturns.Add(rs);
        }

        return (openReturns, closeReturns, rogersReturns);
    }

    /// <summary>
    /// Calculates Yang-Zhang variance from return components.
    /// </summary>
    private static double CalculateYangZhangVariance(List<double> openReturns, List<double> closeReturns, List<double> rogersReturns, int window)
    {
        // Calculate variance components
        double openVariance = Variance(openReturns);
        double closeVariance = Variance(closeReturns);
        double rogersVariance = rogersReturns.Average();

        // Calculate the weight k = 0.34 / (1.34 + (n+1)/(n-1))
        double k = 0.34 / (1.34 + ((window + 1.0) / (window - 1.0)));

        // Yang-Zhang variance: σ_yz² = σ_o² + k·σ_c² + (1-k)·σ_rs²
        return openVariance + (k * closeVariance) + ((1 - k) * rogersVariance);
    }

    /// <summary>
    /// Calculates sample variance using (n-1) denominator.
    /// </summary>
    private static double Variance(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        double mean = values.Average();
        double sumSquaredDeviations = values.Sum(v => Math.Pow(v - mean, 2));
        return sumSquaredDeviations / (values.Count - 1);
    }

    /// <summary>
    /// Calculates a rolling Yang-Zhang volatility series.
    /// </summary>
    public IReadOnlyList<(DateTime Date, double Volatility)> CalculateRolling(
        IReadOnlyList<PriceBar> priceBars,
        int window,
        bool annualized = true)
    {
        ArgumentNullException.ThrowIfNull(priceBars);

        if (priceBars.Count < (window + 1))
        {
            return new List<(DateTime, double)>();
        }

        List<(DateTime Date, double Volatility)> results = new List<(DateTime Date, double Volatility)>();

        for (int i = window; i < priceBars.Count; i++)
        {
            List<PriceBar> windowBars = priceBars.Skip(i - window).Take(window + 1).ToList();
            double volatility = Calculate(windowBars, window, annualized);
            results.Add((priceBars[i].Date, volatility));
        }

        return results;
    }
}