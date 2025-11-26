using Alaris.Events.Core;

namespace Alaris.Events.Domain;

/// <summary>
/// Event raised when a trading signal is generated for a symbol.
/// </summary>
public sealed record STCR004AGeneratedEvent : EVCR001A
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => nameof(STCR004AGeneratedEvent);
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the symbol for which the signal was generated.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the earnings date for this signal.
    /// </summary>
    public required DateTime EarningsDate { get; init; }

    /// <summary>
    /// Gets the signal strength (Recommended, Consider, Avoid).
    /// </summary>
    public required string STCR004AStrength { get; init; }

    /// <summary>
    /// Gets the IV/RV ratio.
    /// </summary>
    public required double IVRVRatio { get; init; }

    /// <summary>
    /// Gets the term structure slope.
    /// </summary>
    public required double STTM001ASlope { get; init; }

    /// <summary>
    /// Gets the average volume.
    /// </summary>
    public required long AverageVolume { get; init; }
}

/// <summary>
/// Event raised when a trading opportunity is evaluated.
/// </summary>
public sealed record OpportunityEvaluatedEvent : EVCR001A
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => nameof(OpportunityEvaluatedEvent);
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the symbol.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the earnings date.
    /// </summary>
    public required DateTime EarningsDate { get; init; }

    /// <summary>
    /// Gets whether the opportunity is actionable.
    /// </summary>
    public required bool IsActionable { get; init; }

    /// <summary>
    /// Gets the recommended number of contracts (if actionable).
    /// </summary>
    public int? Contracts { get; init; }

    /// <summary>
    /// Gets the spread cost.
    /// </summary>
    public double? SpreadCost { get; init; }

    /// <summary>
    /// Gets the allocation percentage.
    /// </summary>
    public double? AllocationPercent { get; init; }
}

/// <summary>
/// Event raised when an option is priced.
/// </summary>
public sealed record OptionPricedEvent : EVCR001A
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => nameof(OptionPricedEvent);
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the option type (Call/Put).
    /// </summary>
    public required string OptionType { get; init; }

    /// <summary>
    /// Gets the underlying price.
    /// </summary>
    public required double UnderlyingPrice { get; init; }

    /// <summary>
    /// Gets the strike price.
    /// </summary>
    public required double Strike { get; init; }

    /// <summary>
    /// Gets the time to expiry in years.
    /// </summary>
    public required double TimeToExpiry { get; init; }

    /// <summary>
    /// Gets the implied volatility.
    /// </summary>
    public required double ImpliedVolatility { get; init; }

    /// <summary>
    /// Gets the option price.
    /// </summary>
    public required double Price { get; init; }

    /// <summary>
    /// Gets the delta.
    /// </summary>
    public required double Delta { get; init; }

    /// <summary>
    /// Gets the gamma.
    /// </summary>
    public required double Gamma { get; init; }

    /// <summary>
    /// Gets the vega.
    /// </summary>
    public required double Vega { get; init; }

    /// <summary>
    /// Gets the theta.
    /// </summary>
    public required double Theta { get; init; }

    /// <summary>
    /// Gets the pricing regime used (PositiveRates, DoubleBoundary, etc.).
    /// </summary>
    public required string PricingRegime { get; init; }
}

/// <summary>
/// Event raised when a calendar spread is priced.
/// </summary>
public sealed record STPR001APricedEvent : EVCR001A
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => nameof(STPR001APricedEvent);
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the underlying price.
    /// </summary>
    public required double UnderlyingPrice { get; init; }

    /// <summary>
    /// Gets the strike price.
    /// </summary>
    public required double Strike { get; init; }

    /// <summary>
    /// Gets the front month expiry.
    /// </summary>
    public required DateTime FrontExpiry { get; init; }

    /// <summary>
    /// Gets the back month expiry.
    /// </summary>
    public required DateTime BackExpiry { get; init; }

    /// <summary>
    /// Gets the spread cost (debit paid).
    /// </summary>
    public required double SpreadCost { get; init; }

    /// <summary>
    /// Gets the maximum profit potential.
    /// </summary>
    public required double MaxProfit { get; init; }

    /// <summary>
    /// Gets the maximum loss potential.
    /// </summary>
    public required double MaxLoss { get; init; }

    /// <summary>
    /// Gets the breakeven points.
    /// </summary>
    public required IReadOnlyList<double> BreakEvenPoints { get; init; }
}

/// <summary>
/// Event raised when a position size is calculated.
/// </summary>
public sealed record PositionSizeCalculatedEvent : EVCR001A
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => nameof(PositionSizeCalculatedEvent);
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the symbol.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the portfolio value used in calculation.
    /// </summary>
    public required double PortfolioValue { get; init; }

    /// <summary>
    /// Gets the number of contracts recommended.
    /// </summary>
    public required int Contracts { get; init; }

    /// <summary>
    /// Gets the allocation percentage.
    /// </summary>
    public required double AllocationPercent { get; init; }

    /// <summary>
    /// Gets the dollar allocation.
    /// </summary>
    public required double DollarAllocation { get; init; }

    /// <summary>
    /// Gets the Kelly fraction used.
    /// </summary>
    public required double KellyFraction { get; init; }

    /// <summary>
    /// Gets the number of historical trades analyzed.
    /// </summary>
    public required int HistoricalTradesAnalyzed { get; init; }
}
