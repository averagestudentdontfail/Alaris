using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Calibrates earnings jump volatility (sigma_e) from historical earnings announcement data.
///
/// The earnings jump volatility represents the standard deviation of log-returns on earnings
/// announcement dates, as specified in Leung &amp; Santoli (2014). This parameter is crucial for:
///   1. Computing theoretical pre-EA implied volatility
///   2. Estimating expected IV crush magnitude
///   3. Identifying mispricing in market IV vs theoretical IV
///
/// Calibration approach:
///   sigma_e = sqrt(Var(Z_e)) where Z_e is the log-return on EA date
///   Empirically: sigma_e = StdDev(log(S_t+1 / S_t)) for historical EA dates
///
/// Reference: "Accounting for Earnings Announcements in the Pricing of Equity Options"
/// Leung &amp; Santoli (2014), Section 5.2 - Analytic Estimators under the Extended BS Model
/// </summary>
public sealed class EarningsJumpCalibrator
{
    private readonly ILogger<EarningsJumpCalibrator>? _logger;

    /// <summary>
    /// Minimum number of historical earnings to compute reliable sigma_e.
    /// </summary>
    private const int MinHistoricalSamples = 4; // At least 4 quarters (1 year)

    /// <summary>
    /// Default lookback period for historical earnings (quarters).
    /// </summary>
    private const int DefaultLookbackQuarters = 12; // 3 years of quarterly earnings

    /// <summary>
    /// Maximum reasonable sigma_e (100% daily move).
    /// </summary>
    private const double MaxSigmaE = 1.0;

