// STCS009A.cs - liquidity validation result

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the result of liquidity validation for a proposed position.
/// </summary>

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