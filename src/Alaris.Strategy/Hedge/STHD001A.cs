// STHD001A.cs - vega correlation analyser

using MathNet.Numerics.Statistics;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Hedge;

/// <summary>
/// Analyses the correlation between front-month and back-month implied
/// volatility changes to assess calendar spread vega independence.
/// </summary>

public sealed class STHD001A
{
    private readonly ILogger<STHD001A>? _logger;
    private readonly double _maxAcceptableCorrelation;
    private readonly int _minimumObservations;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, double, string, Exception?> LogCorrelationResult =
        LoggerMessage.Define<string, double, string>(
            LogLevel.Information,
            new EventId(1, nameof(LogCorrelationResult)),
            "Vega correlation for {Symbol}: {Correlation:F4} - {Interpretation}");

    private static readonly Action<ILogger, string, int, int, Exception?> LogInsufficientData =
        LoggerMessage.Define<string, int, int>(
            LogLevel.Warning,
            new EventId(2, nameof(LogInsufficientData)),
            "Insufficient IV history for {Symbol}: {Actual} observations, required {Required}");

    /// <summary>
    /// Default maximum acceptable correlation coefficient.
    /// </summary>
    
    public const double DefaultMaxCorrelation = 0.70;

    /// <summary>
    /// Default minimum number of observations required.
    /// </summary>
    
    public const int DefaultMinimumObservations = 20;