    /// <summary>
    /// Minimum sigma_e to return (avoid zero division issues).
    /// </summary>
    private const double MinSigmaE = 0.001;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, int, Exception?> LogCalibrating =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogCalibrating)),
            "Calibrating sigma_e for {Symbol} using {SampleCount} historical earnings");

    private static readonly Action<ILogger, string, double, Exception?> LogCalibrationResult =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(2, nameof(LogCalibrationResult)),
            "Calibrated sigma_e for {Symbol}: {SigmaE:P2}");

    private static readonly Action<ILogger, string, Exception?> LogInsufficientData =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(LogInsufficientData)),
            "Insufficient historical earnings data for {Symbol}");

    public EarningsJumpCalibrator(ILogger<EarningsJumpCalibrator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calibrates earnings jump volatility from historical earnings moves.
    /// </summary>
    /// <param name="historicalEarningsMoves">Array of historical earnings log-returns.</param>
    /// <returns>Calibrated sigma_e, or null if insufficient data.</returns>
    public double? CalibrateFromMoves(double[] historicalEarningsMoves)
    {
        if (historicalEarningsMoves == null || historicalEarningsMoves.Length < MinHistoricalSamples)
        {
            return null;
        }

        // Compute standard deviation of log-returns
        double mean = historicalEarningsMoves.Average();
        double sumSquaredDeviations = historicalEarningsMoves.Sum(x => (x - mean) * (x - mean));
        double variance = sumSquaredDeviations / (historicalEarningsMoves.Length - 1); // Sample variance
        double sigmaE = Math.Sqrt(variance);

        // Clamp to reasonable range
        return Math.Clamp(sigmaE, MinSigmaE, MaxSigmaE);
    }

    /// <summary>
    /// Calibrates earnings jump volatility from historical price data and earnings dates.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <param name="historicalPrices">Historical daily price bars (sorted chronologically).</param>
    /// <param name="earningsDates">Historical earnings announcement dates.</param>
    /// <returns>Calibration result with sigma_e and statistics.</returns>
    public EarningsJumpCalibration Calibrate(
        string symbol,
        IReadOnlyList<PriceBar> historicalPrices,
        IReadOnlyList<DateTime> earningsDates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(historicalPrices);
        ArgumentNullException.ThrowIfNull(earningsDates);

        // Build a dictionary for fast date lookup
        Dictionary<DateTime, PriceBar> priceByDate = historicalPrices
            .GroupBy(p => p.Date.Date)
            .ToDictionary(g => g.Key, g => g.First());

        // Calculate log-returns for each earnings date
        List<double> earningsLogReturns = new List<double>();
        List<double> absoluteMoves = new List<double>();

        foreach (DateTime earningsDate in earningsDates.OrderByDescending(d => d))
        {
            // Find the price bar for earnings date and the day before
            if (!priceByDate.TryGetValue(earningsDate.Date, out PriceBar? earningsBar))
            {
                continue;
            }

            // Look for previous trading day (up to 5 days back to handle weekends/holidays)
            PriceBar? prevBar = null;
            for (int i = 1; i <= 5; i++)
            {
                DateTime prevDate = earningsDate.AddDays(-i).Date;
                if (priceByDate.TryGetValue(prevDate, out prevBar))
                {
                    break;
                }
            }

            if (prevBar == null || prevBar.Close <= 0 || earningsBar.Close <= 0)
            {
                continue;
            }

            // Calculate log-return: Z_e = log(S_t+1 / S_t)
            double logReturn = Math.Log(earningsBar.Close / prevBar.Close);
            earningsLogReturns.Add(logReturn);
            absoluteMoves.Add(Math.Abs(logReturn));

            // Limit to lookback period
            if (earningsLogReturns.Count >= DefaultLookbackQuarters)
            {
                break;
            }
        }

        SafeLog(() => LogCalibrating(_logger!, symbol, earningsLogReturns.Count, null));

        if (earningsLogReturns.Count < MinHistoricalSamples)
        {
            SafeLog(() => LogInsufficientData(_logger!, symbol, null));
            return new EarningsJumpCalibration
            {
                Symbol = symbol,
                SigmaE = null,
                SampleCount = earningsLogReturns.Count,
                IsValid = false
            };
        }

        // Compute sigma_e as standard deviation of log-returns
        double mean = earningsLogReturns.Average();
        double sumSquaredDeviations = earningsLogReturns.Sum(x => (x - mean) * (x - mean));
        double variance = sumSquaredDeviations / (earningsLogReturns.Count - 1); // Sample variance
        double sigmaE = Math.Sqrt(variance);

        // Clamp to reasonable range
        sigmaE = Math.Clamp(sigmaE, MinSigmaE, MaxSigmaE);

        SafeLog(() => LogCalibrationResult(_logger!, symbol, sigmaE, null));

        return new EarningsJumpCalibration
        {
            Symbol = symbol,
            SigmaE = sigmaE,
            SampleCount = earningsLogReturns.Count,
            MeanLogReturn = mean,
            MedianAbsoluteMove = ComputeMedian(absoluteMoves),
            MaxAbsoluteMove = absoluteMoves.Max(),
            MinAbsoluteMove = absoluteMoves.Min(),
            IsValid = true,
            HistoricalMoves = earningsLogReturns.ToArray()
        };
    }

    /// <summary>
    /// Computes the term structure estimator from two IV observations.
    /// From Leung &amp; Santoli (2014) Section 5.2, Equation 5.2:
    ///     sigma_e^TS = sqrt((IV(T1)^2 - IV(T2)^2) / (1/(T1-t) - 1/(T2-t)))
    /// </summary>
    /// <param name="iv1">Implied volatility for first maturity.</param>
    /// <param name="dte1">Days to expiry for first maturity.</param>
    /// <param name="iv2">Implied volatility for second maturity (dte2 > dte1).</param>
    /// <param name="dte2">Days to expiry for second maturity.</param>
    /// <returns>Term structure estimator of sigma_e, or null if invalid.</returns>
    public static double? TermStructureEstimator(double iv1, int dte1, double iv2, int dte2)
    {
        // Ensure dte1 < dte2 for proper term structure
        if (dte1 >= dte2 || dte1 <= 0 || dte2 <= 0)
        {
            return null;
        }

        // IV must decrease with maturity for meaningful estimate
        if (iv1 <= iv2)
        {
            return null;
        }

        double t1 = dte1 / 252.0; // Convert to years
        double t2 = dte2 / 252.0;

        // From equation 5.2: sigma_e^2 = (IV(T1)^2 - IV(T2)^2) / (1/(T1-t) - 1/(T2-t))
        double ivSquaredDiff = (iv1 * iv1) - (iv2 * iv2);
        double inverseTauDiff = (1.0 / t1) - (1.0 / t2);

        if (inverseTauDiff <= 0)
        {
            return null;
        }

        double sigmaESquared = ivSquaredDiff / inverseTauDiff;
        if (sigmaESquared <= 0)
        {
            return null;
        }

        double sigmaE = Math.Sqrt(sigmaESquared);
        return Math.Clamp(sigmaE, MinSigmaE, MaxSigmaE);
    }

    /// <summary>
    /// Computes the base volatility (sigma) from term structure observations.
    /// From Leung &amp; Santoli (2014) Section 5.2:
    ///     sigma^TS = sqrt(((T1-t)*IV(T1)^2 - (T2-t)*IV(T2)^2) / (T1 - T2))
    /// </summary>
    /// <param name="iv1">Implied volatility for first maturity.</param>
    /// <param name="dte1">Days to expiry for first maturity.</param>
    /// <param name="iv2">Implied volatility for second maturity.</param>
    /// <param name="dte2">Days to expiry for second maturity.</param>
    /// <returns>Term structure estimator of base volatility sigma, or null if invalid.</returns>
    public static double? BaseVolatilityEstimator(double iv1, int dte1, double iv2, int dte2)
    {
        if (dte1 <= 0 || dte2 <= 0 || dte1 == dte2)
        {
            return null;
        }

        double t1 = dte1 / 252.0;
        double t2 = dte2 / 252.0;

        // sigma^2 = ((T1-t)*IV(T1)^2 - (T2-t)*IV(T2)^2) / (T1 - T2)
        double numerator = (t1 * iv1 * iv1) - (t2 * iv2 * iv2);
        double denominator = t1 - t2;

        double sigmaSquared = numerator / denominator;
        if (sigmaSquared <= 0)
        {
            return null;
        }

        return Math.Sqrt(sigmaSquared);
    }

    /// <summary>
    /// Computes the median of a list of values.
    /// </summary>
    private static double ComputeMedian(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        List<double> sorted = values.OrderBy(x => x).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        return sorted[mid];
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation.
    /// </summary>
    private void SafeLog(Action logAction)
    {
        if (_logger == null)
        {
            return;
        }

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Swallow logging exceptions - fault isolation
        }
#pragma warning restore CA1031
    }
}

/// <summary>
/// Results of earnings jump volatility calibration.
/// </summary>
public sealed class EarningsJumpCalibration
{
    /// <summary>
    /// Gets or sets the security symbol.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the calibrated earnings jump volatility (sigma_e).
    /// Null if calibration failed due to insufficient data.
    /// </summary>
    public double? SigmaE { get; set; }

    /// <summary>
    /// Gets or sets the number of historical earnings samples used.
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Gets or sets the mean log-return on earnings dates.
    /// </summary>
    public double MeanLogReturn { get; set; }

    /// <summary>
    /// Gets or sets the median absolute move on earnings dates.
    /// </summary>
    public double MedianAbsoluteMove { get; set; }

    /// <summary>
    /// Gets or sets the maximum absolute move observed.
    /// </summary>
    public double MaxAbsoluteMove { get; set; }

    /// <summary>
    /// Gets or sets the minimum absolute move observed.
    /// </summary>
    public double MinAbsoluteMove { get; set; }

    /// <summary>
    /// Gets or sets whether the calibration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the historical log-returns.
    /// </summary>
    public IReadOnlyList<double> HistoricalMoves { get; set; } = Array.Empty<double>();
}
