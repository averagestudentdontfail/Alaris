// STCL001A.cs - Trading calendar service (NYSE)
// Component ID: STCL001A
//
// This implementation provides NYSE holiday calendar functionality
// without depending on QuantLib.
//
// NYSE Holiday Rules Reference:
// - Federal Reserve Bank of New York
// - NYSE official holiday calendar
//
// References:
// - Alaris.Governance/Coding.md Rule 8 (Limited Scope)
// - Alaris.Governance/Coding.md Rule 5 (Avoid heap in hot paths)

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
/// NYSE trading calendar implementation with native holiday rules.
/// Replaces QuantLib UnitedStates(NYSE) with equivalent functionality.
/// </summary>
public sealed class STCL001A : ITradingCalendar
{
    /// <summary>
    /// Trading days per year for annualisation.
    /// </summary>
    public const double TradingDaysPerYear = 252.0;

    // Pre-computed holidays cache for performance (Rule 5: avoid heap in hot paths)
    // Cache is lazily populated as needed
    private readonly Dictionary<int, HashSet<DateTime>> _holidayCache =
        new Dictionary<int, HashSet<DateTime>>();
    private readonly object _cacheLock = new object();

    /// <summary>
    /// Initialises a new NYSE trading calendar.
    /// </summary>
    public STCL001A()
    {
    }

    /// <inheritdoc/>
    public int GetBusinessDaysBetween(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            return 0;
        }

        // Include first day, exclude last day (standard convention for T-to-expiry)
        int count = 0;
        DateTime current = startDate.Date;
        DateTime end = endDate.Date;

        // Bounded loop - max reasonable range is ~10 years (Rule 3)
        const int maxDays = 365 * 10;
        int iterations = 0;

        while (current < end && iterations < maxDays)
        {
            if (IsBusinessDay(current))
            {
                count++;
            }
            current = current.AddDays(1);
            iterations++;
        }

