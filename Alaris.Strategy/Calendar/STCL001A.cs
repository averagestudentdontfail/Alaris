// =============================================================================
// STCL001A.cs - Trading Calendar Service (NYSE)
// Component: STCL001A | Category: Calendar | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 3.4
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

namespace Alaris.Strategy.Calendar;

/// <summary>
/// Interface for trading calendar operations.
/// </summary>
public interface ITradingCalendar
{
    /// <summary>
    /// Gets the number of business days between two dates.
    /// </summary>
    /// <param name="startDate">Start date (exclusive by default).</param>
    /// <param name="endDate">End date (exclusive by default).</param>
    /// <returns>Number of business days.</returns>
    public int GetBusinessDaysBetween(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Determines if a date is a business day.
    /// </summary>
    /// <param name="targetDate">Date to check.</param>
    /// <returns>True if the date is a business day.</returns>
    public bool IsBusinessDay(DateTime targetDate);

    /// <summary>
    /// Calculates time to expiry in years (trading days / 252).
    /// </summary>
    /// <param name="currentDate">Current date.</param>
    /// <param name="expiryDate">Option expiration date.</param>
    /// <returns>Time to expiry in years.</returns>
    public double GetTimeToExpiryInYears(DateTime currentDate, DateTime expiryDate);
}

/// <summary>
/// NYSE trading calendar implementation using QuantLib.
/// </summary>
/// <remarks>
/// <para>
/// This service wraps QuantLib's UnitedStates NYSE calendar to provide
/// accurate business day calculations that exclude:
/// </para>
/// 
/// <list type="bullet">
/// <item>Weekends (Saturday, Sunday)</item>
/// <item>New Year's Day</item>
/// <item>Martin Luther King Jr. Day</item>
/// <item>Presidents' Day</item>
/// <item>Good Friday</item>
/// <item>Memorial Day</item>
/// <item>Juneteenth (from 2022)</item>
/// <item>Independence Day</item>
/// <item>Labor Day</item>
/// <item>Thanksgiving Day</item>
/// <item>Christmas Day</item>
/// </list>
/// 
/// <para>
/// The calendar is thread-safe for read operations but not for modification.
/// A single instance should be shared across the application.
/// </para>
/// </remarks>
public sealed class STCL001A : ITradingCalendar, IDisposable
{
    private readonly UnitedStates _calendar;
    private bool _disposed;

    /// <summary>
    /// Trading days per year for annualisation.
    /// </summary>
    public const double TradingDaysPerYear = 252.0;

    /// <summary>
    /// Initialises a new NYSE trading calendar.
    /// </summary>
    public STCL001A()
    {
        _calendar = new UnitedStates(UnitedStates.Market.NYSE);
    }

    /// <inheritdoc/>
    public int GetBusinessDaysBetween(DateTime startDate, DateTime endDate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (endDate <= startDate)
        {
            return 0;
        }

        using Date qlStart = CreateQuantLibDate(startDate);
        using Date qlEnd = CreateQuantLibDate(endDate);

        // Include first day, exclude last day (standard convention for T-to-expiry)
        return _calendar.businessDaysBetween(qlStart, qlEnd, true, false);
    }

    /// <inheritdoc/>
    public bool IsBusinessDay(DateTime targetDate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using Date qlDate = CreateQuantLibDate(targetDate);
        return _calendar.isBusinessDay(qlDate);
    }

    /// <inheritdoc/>
    public double GetTimeToExpiryInYears(DateTime currentDate, DateTime expiryDate)
    {
        int businessDays = GetBusinessDaysBetween(currentDate, expiryDate);
        return Math.Max(0, businessDays / TradingDaysPerYear);
    }

    /// <summary>
    /// Creates a QuantLib Date from a .NET DateTime.
    /// </summary>
    private static Date CreateQuantLibDate(DateTime dateTime)
    {
        Month month = (Month)dateTime.Month;
        return new Date(dateTime.Day, month, dateTime.Year);
    }

    /// <summary>
    /// Disposes of the underlying QuantLib calendar.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _calendar.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Default trading calendar provider and utilities.
/// </summary>
/// <remarks>
/// This class provides:
/// <list type="bullet">
/// <item>Singleton NYSE calendar instance for DI fallback</item>
/// <item>Static DTE-to-years conversion using 252 trading days/year</item>
/// </list>
/// </remarks>
public static class TradingCalendarDefaults
{
    private static readonly Lazy<ITradingCalendar> LazyInstance = new(
        () => new STCL001A(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Trading days per year for annualisation (NYSE standard).
    /// </summary>
    public const double TradingDaysPerYear = 252.0;

    /// <summary>
    /// Gets the default NYSE trading calendar instance.
    /// </summary>
    /// <remarks>
    /// This singleton instance should be used when dependency injection
    /// is not available. Prefer constructor injection where possible.
    /// </remarks>
    public static ITradingCalendar Instance => LazyInstance.Value;

    /// <summary>
    /// Converts days-to-expiry (trading days) to time in years.
    /// </summary>
    /// <param name="daysToExpiry">Trading days until expiration.</param>
    /// <returns>Time to expiry in years (DTE / 252).</returns>
    /// <remarks>
    /// Use this for consistent DTE conversion across all strategy components.
    /// Assumes DTE is already in trading days (not calendar days).
    /// </remarks>
    public static double DteToYears(int daysToExpiry)
    {
        return Math.Max(0, daysToExpiry / TradingDaysPerYear);
    }

    /// <summary>
    /// Converts days-to-expiry (trading days) to time in years.
    /// </summary>
    /// <param name="daysToExpiry">Trading days until expiration (double for API compatibility).</param>
    /// <returns>Time to expiry in years (DTE / 252).</returns>
    public static double DteToYears(double daysToExpiry)
    {
        return Math.Max(0, daysToExpiry / TradingDaysPerYear);
    }

    /// <summary>
    /// Converts time in years back to trading days.
    /// </summary>
    /// <param name="years">Time in years.</param>
    /// <returns>Trading days.</returns>
    public static int YearsToDte(double years)
    {
        return (int)Math.Round(years * TradingDaysPerYear);
    }
}
