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