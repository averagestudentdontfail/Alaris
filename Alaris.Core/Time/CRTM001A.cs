// CRTM001A.cs - ITimeProvider interface for backtest-aware time operations

using NodaTime;

namespace Alaris.Core.Time;

/// <summary>
/// Provides time-aware operations supporting both live and backtest modes.
/// </summary>
/// <remarks>
/// This abstraction enables the Alaris system to operate in both:
/// - Live mode: Uses system clock (real wall-clock time)
/// - Backtest mode: Uses simulated time from LEAN algorithm
/// 
/// All temporal operations should use this interface rather than
/// DateTime.Now/UtcNow to ensure correct behaviour in backtests.
/// </remarks>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current instant in time (UTC).
    /// </summary>
    /// <remarks>
    /// In live mode, this returns the current system time.
    /// In backtest mode, this returns the simulated algorithm time.
    /// </remarks>
    public Instant Now { get; }

    /// <summary>
    /// Gets the current date in the trading timezone (America/New_York).
    /// </summary>
    public LocalDate Today { get; }

    /// <summary>
    /// Gets the current date and time in the trading timezone.
    /// </summary>
    public ZonedDateTime ZonedNow { get; }

    /// <summary>
    /// Gets the trading timezone (America/New_York for NYSE).
    /// </summary>
    public DateTimeZone TimeZone { get; }

    /// <summary>
    /// Converts a DateTime to an Instant.
    /// </summary>
    /// <param name="dateTime">The DateTime to convert (assumed UTC if Kind is Unspecified).</param>
    /// <returns>The corresponding Instant.</returns>
    public Instant ToInstant(DateTime dateTime);

    /// <summary>
    /// Converts an Instant to a DateTime (UTC).
    /// </summary>
    /// <param name="instant">The Instant to convert.</param>
    /// <returns>The corresponding DateTime in UTC.</returns>
    public DateTime ToDateTime(Instant instant);

    /// <summary>
    /// Converts a DateTime to a LocalDate in the trading timezone.
    /// </summary>
    /// <param name="dateTime">The DateTime to convert.</param>
    /// <returns>The corresponding LocalDate.</returns>
    public LocalDate ToLocalDate(DateTime dateTime);

    /// <summary>
    /// Converts a LocalDate to a DateTime (midnight UTC on that date).
    /// </summary>
    /// <param name="localDate">The LocalDate to convert.</param>
    /// <returns>The corresponding DateTime.</returns>
    public DateTime ToDateTime(LocalDate localDate);
}
