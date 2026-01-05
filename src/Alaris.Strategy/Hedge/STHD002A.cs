// STHD002A.cs - vega correlation result

namespace Alaris.Strategy.Hedge;

/// <summary>
/// Represents the result of vega correlation analysis.
/// </summary>

public sealed record STHD002A
{
    /// <summary>
    /// Gets the symbol being analysed.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the Pearson correlation coefficient.
    /// </summary>
    
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