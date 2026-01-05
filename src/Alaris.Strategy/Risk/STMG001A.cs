// STMG001A.cs - maturity guard for position entry/exit

using Alaris.Strategy.Calendar;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Risk;

/// <summary>
/// Provides maturity-based position filtering to avoid near-expiry regime.
/// </summary>

public sealed class STMG001A
{
    /// <summary>
    /// Minimum time-to-expiry for entering new positions (in years).
    /// Default: 10/252 ≈ 2 weeks.
    /// </summary>
    public const double DefaultMinEntryMaturity = 10.0 / 252.0;

    /// <summary>
    /// Maturity threshold for force exit (in years).
    /// Default: 5/252 ≈ 1 week.
    /// </summary>
    public const double DefaultForceExitMaturity = 5.0 / 252.0;

    /// <summary>
    /// Near-expiry handler threshold (DBEX001A).
    /// Below this, QD+ is numerically unstable.
    /// </summary>
    public const double NearExpiryThreshold = 3.0 / 252.0;

    private readonly double _minEntryMaturity;
    private readonly double _forceExitMaturity;
    private readonly ITradingCalendar? _calendar;
    private readonly ILogger<STMG001A>? _logger;

    /// <summary>
    /// Initialises a new maturity guard with default thresholds.
    /// </summary>
    /// <param name="calendar">Optional trading calendar for accurate business days.</param>
    /// <param name="logger">Optional logger instance.</param>
    public STMG001A(ITradingCalendar? calendar = null, ILogger<STMG001A>? logger = null)
        : this(DefaultMinEntryMaturity, DefaultForceExitMaturity, calendar, logger)
    {
    }

    /// <summary>
    /// Initialises a new maturity guard with custom thresholds.
    /// </summary>
    /// <param name="minEntryMaturity">Minimum maturity for entry.</param>
    /// <param name="forceExitMaturity">Maturity threshold for force exit.</param>
    /// <param name="calendar">Optional trading calendar for accurate business days.</param>
    /// <param name="logger">Optional logger instance.</param>
    public STMG001A(
        double minEntryMaturity, 
        double forceExitMaturity, 
        ITradingCalendar? calendar = null,
        ILogger<STMG001A>? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minEntryMaturity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(forceExitMaturity);

        if (forceExitMaturity >= minEntryMaturity)
        {
            throw new ArgumentException(
                "Force exit maturity must be less than minimum entry maturity");
        }

        _minEntryMaturity = minEntryMaturity;
        _forceExitMaturity = forceExitMaturity;
        _calendar = calendar;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates whether a position entry should be allowed based on maturity.
    /// </summary>
    /// <param name="symbol">Security symbol for logging.</param>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <returns>Entry evaluation result.</returns>
    public MaturityEntryResult EvaluateEntry(string symbol, double timeToExpiry)
    {
        if (timeToExpiry < _forceExitMaturity)
        {
            SafeLog(LogLevel.Warning,
                "Entry rejected for {Symbol}: T={T:F4} below force exit threshold {Threshold:F4}",
                symbol, timeToExpiry, _forceExitMaturity);

            return new MaturityEntryResult
            {
                IsAllowed = false,
                TimeToExpiry = timeToExpiry,
                Reason = $"Maturity {timeToExpiry:F4}y below exit threshold {_forceExitMaturity:F4}y",
                RecommendedAction = MaturityAction.Reject
            };
        }

        if (timeToExpiry < _minEntryMaturity)
        {
            SafeLog(LogLevel.Warning,
                "Entry rejected for {Symbol}: T={T:F4} below minimum entry {Min:F4}",
                symbol, timeToExpiry, _minEntryMaturity);

            return new MaturityEntryResult
            {
                IsAllowed = false,
                TimeToExpiry = timeToExpiry,
                Reason = $"Maturity {timeToExpiry:F4}y below minimum entry {_minEntryMaturity:F4}y",
                RecommendedAction = MaturityAction.Reject
            };
        }

        return new MaturityEntryResult
        {
            IsAllowed = true,
            TimeToExpiry = timeToExpiry,
            Reason = "Maturity within acceptable range",
            RecommendedAction = MaturityAction.Allow
        };
    }

    /// <summary>
    /// Evaluates whether a position should be force-exited based on maturity.
    /// </summary>
    /// <param name="symbol">Security symbol for logging.</param>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <returns>Exit evaluation result.</returns>
    public MaturityExitResult EvaluateExit(string symbol, double timeToExpiry)
    {
        if (timeToExpiry <= _forceExitMaturity)
        {
            SafeLog(LogLevel.Information,
                "Force exit triggered for {Symbol}: T={T:F4} at/below threshold {Threshold:F4}",
                symbol, timeToExpiry, _forceExitMaturity);

            return new MaturityExitResult
            {
                RequiresExit = true,
                TimeToExpiry = timeToExpiry,
                Reason = $"Maturity {timeToExpiry:F4}y at/below exit threshold",
                UrgencyLevel = timeToExpiry <= NearExpiryThreshold 
                    ? ExitUrgency.Immediate 
                    : ExitUrgency.Normal
            };
        }

        if (timeToExpiry <= _minEntryMaturity)
        {
            // In warning zone - no new entries but don't force exit yet
            SafeLog(LogLevel.Debug,
                "Position {Symbol} in warning zone: T={T:F4}",
                symbol, timeToExpiry);

            return new MaturityExitResult
            {
                RequiresExit = false,
                TimeToExpiry = timeToExpiry,
                Reason = "In warning zone - monitor closely",
                UrgencyLevel = ExitUrgency.None
            };
        }

        return new MaturityExitResult
        {
            RequiresExit = false,
            TimeToExpiry = timeToExpiry,
            Reason = "Maturity within normal range",
            UrgencyLevel = ExitUrgency.None
        };
    }

    /// <summary>
    /// Converts dates to time-to-expiry in years using trading calendar.
    /// </summary>
    /// <param name="expiryDate">Option expiration date.</param>
    /// <param name="currentDate">Current evaluation date.</param>
    /// <returns>Time to expiry in years (trading days / 252).</returns>
    
    public double CalculateTimeToExpiry(DateTime expiryDate, DateTime currentDate)
    {
        // Use trading calendar if available for accurate business day count
        if (_calendar is not null)
        {
            return _calendar.GetTimeToExpiryInYears(currentDate, expiryDate);
        }

        // Fallback: approximate using 5/7 calendar-to-trading day ratio
        TimeSpan span = expiryDate - currentDate;
        double calendarDays = span.TotalDays;
        double tradingDays = calendarDays * (5.0 / 7.0);
        return Math.Max(0, tradingDays / 252.0);
    }

    /// <summary>
    /// Static fallback for calculating time-to-expiry without a calendar instance.
    /// Uses 5/7 approximation for calendar-to-trading day conversion.
    /// </summary>
    /// <param name="expiryDate">Option expiration date.</param>
    /// <param name="currentDate">Current evaluation date.</param>
    /// <returns>Time to expiry in years (approximate).</returns>
    public static double CalculateTimeToExpiryApproximate(DateTime expiryDate, DateTime currentDate)
    {
        TimeSpan span = expiryDate - currentDate;
        double calendarDays = span.TotalDays;
        double tradingDays = calendarDays * (5.0 / 7.0);
        return Math.Max(0, tradingDays / 252.0);
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation (Rule 15).
    /// Prevents logging failures from crashing critical paths.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design", "CA1031:Do not catch general exception types",
        Justification = "Logging must not crash critical pricing path - Rule 15")]
    private void SafeLog(LogLevel level, string message, params object[] args)
    {
        try
        {
#pragma warning disable CA2254 // Template should be constant
            _logger?.Log(level, message, args);
#pragma warning restore CA2254
        }
        catch (Exception)
        {
            // Logging failure must not crash critical path (Rule 15)
        }
    }
}

