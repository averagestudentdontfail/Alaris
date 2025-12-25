// STHD004A.cs - gamma risk assessment result

namespace Alaris.Strategy.Hedge;

/// <summary>
/// Represents the result of gamma risk assessment for a calendar spread.
/// </summary>

public sealed record STHD004A
{
    /// <summary>
    /// Gets the symbol being assessed.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the current spread delta per contract.
    /// </summary>
    public required double CurrentDelta { get; init; }

    /// <summary>
    /// Gets the current spread gamma per contract.
    /// </summary>
    public required double CurrentGamma { get; init; }

    /// <summary>
    /// Gets the current spread vega per contract.
    /// </summary>
    public required double CurrentVega { get; init; }

    /// <summary>
    /// Gets the current spread theta per contract.
    /// </summary>
    public required double CurrentTheta { get; init; }

    /// <summary>
    /// Gets the current underlying spot price.
    /// </summary>
    public required double SpotPrice { get; init; }

    /// <summary>
    /// Gets the calendar spread strike price.
    /// </summary>
    public required double StrikePrice { get; init; }

    /// <summary>
    /// Gets the moneyness ratio (Spot/Strike).
    /// </summary>
    
    public required double Moneyness { get; init; }

    /// <summary>
    /// Gets the absolute deviation from at-the-money.
    /// </summary>
    public required double MoneynessDeviation { get; init; }

    /// <summary>
    /// Gets the days until earnings announcement.
    /// </summary>
    public required int DaysToEarnings { get; init; }

    /// <summary>
    /// Gets the delta threshold for re-centering.
    /// </summary>
    public required double DeltaThreshold { get; init; }

    /// <summary>
    /// Gets the gamma warning threshold.
    /// </summary>
    public required double GammaThreshold { get; init; }

    /// <summary>
    /// Gets the moneyness alert threshold.
    /// </summary>
    public required double MoneynessThreshold { get; init; }

    /// <summary>
    /// Gets the recommended action.
    /// </summary>
    public required RehedgeAction RecommendedAction { get; init; }

    /// <summary>
    /// Gets the rationale for the recommendation.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Gets whether delta is within acceptable bounds.
    /// </summary>
    public bool DeltaWithinBounds => Math.Abs(CurrentDelta) <= DeltaThreshold;

    /// <summary>
    /// Gets whether gamma risk is elevated.
    /// </summary>
    public bool GammaElevated => CurrentGamma < GammaThreshold;

    /// <summary>
    /// Gets whether moneyness deviation is significant.
    /// </summary>
    public bool MoneynessSignificant => MoneynessDeviation > MoneynessThreshold;

    /// <summary>
    /// Gets the percentage distance to delta threshold.
    /// </summary>
    public double DeltaMarginPercent => DeltaThreshold > 0
        ? (DeltaThreshold - Math.Abs(CurrentDelta)) / DeltaThreshold * 100.0
        : 0.0;

    /// <summary>
    /// Gets the underlying move required to hit strike.
    /// </summary>
    public double MoveToStrike => StrikePrice - SpotPrice;

    /// <summary>
    /// Gets the percentage move required to hit strike.
    /// </summary>
    public double MoveToStrikePercent => SpotPrice > 0
        ? MoveToStrike / SpotPrice * 100.0
        : 0.0;

    /// <summary>
    /// Gets whether immediate action is required.
    /// </summary>
    public bool RequiresAction => RecommendedAction is RehedgeAction.RecenterRequired or RehedgeAction.ExitPosition;

    /// <summary>
    /// Gets a human-readable summary of the assessment.
    /// </summary>
    public string Summary => $"{Symbol}: {RecommendedAction} - Delta={CurrentDelta:F4}, " +
                            $"Gamma={CurrentGamma:F4}, Moneyness={Moneyness:F4}. " +
                            Rationale;
}