    /// <summary>
    /// Initialises a new instance of the vega correlation analyser.
    /// </summary>
    /// <param name="maxAcceptableCorrelation">
    /// Maximum acceptable correlation. Default: 0.70.
    /// </param>
    /// <param name="minimumObservations">
    /// Minimum observations required. Default: 20.
    /// </param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when correlation threshold is not in [-1, 1] or observations &lt; 3.
    /// </exception>
    public STHD001A(
        double maxAcceptableCorrelation = DefaultMaxCorrelation,
        int minimumObservations = DefaultMinimumObservations,
        ILogger<STHD001A>? logger = null)
    {
        if (maxAcceptableCorrelation < -1.0 || maxAcceptableCorrelation > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAcceptableCorrelation),
                maxAcceptableCorrelation,
                "Correlation threshold must be in range [-1, 1].");
        }

        if (minimumObservations < 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumObservations),
                minimumObservations,
                "Minimum observations must be at least 3 for correlation computation.");
        }

        _maxAcceptableCorrelation = maxAcceptableCorrelation;
        _minimumObservations = minimumObservations;
        _logger = logger;
    }

    /// <summary>
    /// Gets a VIX-conditional correlation threshold for regime-dependent filtering.
    /// </summary>
    
    /// <param name="currentVIX">Current VIX level.</param>
    /// <returns>Conditional correlation threshold.</returns>
    public static double GetConditionalCorrelationThreshold(double currentVIX)
    {
        const double BaseThreshold = 0.70;
        const double HighVolThreshold = 0.85;
        const double VixBreakpoint = 25.0;

        return currentVIX > VixBreakpoint ? HighVolThreshold : BaseThreshold;
    }

    /// <summary>
    /// Analyses the correlation between front-month and back-month IV changes.
    /// </summary>
    /// <param name="symbol">The underlying symbol.</param>
    /// <param name="frontIVChanges">Daily changes in front-month IV.</param>
    /// <param name="backIVChanges">Daily changes in back-month IV.</param>
    /// <returns>Vega correlation analysis result.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either IV change array is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when arrays have different lengths.
    /// </exception>
    public STHD002A Analyse(
        string symbol,
        IReadOnlyList<double> frontIVChanges,
        IReadOnlyList<double> backIVChanges)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(frontIVChanges);
        ArgumentNullException.ThrowIfNull(backIVChanges);

        if (frontIVChanges.Count != backIVChanges.Count)
        {
            throw new ArgumentException(
                $"IV change series must have equal length. Front: {frontIVChanges.Count}, Back: {backIVChanges.Count}",
                nameof(backIVChanges));
        }

        int observations = frontIVChanges.Count;

        // Check minimum observations
        if (observations < _minimumObservations)
        {
            SafeLog(() => LogInsufficientData(
                _logger!,
                symbol,
                observations,
                _minimumObservations,
                null));

            return new STHD002A
            {
                Symbol = symbol,
                Correlation = double.NaN,
                Threshold = _maxAcceptableCorrelation,
                Observations = observations,
                MinimumObservations = _minimumObservations,
                PassesFilter = false,
                HasSufficientData = false,
                Interpretation = "Insufficient data for correlation analysis."
            };
        }

        // Compute Pearson correlation using MathNet.Numerics
        double correlation = Correlation.Pearson(frontIVChanges, backIVChanges);

        // Handle edge case where correlation is NaN (e.g., zero variance)
        if (double.IsNaN(correlation))
        {
            return new STHD002A
            {
                Symbol = symbol,
                Correlation = double.NaN,
                Threshold = _maxAcceptableCorrelation,
                Observations = observations,
                MinimumObservations = _minimumObservations,
                PassesFilter = false,
                HasSufficientData = true,
                Interpretation = "Correlation undefined (zero variance in one or both series)."
            };
        }

        // Determine interpretation
        string interpretation = DetermineInterpretation(correlation);

        // Evaluate against threshold
        bool passesFilter = correlation < _maxAcceptableCorrelation;

        var result = new STHD002A
        {
            Symbol = symbol,
            Correlation = correlation,
            Threshold = _maxAcceptableCorrelation,
            Observations = observations,
            MinimumObservations = _minimumObservations,
            PassesFilter = passesFilter,
            HasSufficientData = true,
            Interpretation = interpretation
        };

        SafeLog(() => LogCorrelationResult(_logger!, symbol, correlation, interpretation, null));

        return result;
    }

    /// <summary>
    /// Analyses correlation from historical IV time series.
    /// </summary>
    /// <param name="symbol">The underlying symbol.</param>
    /// <param name="frontIVHistory">Front-month IV time series (levels, not changes).</param>
    /// <param name="backIVHistory">Back-month IV time series (levels, not changes).</param>
    /// <returns>Vega correlation analysis result.</returns>
    
    public STHD002A AnalyseFromLevels(
        string symbol,
        IReadOnlyList<double> frontIVHistory,
        IReadOnlyList<double> backIVHistory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(frontIVHistory);
        ArgumentNullException.ThrowIfNull(backIVHistory);

        // Compute daily changes (first differences)
        double[] frontChanges = ComputeChanges(frontIVHistory);
        double[] backChanges = ComputeChanges(backIVHistory);

        return Analyse(symbol, frontChanges, backChanges);
    }

    /// <summary>
    /// Computes daily changes (first differences) from a time series.
    /// </summary>
    /// <param name="levels">Time series of levels.</param>
    /// <returns>Array of daily changes.</returns>
    private static double[] ComputeChanges(IReadOnlyList<double> levels)
    {
        if (levels.Count < 2)
        {
            return [];
        }

        double[] changes = new double[levels.Count - 1];
        for (int i = 1; i < levels.Count; i++)
        {
            changes[i - 1] = levels[i] - levels[i - 1];
        }

        return changes;
    }

    /// <summary>
    /// Determines a human-readable interpretation of the correlation.
    /// </summary>
    /// <param name="correlation">The Pearson correlation coefficient.</param>
    /// <returns>Interpretation string.</returns>
    private static string DetermineInterpretation(double correlation)
    {
        double absCorrelation = Math.Abs(correlation);

        return absCorrelation switch
        {
            < 0.30 => "Weak correlation - Strong vega independence between tenors.",
            < 0.50 => "Moderate-weak correlation - Good independence for calendar spread.",
            < 0.70 => "Moderate correlation - Acceptable for calendar spread with monitoring.",
            < 0.85 => "Strong correlation - Elevated sympathetic collapse risk.",
            _ => "Very strong correlation - Calendar spread thesis at risk."
        };
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation (Rule 15).
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
            // Swallow logging exceptions (Rule 15: Fault Isolation)
        }
#pragma warning restore CA1031
    }
}