// Supporting Types

/// <summary>
/// Result of maturity-based entry evaluation.
/// </summary>
public sealed record MaturityEntryResult
{
    /// <summary>Whether entry is allowed.</summary>
    public required bool IsAllowed { get; init; }

    /// <summary>Time to expiration in years.</summary>
    public required double TimeToExpiry { get; init; }

    /// <summary>Human-readable reason.</summary>
    public required string Reason { get; init; }

    /// <summary>Recommended action.</summary>
    public required MaturityAction RecommendedAction { get; init; }
}

/// <summary>
/// Result of maturity-based exit evaluation.
/// </summary>
public sealed record MaturityExitResult
{
    /// <summary>Whether exit is required.</summary>
    public required bool RequiresExit { get; init; }

    /// <summary>Time to expiration in years.</summary>
    public required double TimeToExpiry { get; init; }

    /// <summary>Human-readable reason.</summary>
    public required string Reason { get; init; }

    /// <summary>Exit urgency level.</summary>
    public required ExitUrgency UrgencyLevel { get; init; }
}

/// <summary>
/// Recommended maturity action.
/// </summary>
public enum MaturityAction
{
    /// <summary>Entry allowed.</summary>
    Allow = 0,

    /// <summary>Entry rejected due to maturity.</summary>
    Reject = 1
}

/// <summary>
/// Exit urgency levels.
/// </summary>
public enum ExitUrgency
{
    /// <summary>No exit required.</summary>
    None = 0,

    /// <summary>Normal exit - close at next opportunity.</summary>
    Normal = 1,

    /// <summary>Immediate exit - near-expiry regime imminent.</summary>
    Immediate = 2
}
