// =============================================================================
// STHD002A.cs - Vega Correlation Result
// Component: STHD002A | Category: Hedging | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

namespace Alaris.Strategy.Hedge;

/// <summary>
/// Represents the result of vega correlation analysis.
/// </summary>
/// <remarks>
/// <para>
/// This immutable record captures the correlation between front-month and
/// back-month IV changes, enabling assessment of calendar spread vega risk.
/// </para>
/// <para>
/// Interpretation guidelines:
/// - Correlation &lt; 0.50: Strong independence, ideal for calendar spreads
/// - Correlation 0.50-0.70: Acceptable with monitoring
/// - Correlation &gt; 0.70: Elevated sympathetic collapse risk
/// </para>
/// </remarks>
public sealed record STHD002A
{
    /// <summary>
    /// Gets the symbol being analysed.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the Pearson correlation coefficient.
    /// </summary>
    /// <remarks>
    /// Range: [-1, 1]. Values closer to 0 indicate independence.
    /// May be NaN if insufficient data or zero variance.
    /// </remarks>
    public required double Correlation { get; init; }

    /// <summary>
    /// Gets the maximum acceptable correlation threshold.
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>
    /// Gets the number of observations used in the analysis.
    /// </summary>
    public required int Observations { get; init; }

    /// <summary>
    /// Gets the minimum required observations.
    /// </summary>
    public required int MinimumObservations { get; init; }

    /// <summary>
    /// Gets whether the correlation is below the threshold.
    /// </summary>
    public required bool PassesFilter { get; init; }

    /// <summary>
    /// Gets whether sufficient data was available for analysis.
    /// </summary>
    public required bool HasSufficientData { get; init; }

    /// <summary>
    /// Gets the human-readable interpretation.
    /// </summary>
    public required string Interpretation { get; init; }

    /// <summary>
    /// Gets the absolute correlation value.
    /// </summary>
    public double AbsoluteCorrelation => Math.Abs(Correlation);

    /// <summary>
    /// Gets the margin below the threshold.
    /// </summary>
    /// <remarks>
    /// Positive values indicate the correlation is below threshold.
    /// Negative values indicate threshold exceeded.
    /// </remarks>
    public double Margin => Threshold - Correlation;

    /// <summary>
    /// Gets a human-readable summary of the analysis.
    /// </summary>
    public string Summary
    {
        get
        {
            if (!HasSufficientData)
            {
                return $"{Symbol}: INSUFFICIENT DATA - {Observations}/{MinimumObservations} observations";
            }

            if (double.IsNaN(Correlation))
            {
                return $"{Symbol}: UNDEFINED - Zero variance in IV series";
            }

            string status = PassesFilter ? "PASS" : "FAIL";
            return $"{Symbol}: {status} - Correlation {Correlation:F4} " +
                   $"({(PassesFilter ? "<" : "≥")} {Threshold:F2}). {Interpretation}";
        }
    }
}