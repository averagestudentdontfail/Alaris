// STCS007A.cs - cost validation result

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the result of validating a signal against execution costs.
/// </summary>

public sealed record STCS007A
{
    /// <summary>
    /// Gets the symbol being validated.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the IV/RV ratio before cost adjustment.
    /// </summary>
    
    public required double PreCostIVRVRatio { get; init; }

    /// <summary>
    /// Gets the IV/RV ratio after cost adjustment.
    /// </summary>
    
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
    /// Gets the slippage percentage.
    /// </summary>
    public required decimal SlippagePercent { get; init; }

    /// <summary>
    /// Gets the slippage per spread in dollars.
    /// </summary>
    public required decimal SlippagePerSpread { get; init; }

    /// <summary>
    /// Gets the slippage percent threshold.
    /// </summary>
    public required decimal SlippagePercentThreshold { get; init; }

    /// <summary>
    /// Gets the slippage per spread threshold (dollars).
    /// </summary>
    public required decimal SlippagePerSpreadThreshold { get; init; }

    /// <summary>
    /// Gets whether slippage percent is within acceptable bounds.
    /// </summary>
    public required bool PassesSlippagePercent { get; init; }

    /// <summary>
    /// Gets whether slippage per spread is within acceptable bounds.
    /// </summary>
    public required bool PassesSlippageAbsolute { get; init; }

    /// <summary>
    /// Gets the execution cost percentage (using minimum capital basis).
    /// </summary>
    public required decimal ExecutionCostPercent { get; init; }

    /// <summary>
    /// Gets the execution cost percentage basis in dollars.
    /// </summary>
    public required decimal ExecutionCostPercentBasis { get; init; }

    /// <summary>
    /// Gets the execution cost per spread in dollars.
    /// </summary>
    public required decimal ExecutionCostPerSpread { get; init; }

    /// <summary>
    /// Gets the execution cost percent threshold.
    /// </summary>
    public required decimal ExecutionCostPercentThreshold { get; init; }

    /// <summary>
    /// Gets the execution cost per spread threshold (dollars).
    /// </summary>
    public required decimal ExecutionCostPerSpreadThreshold { get; init; }

    /// <summary>
    /// Gets whether execution cost percent is within acceptable bounds.
    /// </summary>
    public required bool PassesExecutionCostPercent { get; init; }

    /// <summary>
    /// Gets whether execution cost per spread is within acceptable bounds.
    /// </summary>
    public required bool PassesExecutionCostAbsolute { get; init; }

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
    
    public double RatioDegradationPercent => PreCostIVRVRatio > 0
        ? (PreCostIVRVRatio - PostCostIVRVRatio) / PreCostIVRVRatio * 100.0
        : 0.0;

    /// <summary>
    /// Gets the ratio margin above the minimum threshold.
    /// </summary>
    
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
        List<string> failures = new List<string>();

        if (!PassesRatioThreshold)
        {
            failures.Add($"IV/RV {PostCostIVRVRatio:F3} < {MinimumRequiredRatio:F3}");
        }

        if (!PassesSlippagePercent)
        {
            failures.Add($"Slippage {SlippagePercent:F2}% > {SlippagePercentThreshold:F2}%");
        }

        if (!PassesSlippageAbsolute)
        {
            failures.Add($"Slippage ${SlippagePerSpread:F2} > ${SlippagePerSpreadThreshold:F2} per spread");
        }

        if (!PassesExecutionCostPercent)
        {
            failures.Add($"Execution cost {ExecutionCostPercent:F2}% (basis ${ExecutionCostPercentBasis:F2}) > {ExecutionCostPercentThreshold:F2}%");
        }

        if (!PassesExecutionCostAbsolute)
        {
            failures.Add($"Execution cost ${ExecutionCostPerSpread:F2} > ${ExecutionCostPerSpreadThreshold:F2} per spread");
        }

        return $"{Symbol}: FAIL - {string.Join("; ", failures)}";
    }
}