        return count;
    }

    /// <inheritdoc/>
    public bool IsBusinessDay(DateTime targetDate)
    {
        DateTime date = targetDate.Date;

        // Weekend check (Saturday = 6, Sunday = 0)
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        // Holiday check
        return !IsNYSEHoliday(date);
    }

    /// <inheritdoc/>
    public double GetTimeToExpiryInYears(DateTime currentDate, DateTime expiryDate)
    {
        int businessDays = GetBusinessDaysBetween(currentDate, expiryDate);
        return System.Math.Max(0, businessDays / TradingDaysPerYear);
    }

    /// <summary>
    /// Determines if the given date is an NYSE holiday.
    /// </summary>
    /// <param name="date">Date to check (must be weekday).</param>
    /// <returns>True if the date is an NYSE holiday.</returns>
    private bool IsNYSEHoliday(DateTime date)
    {
        HashSet<DateTime> yearHolidays = GetHolidaysForYear(date.Year);
        return yearHolidays.Contains(date.Date);
    }

    /// <summary>
    /// Gets or computes the set of NYSE holidays for a given year.
    /// </summary>
    private HashSet<DateTime> GetHolidaysForYear(int year)
    {
        lock (_cacheLock)
        {
            if (_holidayCache.TryGetValue(year, out HashSet<DateTime>? cached))
            {
                return cached;
            }

            HashSet<DateTime> holidays = ComputeNYSEHolidays(year);
            _holidayCache[year] = holidays;
            return holidays;
        }
    }

    /// <summary>
    /// Computes all NYSE holidays for a given year.
    /// </summary>
    /// <remarks>
    /// NYSE Holiday Rules:
    /// 1. New Year's Day (Jan 1, or following Monday if Sunday)
    /// 2. Martin Luther King Jr. Day (3rd Monday of January)
    /// 3. Presidents' Day (3rd Monday of February)
    /// 4. Good Friday (Friday before Easter Sunday)
    /// 5. Memorial Day (Last Monday of May)
    /// 6. Juneteenth (June 19, or nearest weekday) - Added 2021
    /// 7. Independence Day (July 4, or nearest weekday)
    /// 8. Labor Day (1st Monday of September)
    /// 9. Thanksgiving Day (4th Thursday of November)
    /// 10. Christmas Day (Dec 25, or nearest weekday)
    /// </remarks>
    private static HashSet<DateTime> ComputeNYSEHolidays(int year)
    {
        HashSet<DateTime> holidays = new HashSet<DateTime>();

        // 1. New Year's Day
        DateTime newYears = new DateTime(year, 1, 1);
        holidays.Add(AdjustWeekendHoliday(newYears));

        // 2. Martin Luther King Jr. Day (3rd Monday of January)
        DateTime mlkDay = GetNthWeekdayOfMonth(year, 1, DayOfWeek.Monday, 3);
        holidays.Add(mlkDay);

        // 3. Presidents' Day (3rd Monday of February)
        DateTime presidentsDay = GetNthWeekdayOfMonth(year, 2, DayOfWeek.Monday, 3);
        holidays.Add(presidentsDay);

        // 4. Good Friday (Friday before Easter)
        DateTime easter = ComputeEasterSunday(year);
        DateTime goodFriday = easter.AddDays(-2);
        holidays.Add(goodFriday);

        // 5. Memorial Day (Last Monday of May)
        DateTime memorialDay = GetLastWeekdayOfMonth(year, 5, DayOfWeek.Monday);
        holidays.Add(memorialDay);

        // 6. Juneteenth (June 19) - Official NYSE holiday starting 2021
        if (year >= 2021)
        {
        DateTime juneteenth = new DateTime(year, 6, 19);
            holidays.Add(AdjustWeekendHoliday(juneteenth));
        }

        // 7. Independence Day (July 4)
        DateTime independenceDay = new DateTime(year, 7, 4);
        holidays.Add(AdjustWeekendHoliday(independenceDay));

        // 8. Labor Day (1st Monday of September)
        DateTime laborDay = GetNthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1);
        holidays.Add(laborDay);

        // 9. Thanksgiving (4th Thursday of November)
        DateTime thanksgiving = GetNthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4);
        holidays.Add(thanksgiving);

        // 10. Christmas (December 25)
        DateTime christmas = new DateTime(year, 12, 25);
        holidays.Add(AdjustWeekendHoliday(christmas));

        return holidays;
    }

    /// <summary>
    /// Adjusts a weekend holiday to the nearest weekday.
    /// Saturday holidays are observed on Friday.
    /// Sunday holidays are observed on Monday.
    /// </summary>
    private static DateTime AdjustWeekendHoliday(DateTime holiday)
    {
        return holiday.DayOfWeek switch
        {
            DayOfWeek.Saturday => holiday.AddDays(-1),  // Friday
            DayOfWeek.Sunday => holiday.AddDays(1),     // Monday
            _ => holiday
        };
    }

    /// <summary>
    /// Gets the nth occurrence of a weekday in a month.
    /// </summary>
    /// <param name="year">Year.</param>
    /// <param name="month">Month (1-12).</param>
    /// <param name="dayOfWeek">Target day of week.</param>
    /// <param name="n">Occurrence number (1-5).</param>
    private static DateTime GetNthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, int n)
    {
        DateTime first = new(year, month, 1);
        int daysToAdd = ((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7;
        DateTime firstOccurrence = first.AddDays(daysToAdd);
        return firstOccurrence.AddDays((n - 1) * 7);
    }

    /// <summary>
    /// Gets the last occurrence of a weekday in a month.
    /// </summary>
    private static DateTime GetLastWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek)
    {
        DateTime lastDay = new(year, month, DateTime.DaysInMonth(year, month));
        int daysToSubtract = ((int)lastDay.DayOfWeek - (int)dayOfWeek + 7) % 7;
        return lastDay.AddDays(-daysToSubtract);
    }

    /// <summary>
    /// Computes Easter Sunday using the Anonymous Gregorian algorithm.
    /// </summary>
    /// <remarks>
    /// Algorithm source: Meeus, Jean. "Astronomical Algorithms" (1991)
    /// </remarks>
    private static DateTime ComputeEasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = ((19 * a) + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        int m = (a + (11 * h) + (22 * l)) / 451;
        int month = (h + l - (7 * m) + 114) / 31;
        int day = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateTime(year, month, day);
    }
}

/// <summary>
/// Default trading calendar provider and utilities.
/// </summary>
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
    public static ITradingCalendar Instance => LazyInstance.Value;

    /// <summary>
    /// Converts days-to-expiry (trading days) to time in years.
    /// </summary>
    /// <param name="daysToExpiry">Trading days until expiration.</param>
    /// <returns>Time to expiry in years (DTE / 252).</returns>
    public static double DteToYears(int daysToExpiry)
    {
        return System.Math.Max(0, daysToExpiry / TradingDaysPerYear);
    }

    /// <summary>
    /// Converts days-to-expiry (trading days) to time in years.
    /// </summary>
    /// <param name="daysToExpiry">Trading days until expiration (double for API compatibility).</param>
    /// <returns>Time to expiry in years (DTE / 252).</returns>
    public static double DteToYears(double daysToExpiry)
    {
        return System.Math.Max(0, daysToExpiry / TradingDaysPerYear);
    }

    /// <summary>
    /// Converts time in years back to trading days.
    /// </summary>
    /// <param name="years">Time in years.</param>
    /// <returns>Trading days.</returns>
    public static int YearsToDte(double years)
    {
        return (int)System.Math.Round(years * TradingDaysPerYear);
    }
}
