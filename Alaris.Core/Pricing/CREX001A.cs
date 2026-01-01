// CREX001A.cs - Near-expiry numerical stability with intrinsic value blending
// Component ID: CREX001A
// Migrated from: Alaris.Double.DBEX001A

using System;

namespace Alaris.Core.Pricing;

/// <summary>
/// Handles numerical stability as τ → 0 via blending to intrinsic. Threshold: τ &lt; 1/252.
/// </summary>
public sealed class CREX001A
{
    /// <summary>
    /// Minimum time-to-expiry threshold (in years) before near-expiry regime.
    /// Default: 1/252 ≈ 0.00397 (1 trading day).
    /// </summary>
    public const double DefaultMinTimeToExpiry = 1.0 / 252.0;

    /// <summary>
    /// Blending zone width (in years) for smooth transition.
    /// Default: 2/252 ≈ 0.00794 (2 trading days).
    /// </summary>
    public const double DefaultBlendingZoneWidth = 2.0 / 252.0;

    private readonly double _minTimeToExpiry;
    private readonly double _blendingZoneWidth;

    /// <summary>
    /// Initialises a new instance of the near-expiry handler.
    /// </summary>
    /// <param name="minTimeToExpiry">Minimum time-to-expiry threshold (years).</param>
    /// <param name="blendingZoneWidth">Width of blending zone (years).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when thresholds are non-positive.
    /// </exception>
    public CREX001A(
        double minTimeToExpiry = DefaultMinTimeToExpiry,
        double blendingZoneWidth = DefaultBlendingZoneWidth)
    {
        if (minTimeToExpiry <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minTimeToExpiry),
                "Minimum time-to-expiry must be positive");
        }

        if (blendingZoneWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blendingZoneWidth),
                "Blending zone width must be positive");
        }

        _minTimeToExpiry = minTimeToExpiry;
        _blendingZoneWidth = blendingZoneWidth;
    }

    /// <summary>
    /// Determines if the option is in the near-expiry regime.
    /// </summary>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <returns>True if near-expiry handling should be applied.</returns>
    public bool IsNearExpiry(double timeToExpiry)
    {
        return timeToExpiry <= _minTimeToExpiry + _blendingZoneWidth;
    }

    /// <summary>
    /// Determines if the option is fully in intrinsic-value regime.
    /// </summary>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <returns>True if only intrinsic value should be used.</returns>
    public bool IsIntrinsicOnly(double timeToExpiry)
    {
        return timeToExpiry <= _minTimeToExpiry;
    }

    /// <summary>
    /// Calculates call option intrinsic value.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <returns>Intrinsic value: max(S - K, 0).</returns>
    public static double CalculateCallIntrinsic(double spot, double strike)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);

        return System.Math.Max(spot - strike, 0);
    }

    /// <summary>
    /// Calculates put option intrinsic value.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <returns>Intrinsic value: max(K - S, 0).</returns>
    public static double CalculatePutIntrinsic(double spot, double strike)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);

        return System.Math.Max(strike - spot, 0);
    }

    /// <summary>
    /// Calculates intrinsic value for either call or put.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <returns>Intrinsic value.</returns>
    public static double CalculateIntrinsic(double spot, double strike, bool isCall)
    {
        return isCall
            ? CalculateCallIntrinsic(spot, strike)
            : CalculatePutIntrinsic(spot, strike);
    }

    /// <summary>
    /// Calculates the blending weight for smooth transition.
    /// </summary>
    /// <remarks>
    /// The weight function provides C⁰ continuity:
    /// <code>
    /// w(τ) = clamp((τ - ε_min) / zone_width, 0, 1)
    /// </code>
    /// At τ = ε_min: w = 0 (pure intrinsic)
    /// At τ = ε_min + zone: w = 1 (pure model)
    /// </remarks>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <returns>Blending weight in [0, 1].</returns>
    public double CalculateBlendingWeight(double timeToExpiry)
    {
        if (timeToExpiry <= _minTimeToExpiry)
        {
            return 0.0;
        }

        if (timeToExpiry >= _minTimeToExpiry + _blendingZoneWidth)
        {
            return 1.0;
        }

        // Linear interpolation in blending zone
        return (timeToExpiry - _minTimeToExpiry) / _blendingZoneWidth;
    }

    /// <summary>
    /// Blends model value with intrinsic value for smooth near-expiry transition.
    /// </summary>
    /// <param name="modelValue">Value from pricing model (Black-Scholes/spectral).</param>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <returns>Blended option value.</returns>
    public double BlendWithIntrinsic(
        double modelValue,
        double spot,
        double strike,
        bool isCall,
        double timeToExpiry)
    {
        if (modelValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modelValue),
                "Model value cannot be negative");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);

        double intrinsicValue = CalculateIntrinsic(spot, strike, isCall);
        double weight = CalculateBlendingWeight(timeToExpiry);

        double blendedValue = (weight * modelValue) + ((1 - weight) * intrinsicValue);

        // Ensure value is at least intrinsic (no arbitrage)
        blendedValue = System.Math.Max(blendedValue, intrinsicValue);

        return blendedValue;
    }

    /// <summary>
    /// Returns near-expiry Greeks with appropriate limiting behaviour.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <returns>Near-expiry Greeks result.</returns>
    public NearExpiryGreeks CalculateNearExpiryGreeks(
        double spot,
        double strike,
        bool isCall,
        double timeToExpiry)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);

        double moneyness = spot / strike;
        bool isITM = isCall ? (moneyness > 1.0) : (moneyness < 1.0);
        bool isATM = System.Math.Abs(moneyness - 1.0) < 0.01; // Within 1%

        // Delta approaches step function at expiry
        double delta;
        if (isCall)
        {
            delta = isITM ? 1.0 : (isATM ? 0.5 : 0.0);
        }
        else
        {
            delta = isITM ? -1.0 : (isATM ? -0.5 : 0.0);
        }

        // Gamma is zero except at-the-money where it explodes
        double gamma;
        if (isATM && timeToExpiry > 0)
        {
            const double VolEstimate = 0.30;
            gamma = System.Math.Min(1.0 / (spot * VolEstimate * System.Math.Sqrt(timeToExpiry)), 100.0 / spot);
        }
        else
        {
            gamma = 0.0;
        }

        // Vega approaches zero
        double vega = 0.0;

        // Theta: rate of time value decay
        double intrinsic = CalculateIntrinsic(spot, strike, isCall);
        double theta = timeToExpiry > 0 ? -intrinsic / (timeToExpiry * 252.0) : 0.0;

        return new NearExpiryGreeks
        {
            Delta = delta,
            Gamma = gamma,
            Vega = vega,
            Theta = theta,
            IsAtMoney = isATM,
            IsNearExpiry = IsNearExpiry(timeToExpiry),
            TimeToExpiry = timeToExpiry
        };
    }

    /// <summary>
    /// Validates that a time-to-expiry value is suitable for model pricing.
    /// </summary>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <returns>Validation result with recommendation.</returns>
    public NearExpiryValidation Validate(double timeToExpiry)
    {
        if (timeToExpiry <= 0)
        {
            return new NearExpiryValidation
            {
                IsValid = false,
                Recommendation = NearExpiryRecommendation.UseIntrinsic,
                Reason = "Expired or invalid time-to-expiry",
                TimeToExpiry = timeToExpiry
            };
        }

        if (timeToExpiry <= _minTimeToExpiry)
        {
            return new NearExpiryValidation
            {
                IsValid = true,
                Recommendation = NearExpiryRecommendation.UseIntrinsic,
                Reason = $"Below minimum threshold ({_minTimeToExpiry:F4}y)",
                TimeToExpiry = timeToExpiry
            };
        }

        if (timeToExpiry <= _minTimeToExpiry + _blendingZoneWidth)
        {
            return new NearExpiryValidation
            {
                IsValid = true,
                Recommendation = NearExpiryRecommendation.UseBlended,
                Reason = "In blending zone - use weighted combination",
                BlendingWeight = CalculateBlendingWeight(timeToExpiry),
                TimeToExpiry = timeToExpiry
            };
        }

        return new NearExpiryValidation
        {
            IsValid = true,
            Recommendation = NearExpiryRecommendation.UseModel,
            Reason = "Normal regime - full model pricing applicable",
            BlendingWeight = 1.0,
            TimeToExpiry = timeToExpiry
        };
    }
}

