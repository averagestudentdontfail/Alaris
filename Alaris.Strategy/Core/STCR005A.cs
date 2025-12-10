// =============================================================================
// STCR005A.cs - Signal Freshness Monitor
// Component: STCR005A | Category: Core | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.2.7
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Monitors signal freshness and detects staleness requiring revalidation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mathematical Foundation</b>
/// </para>
/// 
/// <para>
/// Market conditions evolve over time. Signal staleness follows exponential decay:
/// <code>
/// Freshness(t) = e^(-λ × (t - t_signal))
/// </code>
/// where λ is the decay rate (default: λ = ln(2)/60 for 60-minute half-life).
/// </para>
/// 
/// <para>
/// <b>Staleness Threshold</b>
/// </para>
/// 
/// <para>
/// A signal requires revalidation when Freshness &lt; 0.5 (default).
/// This corresponds to the half-life of the signal.
/// </para>
/// </remarks>
public sealed class STCR005A
{
    private readonly ILogger<STCR005A>? _logger;
    private readonly double _halfLifeMinutes;
    private readonly double _decayRate;

    /// <summary>
    /// Default half-life in minutes (60 = 1 hour).
    /// </summary>
    public const double DefaultHalfLifeMinutes = 60.0;

    /// <summary>
    /// Default freshness threshold for revalidation.
    /// </summary>
    public const double DefaultFreshnessThreshold = 0.5;

    // LoggerMessage delegates
    private static readonly Action<ILogger, double, double, Exception?> LogStaleSignal =
        LoggerMessage.Define<double, double>(
            LogLevel.Warning,
            new EventId(1, nameof(LogStaleSignal)),
            "Signal stale: freshness={Freshness:F3}, age={AgeMinutes:F1}min");

    /// <summary>
    /// Initialises a new instance of the signal freshness monitor.
    /// </summary>
    /// <param name="halfLifeMinutes">Signal half-life in minutes.</param>
    /// <param name="logger">Optional logger instance.</param>
    public STCR005A(
        double halfLifeMinutes = DefaultHalfLifeMinutes,
        ILogger<STCR005A>? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(halfLifeMinutes);

        _halfLifeMinutes = halfLifeMinutes;
        _decayRate = Math.Log(2) / halfLifeMinutes;
        _logger = logger;
    }

    /// <summary>
    /// Calculates the freshness score of a signal.
    /// </summary>
    /// <remarks>
    /// Freshness = e^(-λ × age) where λ = ln(2)/halfLife.
    /// At age = halfLife, freshness = 0.5.
    /// </remarks>
    /// <param name="signalTime">Time when signal was generated.</param>
    /// <param name="currentTime">Current time.</param>
    /// <returns>Freshness score in [0, 1].</returns>
    public double CalculateFreshness(DateTime signalTime, DateTime currentTime)
    {
        if (currentTime < signalTime)
        {
            return 1.0; // Signal is from the future (clock issue) - treat as fresh
        }

        double ageMinutes = (currentTime - signalTime).TotalMinutes;
        return Math.Exp(-_decayRate * ageMinutes);
    }

    /// <summary>
    /// Checks if a signal requires revalidation due to staleness.
    /// </summary>
    /// <param name="signalTime">Time when signal was generated.</param>
    /// <param name="currentTime">Current time.</param>
    /// <param name="freshnessThreshold">Threshold below which revalidation is needed.</param>
    /// <returns>True if signal should be revalidated.</returns>
    public bool RequiresRevalidation(
        DateTime signalTime,
        DateTime currentTime,
        double freshnessThreshold = DefaultFreshnessThreshold)
    {
        double freshness = CalculateFreshness(signalTime, currentTime);
        bool requiresRevalidation = freshness < freshnessThreshold;

        if (requiresRevalidation)
        {
            double ageMinutes = (currentTime - signalTime).TotalMinutes;
            SafeLog(() => LogStaleSignal(_logger!, freshness, ageMinutes, null));
        }

        return requiresRevalidation;
    }

