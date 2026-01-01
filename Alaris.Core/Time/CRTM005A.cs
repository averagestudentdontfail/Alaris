// CRTM005A.cs - Alaris Date type (QuantLib-compatible serial number based date)
// Component ID: CRTM005A
//
// Replaces: QuantLib.Date
//
// Mathematical Specification:
// - Serial number = days since December 31, 1899 (QuantLib convention)
// - Serial number 1 = January 1, 1900
// - AddMonths preserves day-of-month, capped at end-of-month
//
// References:
// - QuantLib date.cpp serial number calculation
// - Alaris.Governance/Coding.md Rule 8 (Limited Scope)

using System.Diagnostics.CodeAnalysis;

namespace Alaris.Core.Time;

/// <summary>
/// High-performance date type with serial number representation.
/// Matches QuantLib Date semantics for binary compatibility.
/// </summary>
/// <remarks>
/// This struct replaces QuantLib.Date with a native implementation that:
/// - Uses the same serial number epoch (Dec 31, 1899 = 0)
/// - Provides identical date arithmetic behaviour
/// - Is a value type for zero-allocation operations
/// </remarks>
public readonly struct CRTM005A : IEquatable<CRTM005A>, IComparable<CRTM005A>
{
    // QuantLib epoch: December 31, 1899 (serial 0)
    // January 1, 1900 = serial 1
    private static readonly DateTime Epoch = new(1899, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets the serial number (days since December 31, 1899).
    /// </summary>
    public int SerialNumber { get; }

    /// <summary>
    /// Gets the day of the month (1-31).
    /// </summary>
    public int Day => ToDateTime().Day;

    /// <summary>
    /// Gets the month (1-12).
    /// </summary>
    public int Month => ToDateTime().Month;

    /// <summary>
    /// Gets the year.
    /// </summary>
    public int Year => ToDateTime().Year;

    /// <summary>
    /// Gets the day of the week.
    /// </summary>
    public DayOfWeek DayOfWeek => ToDateTime().DayOfWeek;

    /// <summary>
    /// Gets the day of the year (1-366).
    /// </summary>
    public int DayOfYear => ToDateTime().DayOfYear;

    /// <summary>
    /// Initialises a new date from a serial number.
    /// </summary>
    /// <param name="serialNumber">Days since December 31, 1899.</param>
    public CRTM005A(int serialNumber)
    {
        SerialNumber = serialNumber;
    }

    /// <summary>
    /// Initialises a new date from day, month, year components.
    /// </summary>
    /// <param name="day">Day of the month (1-31).</param>
    /// <param name="month">Month (1-12).</param>
    /// <param name="year">Year.</param>
    public CRTM005A(int day, int month, int year)
    {
        DateTime date = new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        SerialNumber = (int)(date - Epoch).TotalDays;
    }

    /// <summary>
    /// Initialises a new date from day, month enum, year components.
    /// Provides compatibility with QuantLib Month enum pattern.
    /// </summary>
    /// <param name="day">Day of the month (1-31).</param>
    /// <param name="month">Month enum value.</param>
    /// <param name="year">Year.</param>
    public CRTM005A(int day, CRTM005AMonth month, int year)
        : this(day, (int)month, year)
    {
    }

    /// <summary>
    /// Creates an Alaris date from a .NET DateTime.
    /// </summary>
    /// <param name="dateTime">The DateTime to convert.</param>
    /// <returns>The corresponding Alaris date.</returns>
    public static CRTM005A FromDateTime(DateTime dateTime)
    {
        int serialNumber = (int)(dateTime.Date - Epoch).TotalDays;
        return new CRTM005A(serialNumber);
    }

    /// <summary>
    /// Converts this date to a .NET DateTime (UTC midnight).
    /// </summary>
    /// <returns>DateTime representation.</returns>
    public DateTime ToDateTime()
    {
        return Epoch.AddDays(SerialNumber);
    }

    /// <summary>
    /// Adds the specified number of days to this date.
    /// </summary>
    /// <param name="days">Number of days to add (can be negative).</param>
    /// <returns>A new date offset by the specified days.</returns>
    public CRTM005A AddDays(int days)
    {
        return new CRTM005A(SerialNumber + days);
    }

    /// <summary>
    /// Adds the specified number of months to this date.
    /// Preserves day-of-month, capping at end-of-month if necessary.
    /// </summary>
    /// <param name="months">Number of months to add (can be negative).</param>
    /// <returns>A new date offset by the specified months.</returns>
    public CRTM005A AddMonths(int months)
    {
        DateTime current = ToDateTime();
        int targetMonth = current.Month + months;
        int targetYear = current.Year + ((targetMonth - 1) / 12);

        // Handle negative month values
        if (targetMonth <= 0)
        {
            targetYear += (targetMonth / 12) - 1;
            targetMonth = 12 + (targetMonth % 12);
            if (targetMonth == 0)
            {
                targetMonth = 12;
            }
        }
        else
        {
            targetMonth = ((targetMonth - 1) % 12) + 1;
        }

        // Cap day at end of month
        int daysInTargetMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        int targetDay = System.Math.Min(current.Day, daysInTargetMonth);

        return new CRTM005A(targetDay, targetMonth, targetYear);
    }

    /// <summary>
    /// Adds the specified number of years to this date.
    /// </summary>
    /// <param name="years">Number of years to add (can be negative).</param>
    /// <returns>A new date offset by the specified years.</returns>
    public CRTM005A AddYears(int years)
    {
        DateTime current = ToDateTime();
        int targetYear = current.Year + years;

        // Handle Feb 29 on non-leap years
        int targetDay = current.Day;
        if (current.Month == 2 && current.Day == 29 && !DateTime.IsLeapYear(targetYear))
        {
            targetDay = 28;
        }

        return new CRTM005A(targetDay, current.Month, targetYear);
    }

    /// <summary>
    /// Calculates the number of days between two dates.
    /// </summary>
    /// <param name="left">Start date.</param>
    /// <param name="right">End date.</param>
    /// <returns>Number of days (left - right).</returns>
    public static int operator -(CRTM005A left, CRTM005A right)
    {
        return left.SerialNumber - right.SerialNumber;
    }

    /// <summary>
    /// Adds a period to a date.
    /// </summary>
    [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "Add method provided")]
    public static CRTM005A operator +(CRTM005A date, CRTM006A period)
    {
        return Add(date, period);
    }

    /// <summary>
    /// Adds a period to a date (friendly alternate for + operator).
    /// </summary>
    /// <param name="date">The base date.</param>
    /// <param name="period">The period to add.</param>
    /// <returns>A new date offset by the period.</returns>
    public static CRTM005A Add(CRTM005A date, CRTM006A period)
    {
        return period.Unit switch
        {
            CRTM006AUnit.Days => date.AddDays(period.Length),
            CRTM006AUnit.Weeks => date.AddDays(period.Length * 7),
            CRTM006AUnit.Months => date.AddMonths(period.Length),
            CRTM006AUnit.Years => date.AddYears(period.Length),
            _ => throw new InvalidOperationException($"Unknown period unit: {period.Unit}")
        };
    }

    /// <summary>
    /// Calculates the difference between two dates in days (friendly alternate for - operator).
    /// </summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Number of days (left - right).</returns>
    public static int Subtract(CRTM005A left, CRTM005A right)
    {
        return left.SerialNumber - right.SerialNumber;
    }

    /// <summary>
    /// Compares two dates for equality.
    /// </summary>
    public static bool operator ==(CRTM005A left, CRTM005A right)
    {
        return left.SerialNumber == right.SerialNumber;
    }

    /// <summary>
    /// Compares two dates for inequality.
    /// </summary>
    public static bool operator !=(CRTM005A left, CRTM005A right)
    {
        return left.SerialNumber != right.SerialNumber;
    }

    /// <summary>
    /// Determines if the left date is before the right date.
    /// </summary>
    public static bool operator <(CRTM005A left, CRTM005A right)
    {
        return left.SerialNumber < right.SerialNumber;
    }

    /// <summary>
    /// Determines if the left date is before or equal to the right date.
    /// </summary>
    public static bool operator <=(CRTM005A left, CRTM005A right)
    {
        return left.SerialNumber <= right.SerialNumber;
    }

    /// <summary>
    /// Determines if the left date is after the right date.
    /// </summary>
    public static bool operator >(CRTM005A left, CRTM005A right)
    {
        return left.SerialNumber > right.SerialNumber;
    }

    /// <summary>
    /// Determines if the left date is after or equal to the right date.
    /// </summary>
    public static bool operator >=(CRTM005A left, CRTM005A right)
    {
        return left.SerialNumber >= right.SerialNumber;
    }

    /// <inheritdoc/>
    public bool Equals(CRTM005A other)
    {
        return SerialNumber == other.SerialNumber;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is CRTM005A other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return SerialNumber;
    }

    /// <inheritdoc/>
    public int CompareTo(CRTM005A other)
    {
        return SerialNumber.CompareTo(other.SerialNumber);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        DateTime dt = ToDateTime();
        return $"{dt:yyyy-MM-dd}";
    }

    /// <summary>
    /// Returns an ISO 8601 formatted string.
    /// </summary>
    public string ToIsoString()
    {
        return ToString();
    }
}

/// <summary>
/// Month enumeration matching QuantLib Month enum values.
/// </summary>
public enum CRTM005AMonth
{
    /// <summary>No month specified (invalid/default).</summary>
    None = 0,

    /// <summary>January.</summary>
    January = 1,

    /// <summary>February.</summary>
    February = 2,

    /// <summary>March.</summary>
    March = 3,

    /// <summary>April.</summary>
    April = 4,

    /// <summary>May.</summary>
    May = 5,

    /// <summary>June.</summary>
    June = 6,

    /// <summary>July.</summary>
    July = 7,

    /// <summary>August.</summary>
    August = 8,

    /// <summary>September.</summary>
    September = 9,

    /// <summary>October.</summary>
    October = 10,

    /// <summary>November.</summary>
    November = 11,

    /// <summary>December.</summary>
    December = 12
}
