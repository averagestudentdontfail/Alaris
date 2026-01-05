// CROP002A.cs - Plain vanilla option payoff
// Component ID: CROP002A
//
// Replaces: QuantLib.PlainVanillaPayoff
//
// Mathematical Specification:
// - Call payoff: max(S - K, 0)
// - Put payoff: max(K - S, 0)
//
// References:
// - Hull, J.C. "Options, Futures, and Other Derivatives" Chapter 1
// - QuantLib payoffs.cpp
// - Alaris.Governance/Coding.md Rule 5 (Zero-allocation)

namespace Alaris.Core.Options;

/// <summary>
/// Plain vanilla option payoff (European/American style).
/// Replaces QuantLib PlainVanillaPayoff.
/// </summary>
/// <remarks>
/// This is a value type for zero-allocation operations in hot paths.
/// </remarks>
public readonly struct VanillaPayoff : IEquatable<VanillaPayoff>
{
    /// <summary>
    /// Initialises a new vanilla payoff.
    /// </summary>
    /// <param name="optionType">Call or Put.</param>
    /// <param name="strike">Strike price.</param>
    public VanillaPayoff(OptionType optionType, double strike)
    {
        if (!double.IsFinite(strike))
        {
            throw new ArgumentOutOfRangeException(nameof(strike), "Strike must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike, nameof(strike));

        Type = optionType;
        Strike = strike;
    }

    /// <summary>
    /// Gets the option type (Call or Put).
    /// </summary>
    public OptionType Type { get; }

    /// <summary>
    /// Gets the strike price.
    /// </summary>
    public double Strike { get; }

    /// <summary>
    /// Calculates the intrinsic value (payoff at expiry).
    /// </summary>
    /// <param name="spot">Spot price at expiry.</param>
    /// <returns>Payoff value (always non-negative).</returns>
    public double Payoff(double spot)
    {
        return Type switch
        {
            OptionType.Call => System.Math.Max(spot - Strike, 0.0),
            OptionType.Put => System.Math.Max(Strike - spot, 0.0),
            _ => throw new InvalidOperationException($"Unknown option type: {Type}.")
        };
    }

    /// <summary>
    /// Calculates the intrinsic value using the sign-based formula.
    /// Useful for unified call/put algorithms.
    /// </summary>
    /// <param name="spot">Spot price at expiry.</param>
    /// <returns>Payoff value (always non-negative).</returns>
    public double PayoffSigned(double spot)
    {
        int sign = Type.Sign();
        return System.Math.Max((spot - Strike) * sign, 0.0);
    }

    /// <inheritdoc/>
    public bool Equals(VanillaPayoff other)
    {
        return Type == other.Type && Strike.Equals(other.Strike);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is VanillaPayoff other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Strike);
    }

    /// <summary>
    /// Compares two payoffs for equality.
    /// </summary>
    public static bool operator ==(VanillaPayoff left, VanillaPayoff right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two payoffs for inequality.
    /// </summary>
    public static bool operator !=(VanillaPayoff left, VanillaPayoff right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{Type} @ {Strike:F2}";
    }
}
