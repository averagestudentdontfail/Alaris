namespace Alaris.Strategy.Core;

/// <summary>
/// Production-grade trading calendar for calculating business days and market holidays.
/// Supports US market holidays and can be extended for international markets.
///
/// For production systems, this should be integrated with a market data provider's
/// holiday calendar feed to handle unexpected market closures.
/// </summary>
public static class STTM003A
{
    /// <summary>
    /// Standard trading days per year (252 for US markets).
    /// </summary>
    public const int TradingDaysPerYear = 252;

    /// <summary>
    /// Calculates the number of trading days between two dates.
    /// Excludes weekends and US market holidays.
    /// </summary>
    /// <param name="start">Start date (inclusive).</param>
    /// <param name="end">End date (exclusive).</param>
    /// <param name="includeEndDate">Whether to include the end date in the count.</param>
    /// <returns>Number of trading days.</returns>
    public static int GetTradingDays(DateTime start, DateTime end, bool includeEndDate = false)
    {
        if (start >= end)
        {
            return 0;
        }

        int days = 0;
        DateTime current = start.Date;
        DateTime endDate = includeEndDate ? end.Date.AddDays(1) : end.Date;

        while (current < endDate)
        {
            if (IsTradingDay(current))
            {
                days++;
            }
            current = current.AddDays(1);
        }

        return days;
    }

    /// <summary>
    /// Checks if a given date is a trading day (not weekend or holiday).
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns>True if the date is a trading day.</returns>
    public static bool IsTradingDay(DateTime date)
    {
        // Check if weekend
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // Check if US market holiday
        return !IsUSMarketHoliday(date);
    }

    /// <summary>
    /// Gets the next trading day after the given date.
    /// </summary>
    /// <param name="date">The starting date.</param>
    /// <returns>The next trading day.</returns>
    public static DateTime GetNextTradingDay(DateTime date)
    {
        DateTime next = date.Date.AddDays(1);
        while (!IsTradingDay(next))
        {
            next = next.AddDays(1);
        }
        return next;
    }

    /// <summary>
    /// Gets the previous trading day before the given date.
    /// </summary>
    /// <param name="date">The starting date.</param>
    /// <returns>The previous trading day.</returns>
    public static DateTime GetPreviousTradingDay(DateTime date)
    {
        DateTime prev = date.Date.AddDays(-1);
        while (!IsTradingDay(prev))
        {
            prev = prev.AddDays(-1);
        }
        return prev;
    }

    /// <summary>
    /// Adds a specified number of trading days to a date.
    /// </summary>
    /// <param name="date">The starting date.</param>
    /// <param name="tradingDays">Number of trading days to add (can be negative).</param>
    /// <returns>The resulting date.</returns>
    public static DateTime AddTradingDays(DateTime date, int tradingDays)
    {
        DateTime current = date.Date;
        int remaining = Math.Abs(tradingDays);
        int direction = tradingDays >= 0 ? 1 : -1;

        while (remaining > 0)
        {
            current = current.AddDays(direction);
            if (IsTradingDay(current))
            {
                remaining--;
            }
        }

        return current;
    }

    /// <summary>
    /// Checks if a date is a US market holiday.
    /// Includes NYSE/NASDAQ observed holidays.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns>True if the date is a US market holiday.</returns>
    public static bool IsUSMarketHoliday(DateTime date)
    {
        int year = date.Year;
        int month = date.Month;
        int day = date.Day;

        // New Year's Day (January 1, or observed)
        if (IsObservedHoliday(date, new DateTime(year, 1, 1)))
        {
            return true;
        }

        // Martin Luther King Jr. Day (3rd Monday of January)
        if (date == GetNthWeekdayOfMonth(year, 1, DayOfWeek.Monday, 3))
        {
            return true;
        }

        // Presidents' Day (3rd Monday of February)
        if (date == GetNthWeekdayOfMonth(year, 2, DayOfWeek.Monday, 3))
        {
            return true;
        }

        // Good Friday (Friday before Easter)
        DateTime goodFriday = GetGoodFriday(year);
        if (date.Date == goodFriday.Date)
        {
            return true;
        }

        // Memorial Day (Last Monday of May)
        if (date == GetLastWeekdayOfMonth(year, 5, DayOfWeek.Monday))
        {
            return true;
        }

        // Juneteenth (June 19, or observed) - started in 2021
        if (year >= 2021 && IsObservedHoliday(date, new DateTime(year, 6, 19)))
        {
            return true;
        }

        // Independence Day (July 4, or observed)
        if (IsObservedHoliday(date, new DateTime(year, 7, 4)))
        {
            return true;
        }

        // Labor Day (1st Monday of September)
        if (date == GetNthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1))
        {
            return true;
        }