/// <summary>
/// Greeks calculated for near-expiry options with limiting behaviour.
/// </summary>
public sealed record NearExpiryGreeks
{
    /// <summary>Rate of change with respect to spot price.</summary>
    public required double Delta { get; init; }

    /// <summary>Rate of change of delta (capped for ATM stability).</summary>
    public required double Gamma { get; init; }

    /// <summary>Sensitivity to volatility (approaches zero near expiry).</summary>
    public required double Vega { get; init; }

    /// <summary>Time decay rate.</summary>
    public required double Theta { get; init; }

    /// <summary>Whether option is at-the-money (|S/K - 1| &lt; 1%).</summary>
    public required bool IsAtMoney { get; init; }

    /// <summary>Whether option is in near-expiry regime.</summary>
    public required bool IsNearExpiry { get; init; }

    /// <summary>Time to expiration in years.</summary>
    public required double TimeToExpiry { get; init; }
}

/// <summary>
/// Validation result for near-expiry time-to-expiry values.
/// </summary>
public sealed record NearExpiryValidation
{
    /// <summary>Whether the time-to-expiry is valid for pricing.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Recommended pricing approach.</summary>
    public required NearExpiryRecommendation Recommendation { get; init; }

    /// <summary>Human-readable explanation.</summary>
    public required string Reason { get; init; }

    /// <summary>Blending weight if in blending zone.</summary>
    public double BlendingWeight { get; init; }

    /// <summary>Time to expiration in years.</summary>
    public required double TimeToExpiry { get; init; }
}

/// <summary>
/// Recommended pricing approach for near-expiry options.
/// </summary>
public enum NearExpiryRecommendation
{
    /// <summary>Use full model pricing (normal regime).</summary>
    UseModel = 0,

    /// <summary>Use blended model/intrinsic value.</summary>
    UseBlended = 1,

    /// <summary>Use intrinsic value only (very near expiry).</summary>
    UseIntrinsic = 2
}