    /// <summary>
    /// Calculates the time remaining until a signal becomes stale.
    /// </summary>
    /// <param name="signalTime">Time when signal was generated.</param>
    /// <param name="currentTime">Current time.</param>
    /// <param name="freshnessThreshold">Threshold for staleness.</param>
    /// <returns>Time until stale (negative if already stale).</returns>
    public TimeSpan TimeUntilStale(
        DateTime signalTime,
        DateTime currentTime,
        double freshnessThreshold = DefaultFreshnessThreshold)
    {
        // Solve: e^(-λ×t) = threshold → t = -ln(threshold)/λ
        double staleAgeMinutes = -Math.Log(freshnessThreshold) / _decayRate;
        double currentAgeMinutes = (currentTime - signalTime).TotalMinutes;
        double remainingMinutes = staleAgeMinutes - currentAgeMinutes;

        return TimeSpan.FromMinutes(remainingMinutes);
    }

    /// <summary>
    /// Evaluates signal freshness and provides detailed result.
    /// </summary>
    /// <param name="signalTime">Time when signal was generated.</param>
    /// <param name="currentTime">Current time.</param>
    /// <param name="freshnessThreshold">Threshold for revalidation.</param>
    /// <returns>Freshness evaluation result.</returns>
    public STCR006A Evaluate(
        DateTime signalTime,
        DateTime currentTime,
        double freshnessThreshold = DefaultFreshnessThreshold)
    {
        double freshness = CalculateFreshness(signalTime, currentTime);
        TimeSpan age = currentTime - signalTime;
        TimeSpan timeUntilStale = TimeUntilStale(signalTime, currentTime, freshnessThreshold);
        bool isStale = freshness < freshnessThreshold;

        STCR007A status;
        if (freshness >= 0.8)
        {
            status = STCR007A.Fresh;
        }
        else if (freshness >= freshnessThreshold)
        {
            status = STCR007A.Aging;
        }
        else if (freshness >= 0.25)
        {
            status = STCR007A.Stale;
        }
        else
        {
            status = STCR007A.Expired;
        }

        return new STCR006A
        {
            Freshness = freshness,
            Age = age,
            TimeUntilStale = timeUntilStale,
            RequiresRevalidation = isStale,
            Status = status,
            HalfLifeMinutes = _halfLifeMinutes,
            Rationale = status switch
            {
                STCR007A.Fresh => $"Signal is fresh ({age.TotalMinutes:F0}min old)",
                STCR007A.Aging => $"Signal aging ({age.TotalMinutes:F0}min old, {timeUntilStale.TotalMinutes:F0}min until stale)",
                STCR007A.Stale => $"Signal stale ({age.TotalMinutes:F0}min old) - revalidate before use",
                _ => $"Signal expired ({age.TotalMinutes:F0}min old) - must revalidate"
            }
        };
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation.
    /// </summary>
    private void SafeLog(Action logAction)
    {
        if (_logger == null)
        {
            return;
        }

#pragma warning disable CA1031
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Swallow logging exceptions
        }
#pragma warning restore CA1031
    }
}

// =============================================================================
// Supporting Types
// =============================================================================

/// <summary>
/// Signal freshness evaluation result.
/// </summary>
public sealed record STCR006A
{
    /// <summary>Freshness score (0-1).</summary>
    public required double Freshness { get; init; }

    /// <summary>Age of the signal.</summary>
    public required TimeSpan Age { get; init; }

    /// <summary>Time remaining until stale (negative if already stale).</summary>
    public required TimeSpan TimeUntilStale { get; init; }

    /// <summary>Whether revalidation is required.</summary>
    public required bool RequiresRevalidation { get; init; }

    /// <summary>Status classification.</summary>
    public required STCR007A Status { get; init; }

    /// <summary>Half-life used for calculation.</summary>
    public required double HalfLifeMinutes { get; init; }

    /// <summary>Human-readable rationale.</summary>
    public required string Rationale { get; init; }
}

/// <summary>
/// Signal freshness status.
/// </summary>
public enum STCR007A
{
    /// <summary>Freshness >= 80%.</summary>
    Fresh = 0,

    /// <summary>Freshness 50-80% (approaching stale).</summary>
    Aging = 1,

    /// <summary>Freshness 25-50% (needs revalidation).</summary>
    Stale = 2,

    /// <summary>Freshness &lt; 25% (expired).</summary>
    Expired = 3
}