        // Thanksgiving (4th Thursday of November)
        if (date == GetNthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4))
        {
            return true;
        }

        // Christmas (December 25, or observed)
        if (IsObservedHoliday(date, new DateTime(year, 12, 25)))
        {
            return true;
        }

        // Special closures (September 11-14, 2001)
        if (year == 2001 && month == 9 && day >= 11 && day <= 14)
        {
            return true;
        }

        // Hurricane Sandy (October 29-30, 2012)
        if (year == 2012 && month == 10 && (day == 29 || day == 30))
        {
            return true;
        }

        // National Days of Mourning
        if (IsNationalDayOfMourning(date))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles observed holidays when they fall on weekends.
    /// If Saturday, observed on Friday. If Sunday, observed on Monday.
    /// </summary>
    private static bool IsObservedHoliday(DateTime date, DateTime holiday)
    {
        if (date.Date == holiday.Date)
        {
            return true;
        }

        // If holiday is Saturday, observe on Friday
        if (holiday.DayOfWeek == DayOfWeek.Saturday && date.Date == holiday.AddDays(-1).Date)
        {
            return true;
        }

        // If holiday is Sunday, observe on Monday
        if (holiday.DayOfWeek == DayOfWeek.Sunday && date.Date == holiday.AddDays(1).Date)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the nth occurrence of a weekday in a month.
    /// </summary>
    private static DateTime GetNthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, int n)
    {
        DateTime firstDay = new DateTime(year, month, 1);
        int daysUntilWeekday = ((int)dayOfWeek - (int)firstDay.DayOfWeek + 7) % 7;
        DateTime firstOccurrence = firstDay.AddDays(daysUntilWeekday);
        return firstOccurrence.AddDays((n - 1) * 7);
    }

    /// <summary>
    /// Gets the last occurrence of a weekday in a month.
    /// </summary>
    private static DateTime GetLastWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek)
    {
        DateTime lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        int daysBackToWeekday = ((int)lastDay.DayOfWeek - (int)dayOfWeek + 7) % 7;
        return lastDay.AddDays(-daysBackToWeekday);
    }

    /// <summary>
    /// Computes Good Friday using Meeus's algorithm for Easter.
    /// </summary>
    private static DateTime GetGoodFriday(int year)
    {
        // Meeus/Jones/Butcher algorithm for Gregorian calendar
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

        DateTime easter = new DateTime(year, month, day);
        // Good Friday is 2 days before Easter
        return easter.AddDays(-2);
    }

    /// <summary>
    /// Checks for specific National Days of Mourning when markets were closed.
    /// </summary>
    private static bool IsNationalDayOfMourning(DateTime date)
    {
        // President Ford (January 2, 2007)
        if (date == new DateTime(2007, 1, 2))
        {
            return true;
        }

        // President Reagan (June 11, 2004)
        if (date == new DateTime(2004, 6, 11))
        {
            return true;
        }

        // President Nixon (April 27, 1994)
        if (date == new DateTime(1994, 4, 27))
        {
            return true;
        }

        // President Johnson (January 25, 1973)
        if (date == new DateTime(1973, 1, 25))
        {
            return true;
        }

        // President Truman (December 28, 1972)
        if (date == new DateTime(1972, 12, 28))
        {
            return true;
        }

        // President Eisenhower (March 31, 1969)
        if (date == new DateTime(1969, 3, 31))
        {
            return true;
        }

        // President Kennedy (November 25, 1963)
        if (date == new DateTime(1963, 11, 25))
        {
            return true;
        }

        // President George H.W. Bush (December 5, 2018)
        if (date == new DateTime(2018, 12, 5))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Converts trading days to annualized time.
    /// </summary>
    /// <param name="tradingDays">Number of trading days.</param>
    /// <returns>Time in years.</returns>
    public static double TradingDaysToYears(int tradingDays)
    {
        return tradingDays / (double)TradingDaysPerYear;
    }

    /// <summary>
    /// Converts years to trading days.
    /// </summary>
    /// <param name="years">Time in years.</param>
    /// <returns>Number of trading days.</returns>
    public static int YearsToTradingDays(double years)
    {
        return (int)Math.Round(years * TradingDaysPerYear);
    }
}
