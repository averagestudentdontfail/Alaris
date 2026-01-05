// CROP003A.cs - American exercise specification
// Component ID: CROP003A
//
// Replaces: QuantLib.AmericanExercise
//
// Design:
// - Specifies the exercise window for American-style options
// - EarliestDate is typically today (spot start American)
// - LatestDate is the expiry date
//
// References:
// - QuantLib exercise.hpp
// - Alaris.Governance/Coding.md Rule 5 (Zero-allocation)

using Alaris.Core.Time;

namespace Alaris.Core.Options;

/// <summary>
/// American exercise specification.
/// Replaces QuantLib AmericanExercise.
/// </summary>
/// <remarks>
/// American options can be exercised at any time between the earliest
/// and latest exercise dates. For standard American options:
/// - EarliestDate = valuation date (immediate exercise allowed)
/// - LatestDate = expiration date
/// </remarks>
public readonly struct AmericanExercise : IEquatable<AmericanExercise>
{
    private const int UnsetSerialNumber = 0;

    /// <summary>
    /// Initialises American exercise with immediate exercise allowed.
    /// </summary>
    /// <param name="latestDate">Expiration date.</param>
    public AmericanExercise(CRTM005A latestDate)
    {
        EarliestDate = default; // Will be set to valuation date during pricing
        LatestDate = latestDate;
    }

    /// <summary>
    /// Initialises American exercise with a specific exercise window.
    /// </summary>
    /// <param name="earliestDate">First date exercise is allowed.</param>
    /// <param name="latestDate">Last date exercise is allowed (expiration).</param>
    public AmericanExercise(CRTM005A earliestDate, CRTM005A latestDate)
    {
        if (latestDate < earliestDate)
        {
            throw new ArgumentException("Latest date must be >= earliest date");
        }

        EarliestDate = earliestDate;
        LatestDate = latestDate;
    }

    /// <summary>
    /// Gets the earliest date exercise is allowed.
    /// </summary>
    public CRTM005A EarliestDate { get; }

    /// <summary>
    /// Gets the latest date exercise is allowed (expiration).
    /// </summary>
    public CRTM005A LatestDate { get; }

    /// <summary>
    /// Determines if early exercise is allowed (always true for American).
    /// </summary>
    public bool IsEarlyExerciseAllowed => true;

    /// <summary>
    /// Determines if a given date is within the exercise window.
    /// </summary>
    /// <param name="date">Date to check.</param>
    /// <returns>True if exercise is allowed on this date.</returns>
    public bool CanExercise(CRTM005A date)
    {
        if (EarliestDate.SerialNumber == UnsetSerialNumber)
        {
            return date <= LatestDate;
        }
        return date >= EarliestDate && date <= LatestDate;
    }

    /// <inheritdoc/>
    public bool Equals(AmericanExercise other)
    {
        return EarliestDate == other.EarliestDate && LatestDate == other.LatestDate;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is AmericanExercise other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(EarliestDate, LatestDate);
    }

    /// <summary>
    /// Compares two exercise specifications for equality.
    /// </summary>
    public static bool operator ==(AmericanExercise left, AmericanExercise right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two exercise specifications for inequality.
    /// </summary>
    public static bool operator !=(AmericanExercise left, AmericanExercise right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (EarliestDate.SerialNumber == UnsetSerialNumber)
        {
            return $"American exercise until {LatestDate}";
        }
        return $"American exercise from {EarliestDate} to {LatestDate}";
    }
}
