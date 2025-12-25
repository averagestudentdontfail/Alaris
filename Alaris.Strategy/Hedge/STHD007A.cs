// STHD007A.cs - iv-based early exit monitor

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Hedge;

/// <summary>
/// Monitors implied volatility to trigger early exits when IV crushes faster than expected.
/// </summary>

public sealed class STHD007A
{
    private readonly ILogger<STHD007A>? _logger;

    /// <summary>
    /// Multiplier for fast crush detection (0.7 = exit if IV reaches 70% of expected post-earnings level).
    /// </summary>
    private const double FastCrushThreshold = 0.70;

    /// <summary>
    /// Ratio of actual/expected crush that triggers early exit (0.8 = exit when 80% of expected crush realised).
    /// </summary>
    private const double CrushCaptureRatio = 0.80;

    /// <summary>
    /// Minimum trading days remaining before mandatory review.
    /// </summary>
    private const int MinDaysForEarlyExit = 2;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, double, double, Exception?> LogEarlyExitTriggered =
        LoggerMessage.Define<string, double, double>(
            LogLevel.Information,
            new EventId(1, nameof(LogEarlyExitTriggered)),
            "Early exit triggered for {Symbol}: currentIV={CurrentIV:P2}, expectedPostEarnings={ExpectedIV:P2}");

    private static readonly Action<ILogger, string, double, double, Exception?> LogCrushRatioTriggered =
        LoggerMessage.Define<string, double, double>(
            LogLevel.Information,
            new EventId(2, nameof(LogCrushRatioTriggered)),
            "Crush ratio exit for {Symbol}: actualCrush={ActualCrush:P2}, expectedCrush={ExpectedCrush:P2}");

    private static readonly Action<ILogger, string, Exception?> LogHoldRecommended =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3, nameof(LogHoldRecommended)),
            "Hold recommended for {Symbol}: IV crush within expected range");

    /// <summary>
    /// Initialises a new instance of the IV early exit monitor.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public STHD007A(ILogger<STHD007A>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Evaluates whether early exit is warranted based on IV behaviour.
    /// </summary>
    /// <param name="symbol">The underlying symbol.</param>
    /// <param name="currentFrontIV">Current front-month implied volatility.</param>
    /// <param name="entryFrontIV">Front-month IV at position entry.</param>
    /// <param name="expectedPostEarningsIV">Expected IV after earnings (base volatility from L&amp;S model).</param>
    /// <param name="expectedIVCrush">Expected IV crush magnitude in volatility points.</param>
    /// <param name="daysToExpiry">Trading days until front-month expiration.</param>
    /// <returns>Early exit decision with recommendation and rationale.</returns>
    /// <exception cref="ArgumentException">Thrown when symbol is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when IV values are invalid.</exception>
    public STHD008A Evaluate(
        string symbol,
        double currentFrontIV,
        double entryFrontIV,
        double expectedPostEarningsIV,
        double expectedIVCrush,
        int daysToExpiry)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        }

        if (currentFrontIV < 0 || currentFrontIV > 5.0) // 500% IV cap
        {
            throw new ArgumentOutOfRangeException(nameof(currentFrontIV),
                "Current IV must be between 0 and 500%");
        }

        if (entryFrontIV <= 0 || entryFrontIV > 5.0)
        {
            throw new ArgumentOutOfRangeException(nameof(entryFrontIV),
                "Entry IV must be positive and at most 500%");
        }

        if (expectedPostEarningsIV < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedPostEarningsIV),
                "Expected post-earnings IV cannot be negative");
        }

        if (expectedIVCrush < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedIVCrush),
                "Expected IV crush cannot be negative");
        }

        // Calculate actual IV crush so far
        double actualCrush = entryFrontIV - currentFrontIV;
        double crushRatio = expectedIVCrush > 0 ? actualCrush / expectedIVCrush : 0;

        // Check 1: Has IV dropped below expected post-earnings level prematurely?
        double earlyExitIVLevel = expectedPostEarningsIV * FastCrushThreshold;
        if (currentFrontIV < earlyExitIVLevel && daysToExpiry > MinDaysForEarlyExit)
        {
            SafeLog(() => LogEarlyExitTriggered(_logger!, symbol, currentFrontIV, expectedPostEarningsIV, null));

            return new STHD008A
            {
                Symbol = symbol,
                ShouldExit = true,
                Reason = $"IV crushed to {currentFrontIV:P2}, below threshold of {earlyExitIVLevel:P2}",
                CurrentFrontIV = currentFrontIV,
                EntryFrontIV = entryFrontIV,
                ExpectedPostEarningsIV = expectedPostEarningsIV,
                ActualCrush = actualCrush,
                ExpectedCrush = expectedIVCrush,
                CrushRatio = crushRatio,
                CapturedProfitPercent = EstimateProfitCapture(crushRatio),
                RecommendedAction = EarlyExitAction.ExitNow,
                DaysToExpiry = daysToExpiry
            };
        }

        // Check 2: Have we captured most of the expected IV crush?
        if (crushRatio >= CrushCaptureRatio && daysToExpiry > MinDaysForEarlyExit)
        {
            SafeLog(() => LogCrushRatioTriggered(_logger!, symbol, actualCrush, expectedIVCrush, null));

            return new STHD008A
            {
                Symbol = symbol,
                ShouldExit = true,
                Reason = $"Captured {crushRatio:P0} of expected IV crush",
                CurrentFrontIV = currentFrontIV,
                EntryFrontIV = entryFrontIV,
                ExpectedPostEarningsIV = expectedPostEarningsIV,
                ActualCrush = actualCrush,
                ExpectedCrush = expectedIVCrush,
                CrushRatio = crushRatio,
                CapturedProfitPercent = EstimateProfitCapture(crushRatio),
                RecommendedAction = EarlyExitAction.ExitNow,
                DaysToExpiry = daysToExpiry
            };
        }

        // Check 3: Approaching expiry with minimal remaining theta value
        if (daysToExpiry <= MinDaysForEarlyExit && crushRatio >= 0.5)
        {
            return new STHD008A
            {
                Symbol = symbol,
                ShouldExit = true,
                Reason = $"Near expiry ({daysToExpiry} days) with {crushRatio:P0} crush captured",
                CurrentFrontIV = currentFrontIV,
                EntryFrontIV = entryFrontIV,
                ExpectedPostEarningsIV = expectedPostEarningsIV,
                ActualCrush = actualCrush,
                ExpectedCrush = expectedIVCrush,
                CrushRatio = crushRatio,
                CapturedProfitPercent = EstimateProfitCapture(crushRatio),
                RecommendedAction = EarlyExitAction.ExitNow,
                DaysToExpiry = daysToExpiry
            };
        }

        // Default: Hold position
        SafeLog(() => LogHoldRecommended(_logger!, symbol, null));

        return new STHD008A
        {
            Symbol = symbol,
            ShouldExit = false,
            Reason = $"IV crush at {crushRatio:P0} of expected, holding position",
            CurrentFrontIV = currentFrontIV,
            EntryFrontIV = entryFrontIV,
            ExpectedPostEarningsIV = expectedPostEarningsIV,
            ActualCrush = actualCrush,
            ExpectedCrush = expectedIVCrush,
            CrushRatio = crushRatio,
            CapturedProfitPercent = EstimateProfitCapture(crushRatio),
            RecommendedAction = EarlyExitAction.Hold,
            DaysToExpiry = daysToExpiry
        };
    }

    /// <summary>
    /// Estimates profit capture percentage based on IV crush realisation.
    /// </summary>
    /// <param name="crushRatio">Ratio of actual to expected IV crush.</param>
    /// <returns>Estimated profit capture percentage.</returns>
    
    private static double EstimateProfitCapture(double crushRatio)
    {
        // Clamp to [0, 1] range
        double clampedRatio = Math.Max(0, Math.Min(1.0, crushRatio));

        // Slight concavity to account for diminishing returns at extreme crushes
        return clampedRatio * 0.95;
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
            // Swallow logging exceptions
        }
