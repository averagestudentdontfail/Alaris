// BacktestTimeProvider.cs - simulated time provider for backtesting

using NodaTime;

namespace Alaris.Core.Time;

/// <summary>
/// Time provider for backtesting using simulated time from LEAN algorithm.
/// </summary>
/// <remarks>
/// This provider accepts a delegate that returns the current simulated time,
/// allowing it to advance as the backtest progresses through historical data.
/// </remarks>
public sealed class BacktestTimeProvider : ITimeProvider
{
    private static readonly DateTimeZone NyseTimeZone = 
        DateTimeZoneProviders.Tzdb["America/New_York"];

    private readonly Func<DateTime> _timeSource;

    /// <summary>
    /// Initialises a new backtest time provider with a simulated time source.
    /// </summary>
    /// <param name="timeSource">
    /// A delegate returning the current simulated DateTime (from LEAN's algorithm.Time).
    /// </param>
    public BacktestTimeProvider(Func<DateTime> timeSource)
    {
        _timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
    }

    /// <summary>
    /// Initialises a new backtest time provider with a fixed time.
    /// </summary>
    /// <param name="fixedTime">The fixed simulated time to use.</param>
    public BacktestTimeProvider(DateTime fixedTime)
        : this(() => fixedTime)
    {
    }

    /// <summary>
    /// Initialises a new backtest time provider with a fixed instant.
    /// </summary>
    /// <param name="fixedInstant">The fixed simulated instant to use.</param>
    public BacktestTimeProvider(Instant fixedInstant)
        : this(() => fixedInstant.ToDateTimeUtc())
    {
    }

    /// <inheritdoc/>
    public Instant Now
    {
        get
        {
            DateTime simulatedTime = _timeSource();
            return ToInstant(simulatedTime);
        }
    }

    /// <inheritdoc/>
    public LocalDate Today => ZonedNow.Date;

    /// <inheritdoc/>
    public ZonedDateTime ZonedNow => Now.InZone(NyseTimeZone);

    /// <inheritdoc/>
    public DateTimeZone TimeZone => NyseTimeZone;

    /// <inheritdoc/>
    public Instant ToInstant(DateTime dateTime)
    {
        DateTime utcDateTime = dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
        return Instant.FromDateTimeUtc(utcDateTime);
    }

    /// <inheritdoc/>
    public DateTime ToDateTime(Instant instant)
    {
        return instant.ToDateTimeUtc();
    }

    /// <inheritdoc/>
    public LocalDate ToLocalDate(DateTime dateTime)
    {
        Instant instant = ToInstant(dateTime);
        return instant.InZone(NyseTimeZone).Date;
    }

    /// <inheritdoc/>
    public DateTime ToDateTime(LocalDate localDate)
    {
        return localDate.AtMidnight().InZoneStrictly(DateTimeZone.Utc).ToDateTimeUtc();
    }

    /// <summary>
    /// Updates the time source to a new delegate.
    /// </summary>
    /// <remarks>
    /// This is useful when the LEAN algorithm context changes during setup.
    /// </remarks>
    public BacktestTimeProvider WithTimeSource(Func<DateTime> newTimeSource)
    {
        return new BacktestTimeProvider(newTimeSource);
    }
}
