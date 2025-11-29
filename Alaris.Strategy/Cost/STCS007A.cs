// =============================================================================
// STCS007A.cs - Cost Validation Result
// Component: STCS007A | Category: Cost | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the result of validating a signal against execution costs.
/// </summary>
/// <remarks>
/// <para>
/// This immutable record captures the complete cost validation assessment,
/// enabling transparent decision-making about whether to execute a trade.
/// </para>
/// <para>
/// A signal is considered production-ready when:
/// - Post-cost IV/RV ratio ≥ minimum threshold (default: 1.20)
/// - Slippage percentage ≤ maximum threshold (default: 10%)
/// - Total execution cost ≤ maximum threshold (default: 5% of capital)
/// </para>
/// </remarks>
public sealed record STCS007A
{
    /// <summary>
    /// Gets the symbol being validated.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the IV/RV ratio before cost adjustment.
    /// </summary>
    /// <remarks>
    /// This is the original ratio from signal generation (Atilgan threshold: ≥ 1.25).
    /// </remarks>
    public required double PreCostIVRVRatio { get; init; }

    /// <summary>
    /// Gets the IV/RV ratio after cost adjustment.
    /// </summary>
    /// <remarks>
    /// Computed as: PreCostRatio × (1 - ExecutionCostPercent/100).
    /// Must remain ≥ MinimumRequiredRatio for the signal to pass.
    /// </remarks>
    public required double PostCostIVRVRatio { get; init; }

    /// <summary>
    /// Gets the minimum required post-cost ratio.
    /// </summary>
    public required double MinimumRequiredRatio { get; init; }

    /// <summary>
    /// Gets the detailed spread execution cost.
    /// </summary>
    public required STCS004A SpreadCost { get; init; }

    /// <summary>
    /// Gets whether the post-cost ratio meets the threshold.
    /// </summary>
    public required bool PassesRatioThreshold { get; init; }

    /// <summary>
    /// Gets whether slippage is within acceptable bounds.
    /// </summary>
    public required bool PassesSlippageThreshold { get; init; }

    /// <summary>
    /// Gets whether total execution cost is within acceptable bounds.
    /// </summary>
    public required bool PassesCostThreshold { get; init; }

    /// <summary>
    /// Gets whether all validation criteria are satisfied.
    /// </summary>
    public required bool OverallPass { get; init; }

    /// <summary>
    /// Gets the name of the cost model used for validation.
    /// </summary>
    public required string CostModel { get; init; }

    /// <summary>
    /// Gets the ratio degradation due to costs.
    /// </summary>
    /// <remarks>
    /// Expressed as a percentage: (PreCost - PostCost) / PreCost × 100.
    /// </remarks>
    public double RatioDegradationPercent => PreCostIVRVRatio > 0
        ? (PreCostIVRVRatio - PostCostIVRVRatio) / PreCostIVRVRatio * 100.0
        : 0.0;

    /// <summary>
    /// Gets the ratio margin above the minimum threshold.
    /// </summary>
    /// <remarks>
    /// Positive values indicate headroom; negative values indicate failure.
    /// </remarks>
    public double RatioMargin => PostCostIVRVRatio - MinimumRequiredRatio;

    /// <summary>
    /// Gets a human-readable summary of the validation result.
    /// </summary>
    public string Summary => OverallPass
        ? $"{Symbol}: PASS - Post-cost IV/RV {PostCostIVRVRatio:F3} ≥ {MinimumRequiredRatio:F3}"
        : GenerateFailureSummary();

    /// <summary>
    /// Generates a detailed failure summary.
    /// </summary>
    private string GenerateFailureSummary()
    {
        var failures = new List<string>();

        if (!PassesRatioThreshold)
        {
            failures.Add($"IV/RV {PostCostIVRVRatio:F3} < {MinimumRequiredRatio:F3}");
        }

        if (!PassesSlippageThreshold)
        {
            failures.Add($"Slippage {SpreadCost.SlippagePercent:F2}% exceeds limit");
        }

        if (!PassesCostThreshold)
        {
            failures.Add($"Execution cost {SpreadCost.ExecutionCostPercent:F2}% exceeds limit");
        }

        return $"{Symbol}: FAIL - {string.Join("; ", failures)}";
    }
}