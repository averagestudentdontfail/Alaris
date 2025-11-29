// =============================================================================
// STHD006A.cs - Production Validation Result
// Component: STHD006A | Category: Hedging | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Alaris.Strategy.Core;
using Alaris.Strategy.Cost;

namespace Alaris.Strategy.Hedging;

/// <summary>
/// Represents the complete production validation result for a trading signal.
/// </summary>
/// <remarks>
/// <para>
/// This immutable record aggregates all validation results into a single
/// decision point: is this signal ready for production execution?
/// </para>
/// <para>
/// A signal is production-ready when:
/// - BaseSignal.Strength is Recommended
/// - All validation checks pass (cost, vega, liquidity, gamma)
/// </para>
/// </remarks>
public sealed record STHD006A
{
    /// <summary>
    /// Gets the base trading signal.
    /// </summary>
    public required Signal BaseSignal { get; init; }

    /// <summary>
    /// Gets the list of validation checks performed.
    /// </summary>
    public required IReadOnlyList<ValidationCheck> Checks { get; init; }

    /// <summary>
    /// Gets whether all validation checks passed.
    /// </summary>
    public required bool OverallPass { get; init; }

    /// <summary>
    /// Gets the execution-adjusted spread debit.
    /// </summary>
    public required double AdjustedDebit { get; init; }

    /// <summary>
    /// Gets the recommended number of contracts.
    /// </summary>
    /// <remarks>
    /// May be adjusted down from original signal if liquidity is insufficient.
    /// </remarks>
    public required int RecommendedContracts { get; init; }

    /// <summary>
    /// Gets whether the signal is ready for production execution.
    /// </summary>
    /// <remarks>
    /// Requires both a Recommended signal strength AND all validations passing.
    /// </remarks>
    public required bool ProductionReady { get; init; }

    /// <summary>
    /// Gets the detailed cost validation result.
    /// </summary>
    public STCS007A? CostValidation { get; init; }

    /// <summary>
    /// Gets the vega correlation analysis result.
    /// </summary>
    public STHD002A? VegaCorrelation { get; init; }

    /// <summary>
    /// Gets the liquidity validation result.
    /// </summary>
    public STCS009A? LiquidityValidation { get; init; }

    /// <summary>
    /// Gets the gamma risk assessment result.
    /// </summary>
    public STHD004A? GammaAssessment { get; init; }

    /// <summary>
    /// Gets the number of validation checks that passed.
    /// </summary>
    public int PassedCheckCount => Checks.Count(c => c.Passed);

    /// <summary>
    /// Gets the total number of validation checks.
    /// </summary>
    public int TotalCheckCount => Checks.Count;

    /// <summary>
    /// Gets the names of failed checks.
    /// </summary>
    public IReadOnlyList<string> FailedChecks => Checks
        .Where(c => !c.Passed)
        .Select(c => c.Name)
        .ToList();

    /// <summary>
    /// Gets a human-readable summary of the validation result.
    /// </summary>
    public string Summary
    {
        get
        {
            string status = ProductionReady ? "PRODUCTION READY" : "NOT READY";
            string checkSummary = $"{PassedCheckCount}/{TotalCheckCount} checks passed";

            if (ProductionReady)
            {
                return $"{BaseSignal.Symbol}: {status} - {checkSummary}. " +
                       $"Execute {RecommendedContracts} contracts at ${AdjustedDebit:F4} debit.";
            }

            string failures = string.Join(", ", FailedChecks);
            return $"{BaseSignal.Symbol}: {status} - {checkSummary}. Failed: {failures}";
        }
    }

    /// <summary>
    /// Gets a detailed breakdown of all validation results.
    /// </summary>
    public string DetailedReport
    {
        get
        {
            var lines = new List<string>
            {
                $"=== Production Validation Report: {BaseSignal.Symbol} ===",
                $"Signal Strength: {BaseSignal.Strength}",
                $"Overall Status: {(ProductionReady ? "PRODUCTION READY" : "NOT READY")}",
                "",
                "Validation Checks:"
            };

            foreach (var check in Checks)
            {
                string checkStatus = check.Passed ? "✓ PASS" : "✗ FAIL";
                lines.Add($"  [{checkStatus}] {check.Name}");
                lines.Add($"           {check.Detail}");
            }

            lines.Add("");
            lines.Add("Position Details:");
            lines.Add($"  Recommended Contracts: {RecommendedContracts}");
            lines.Add($"  Adjusted Debit: ${AdjustedDebit:F4}");

            if (CostValidation != null)
            {
                lines.Add($"  Pre-Cost IV/RV: {CostValidation.PreCostIVRVRatio:F3}");
                lines.Add($"  Post-Cost IV/RV: {CostValidation.PostCostIVRVRatio:F3}");
            }

            if (VegaCorrelation != null && VegaCorrelation.HasSufficientData)
            {
                lines.Add($"  Vega Correlation: {VegaCorrelation.Correlation:F4}");
            }

            if (GammaAssessment != null)
            {
                lines.Add($"  Spread Delta: {GammaAssessment.CurrentDelta:F4}");
                lines.Add($"  Spread Gamma: {GammaAssessment.CurrentGamma:F4}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}