#pragma warning restore CA1031
    }
}

/// <summary>
/// Represents the result of IV-based early exit evaluation.
/// </summary>
public sealed record STHD008A
{
    /// <summary>
    /// Gets the symbol being evaluated.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets whether early exit is recommended.
    /// </summary>
    public required bool ShouldExit { get; init; }

    /// <summary>
    /// Gets the reason for the recommendation.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the current front-month implied volatility.
    /// </summary>
    public required double CurrentFrontIV { get; init; }

    /// <summary>
    /// Gets the front-month IV at position entry.
    /// </summary>
    public required double EntryFrontIV { get; init; }

    /// <summary>
    /// Gets the expected post-earnings IV (base volatility).
    /// </summary>
    public required double ExpectedPostEarningsIV { get; init; }

    /// <summary>
    /// Gets the actual IV crush realised so far.
    /// </summary>
    public required double ActualCrush { get; init; }

    /// <summary>
    /// Gets the expected IV crush magnitude.
    /// </summary>
    public required double ExpectedCrush { get; init; }

    /// <summary>
    /// Gets the ratio of actual to expected IV crush.
    /// </summary>
    public required double CrushRatio { get; init; }

    /// <summary>
    /// Gets the estimated profit capture percentage.
    /// </summary>
    public required double CapturedProfitPercent { get; init; }

    /// <summary>
    /// Gets the recommended early exit action.
    /// </summary>
    public required EarlyExitAction RecommendedAction { get; init; }

    /// <summary>
    /// Gets the trading days remaining until front-month expiry.
    /// </summary>
    public required int DaysToExpiry { get; init; }

    /// <summary>
    /// Gets a human-readable summary of the evaluation.
    /// </summary>
    public string Summary => $"{Symbol}: {RecommendedAction} - {Reason}";
}

/// <summary>
/// Early exit action recommendations.
/// </summary>
public enum EarlyExitAction
{
    /// <summary>
    /// Hold position - IV crush within expected range.
    /// </summary>
    Hold = 0,

    /// <summary>
    /// Exit position immediately - IV crush realised faster than expected.
    /// </summary>
    ExitNow = 1,

    /// <summary>
    /// Monitor closely - approaching exit threshold.
    /// </summary>
    MonitorClosely = 2
}
