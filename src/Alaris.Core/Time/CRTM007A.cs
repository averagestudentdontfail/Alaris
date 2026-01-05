// CRTM007A.cs - Day count conventions
// Component ID: CRTM007A
//
// Replaces: QuantLib.Actual365Fixed, QuantLib.Actual360, QuantLib.Thirty360
//
// Day Count Conventions:
// - Actual/365 Fixed: (end - start) / 365
// - Actual/360: (end - start) / 360
// - 30/360: ISDA 30/360 convention
//
// References:
// - ISDA 2006 Definitions Section 4.16
// - QuantLib daycounter.cpp
// - Alaris.Governance/Coding.md Rule 8 (Limited Scope)

using System.Diagnostics.CodeAnalysis;

namespace Alaris.Core.Time;

/// <summary>
/// Interface for day count conventions.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Domain-specific financial terminology")]
public interface IDayCounter
{
    /// <summary>
    /// Gets the name of the day count convention.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Calculates the year fraction between two dates.
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date.</param>
    /// <returns>Year fraction.</returns>
    public double YearFraction(CRTM005A start, CRTM005A end);

    /// <summary>
    /// Calculates the number of days between two dates per convention.
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date.</param>
    /// <returns>Number of days per the day count convention.</returns>
    public int DayCount(CRTM005A start, CRTM005A end);
}

/// <summary>
/// Actual/365 Fixed day count convention.
/// This is the most common convention for equity options.
/// </summary>
public sealed class CRTM007AActual365Fixed : IDayCounter
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly CRTM007AActual365Fixed Instance = new();

    /// <inheritdoc/>
    public string Name => "Actual/365 (Fixed)";

    /// <inheritdoc/>
    public double YearFraction(CRTM005A start, CRTM005A end)
    {
        return DayCount(start, end) / 365.0;
    }

    /// <inheritdoc/>
    public int DayCount(CRTM005A start, CRTM005A end)
    {
        return end - start;
    }
}

/// <summary>
/// Actual/360 day count convention.
/// Common for money market instruments.
/// </summary>
public sealed class CRTM007AActual360 : IDayCounter
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly CRTM007AActual360 Instance = new();

    /// <inheritdoc/>
    public string Name => "Actual/360";

    /// <inheritdoc/>
    public double YearFraction(CRTM005A start, CRTM005A end)
    {
        return DayCount(start, end) / 360.0;
    }

    /// <inheritdoc/>
    public int DayCount(CRTM005A start, CRTM005A end)
    {
        return end - start;
    }
}

/// <summary>
/// 30/360 (ISDA) day count convention.
/// Used for some corporate bonds.
/// </summary>
public sealed class CRTM007AThirty360 : IDayCounter
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly CRTM007AThirty360 Instance = new();

    /// <inheritdoc/>
    public string Name => "30/360 (ISDA)";

    /// <inheritdoc/>
    public double YearFraction(CRTM005A start, CRTM005A end)
    {
        return DayCount(start, end) / 360.0;
    }

    /// <inheritdoc/>
    public int DayCount(CRTM005A start, CRTM005A end)
    {
        // ISDA 30/360 convention
        int d1 = start.Day;
        int d2 = end.Day;
        int m1 = start.Month;
        int m2 = end.Month;
        int y1 = start.Year;
        int y2 = end.Year;

        // Adjust day values per ISDA rules
        if (d1 == 31)
        {
            d1 = 30;
        }

        if (d2 == 31 && d1 >= 30)
        {
            d2 = 30;
        }

        return (360 * (y2 - y1)) + (30 * (m2 - m1)) + (d2 - d1);
    }
}

/// <summary>
/// Factory for commonly used day counters.
/// </summary>
public static class DayCounters
{
    /// <summary>
    /// Actual/365 Fixed (standard for equity options).
    /// </summary>
    public static IDayCounter Actual365Fixed => CRTM007AActual365Fixed.Instance;

    /// <summary>
    /// Actual/360 (money market).
    /// </summary>
    public static IDayCounter Actual360 => CRTM007AActual360.Instance;

    /// <summary>
    /// 30/360 ISDA (corporate bonds).
    /// </summary>
    public static IDayCounter Thirty360 => CRTM007AThirty360.Instance;
}
