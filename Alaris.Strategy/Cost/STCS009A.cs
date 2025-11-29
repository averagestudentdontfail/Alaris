// =============================================================================
// STCS009A.cs - Liquidity Validation Result
// Component: STCS009A | Category: Cost | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the result of liquidity validation for a proposed position.
/// </summary>
/// <remarks>
/// <para>
/// This immutable record captures the complete liquidity assessment,
/// enabling informed decisions about position sizing and risk management.
/// </para>
/// <para>
/// When DefinedRiskAssured is false, the position size should be reduced
/// to RecommendedContracts to maintain the "Max Loss = Debit Paid" assumption.
/// </para>
/// </remarks>
public sealed record STCS009A
{
    /// <summary>
    /// Gets the symbol being validated.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the originally requested number of contracts.
    /// </summary>
    public required int RequestedContracts { get; init; }

    /// <summary>
    /// Gets the recommended number of contracts.
    /// </summary>
    /// <remarks>
    /// Equal to RequestedContracts when liquidity is sufficient;
    /// otherwise, reduced to meet liquidity thresholds.
    /// </remarks>
    public required int RecommendedContracts { get; init; }

    /// <summary>
    /// Gets the back-month option average daily volume.
    /// </summary>
    public required int BackMonthVolume { get; init; }

    /// <summary>
    /// Gets the back-month option open interest.
    /// </summary>
    public required int BackMonthOpenInterest { get; init; }

    /// <summary>
    /// Gets the position-to-volume ratio.
    /// </summary>
    public required double VolumeRatio { get; init; }

    /// <summary>
    /// Gets the position-to-open-interest ratio.
    /// </summary>
    public required double OpenInterestRatio { get; init; }

    /// <summary>
    /// Gets the maximum acceptable volume ratio threshold.
    /// </summary>
    public required double VolumeThreshold { get; init; }

    /// <summary>
    /// Gets the maximum acceptable open interest ratio threshold.
    /// </summary>
    public required double OpenInterestThreshold { get; init; }

    /// <summary>
    /// Gets whether the volume ratio is within acceptable bounds.
    /// </summary>
    public required bool PassesVolumeFilter { get; init; }

    /// <summary>
    /// Gets whether the open interest ratio is within acceptable bounds.
    /// </summary>
    public required bool PassesOpenInterestFilter { get; init; }

    /// <summary>
    /// Gets whether the position can be exited with defined risk.
    /// </summary>
    /// <remarks>
    /// When true, the "Max Loss = Debit Paid" assumption holds.
    /// When false, illiquidity may cause losses exceeding theoretical maximum.
    /// </remarks>
    public required bool DefinedRiskAssured { get; init; }

    /// <summary>
    /// Gets whether the position size needed adjustment.
    /// </summary>
    public bool RequiresAdjustment => RequestedContracts != RecommendedContracts;

    /// <summary>
    /// Gets the percentage reduction in position size.
    /// </summary>
    public double ReductionPercent => RequestedContracts > 0
        ? (RequestedContracts - RecommendedContracts) / (double)RequestedContracts * 100.0
        : 0.0;

    /// <summary>
    /// Gets a warning message if liquidity is insufficient.
    /// </summary>
    public string? Warning => !DefinedRiskAssured
        ? $"Position size exceeds liquidity thresholds. Reduce to {RecommendedContracts} contracts to ensure defined risk."
        : null;

    /// <summary>
    /// Gets a human-readable summary of the validation result.
    /// </summary>
    public string Summary
    {
        get
        {
            if (DefinedRiskAssured)
            {
                return $"{Symbol}: PASS - {RequestedContracts} contracts within liquidity bounds " +
                       $"(Vol: {VolumeRatio:P2} ≤ {VolumeThreshold:P0}, OI: {OpenInterestRatio:P2} ≤ {OpenInterestThreshold:P0})";
            }

            var failures = new List<string>();
            if (!PassesVolumeFilter)
            {
                failures.Add($"Volume {VolumeRatio:P2} > {VolumeThreshold:P0}");
            }
            if (!PassesOpenInterestFilter)
            {
                failures.Add($"OI {OpenInterestRatio:P2} > {OpenInterestThreshold:P0}");
            }

            return $"{Symbol}: FAIL - {string.Join("; ", failures)}. Reduce to {RecommendedContracts} contracts.";
        }
    }
}