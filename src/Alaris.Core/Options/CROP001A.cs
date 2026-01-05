// CROP001A.cs - Option type enumeration
// Component ID: CROP001A
//
// Replaces: QuantLib.Option.Type
//
// Design Decision:
// - Call = +1 for payoff calculation: (S - K) * sign
// - Put = -1 for payoff calculation: (K - S) * sign
//
// References:
// - QuantLib payoffs.hpp
// - Alaris.Governance/Coding.md Rule 8 (Limited Scope)

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Alaris.Core.Options;

/// <summary>
/// Option type enumeration matching QuantLib Option.Type semantics.
/// </summary>
/// <remarks>
/// Values are +1 for Call and -1 for Put to enable elegant payoff calculations:
/// - Call payoff = max(S - K, 0) = max((S - K) * 1, 0)
/// - Put payoff = max(K - S, 0) = max((S - K) * -1, 0) * -1
/// </remarks>
[SuppressMessage("Naming", "CA1008:Enums should have zero value", Justification = "Option type cannot have a zero/none value by design")]
public enum OptionType
{
    /// <summary>
    /// Call option: right to buy at strike.
    /// </summary>
    Call = 1,

    /// <summary>
    /// Put option: right to sell at strike.
    /// </summary>
    Put = -1
}

/// <summary>
/// Extension methods for OptionType.
/// </summary>
public static class OptionTypeExtensions
{
    /// <summary>
    /// Gets the sign multiplier for payoff calculations.
    /// </summary>
    /// <param name="optionType">The option type.</param>
    /// <returns>+1 for Call, -1 for Put.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(this OptionType optionType)
    {
        return optionType switch
        {
            OptionType.Call => 1,
            OptionType.Put => -1,
            _ => throw new ArgumentOutOfRangeException(nameof(optionType), optionType, "Unknown option type.")
        };
    }

    /// <summary>
    /// Determines if this is a call option.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCall(this OptionType optionType)
    {
        return optionType == OptionType.Call;
    }

    /// <summary>
    /// Determines if this is a put option.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPut(this OptionType optionType)
    {
        return optionType == OptionType.Put;
    }
}
