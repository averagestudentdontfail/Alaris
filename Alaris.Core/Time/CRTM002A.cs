// CRTM002A.cs - LiveTimeProvider (system clock for production)

using NodaTime;

namespace Alaris.Core.Time;

/// <summary>
/// Time provider for live trading using the system clock.
/// </summary>
/// <remarks>
/// Uses NodaTime's SystemClock.Instance for all time operations.
/// The trading timezone is set to America/New_York (NYSE).
/// </remarks>
public sealed class LiveTimeProvider : ITimeProvider
{
    private static readonly DateTimeZone NyseTimeZone = 
        DateTimeZoneProviders.Tzdb["America/New_York"];

    private readonly IClock _clock;

    /// <summary>
    /// Initialises a new live time provider using the system clock.
    /// </summary>
    public LiveTimeProvider() : this(SystemClock.Instance)
    {
    }

    /// <summary>
    /// Initialises a new live time provider with a custom clock (for testing).
    /// </summary>
    /// <param name="clock">The clock to use.</param>
    public LiveTimeProvider(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc/>
    public Instant Now => _clock.GetCurrentInstant();

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
        // Return midnight UTC on the given date
        return localDate.AtMidnight().InZoneStrictly(DateTimeZone.Utc).ToDateTimeUtc();
    }
}
