// CRTM006A.cs - Alaris Period type (time duration)
// Component ID: CRTM006A
//
// Replaces: QuantLib.Period, QuantLib.TimeUnit
//
// References:
// - QuantLib period.cpp
// - Alaris.Governance/Coding.md Rule 8 (Limited Scope)

using System.Diagnostics.CodeAnalysis;

namespace Alaris.Core.Time;

/// <summary>
/// Represents a time period with a length and unit.
/// Matches QuantLib Period semantics.
/// </summary>
public readonly struct CRTM006A : IEquatable<CRTM006A>
{
    /// <summary>
    /// Gets the length of the period.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the unit of the period.
    /// </summary>
    public CRTM006AUnit Unit { get; }

    /// <summary>
    /// Initialises a new period.
    /// </summary>
    /// <param name="length">The length of the period.</param>
    /// <param name="unit">The unit of the period.</param>
    public CRTM006A(int length, CRTM006AUnit unit)
    {
        Length = length;
        Unit = unit;
    }

    /// <summary>
    /// Creates a period of the specified number of days.
    /// </summary>
    public static CRTM006A Days(int n) => new(n, CRTM006AUnit.Days);

    /// <summary>
    /// Creates a period of the specified number of weeks.
    /// </summary>
    public static CRTM006A Weeks(int n) => new(n, CRTM006AUnit.Weeks);

    /// <summary>
    /// Creates a period of the specified number of months.
    /// </summary>
    public static CRTM006A Months(int n) => new(n, CRTM006AUnit.Months);

    /// <summary>
    /// Creates a period of the specified number of years.
    /// </summary>
    public static CRTM006A Years(int n) => new(n, CRTM006AUnit.Years);

    /// <summary>
    /// Negates the period.
    /// </summary>
    [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "Negate method provided")]
    public static CRTM006A operator -(CRTM006A period)
    {
        return Negate(period);
    }

    /// <summary>
    /// Negates the period (friendly alternate for - operator).
    /// </summary>
    public static CRTM006A Negate(CRTM006A period)
    {
        return new CRTM006A(-period.Length, period.Unit);
    }

    /// <summary>
    /// Multiplies the period by a scalar.
    /// </summary>
    [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "Multiply method provided")]
    public static CRTM006A operator *(CRTM006A period, int multiplier)
    {
        return Multiply(period, multiplier);
    }

    /// <summary>
    /// Multiplies the period by a scalar.
    /// </summary>
    [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "Multiply method provided")]
    public static CRTM006A operator *(int multiplier, CRTM006A period)
    {
        return Multiply(period, multiplier);
    }

    /// <summary>
    /// Multiplies the period by a scalar (friendly alternate for * operator).
    /// </summary>
    public static CRTM006A Multiply(CRTM006A period, int multiplier)
    {
        return new CRTM006A(period.Length * multiplier, period.Unit);
    }

    /// <inheritdoc/>
    public bool Equals(CRTM006A other)
    {
        return Length == other.Length && Unit == other.Unit;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is CRTM006A other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Length, Unit);
    }

    /// <summary>
    /// Compares two periods for equality.
    /// </summary>
    public static bool operator ==(CRTM006A left, CRTM006A right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two periods for inequality.
    /// </summary>
    public static bool operator !=(CRTM006A left, CRTM006A right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        string unitStr = Unit switch
        {
            CRTM006AUnit.Days => "D",
            CRTM006AUnit.Weeks => "W",
            CRTM006AUnit.Months => "M",
            CRTM006AUnit.Years => "Y",
            _ => "?"
        };
        return $"{Length}{unitStr}";
    }
}

/// <summary>
/// Time unit enumeration matching QuantLib TimeUnit.
/// </summary>
public enum CRTM006AUnit
{
    /// <summary>Calendar days.</summary>
    Days = 0,

    /// <summary>Weeks (7 calendar days).</summary>
    Weeks = 1,

    /// <summary>Calendar months.</summary>
    Months = 2,

    /// <summary>Calendar years.</summary>
    Years = 3
}
