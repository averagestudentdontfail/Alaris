using System;
using System.Collections.Generic;

namespace Alaris.Infrastructure.Data.Model;

/// <summary>
/// Represents a single option contract with market data.
/// </summary>
public sealed class OptionContract
{
    /// <summary>Gets the underlying symbol.</summary>
    public required string UnderlyingSymbol { get; init; }

    /// <summary>Gets the option symbol (OCC format).</summary>
    public required string OptionSymbol { get; init; }

    /// <summary>Gets the strike price.</summary>
    public required decimal Strike { get; init; }

    /// <summary>Gets the expiration date.</summary>
    public required DateTime Expiration { get; init; }

    /// <summary>Gets whether this is a call or put.</summary>
    public required OptionRight Right { get; init; }

    /// <summary>Gets the bid price.</summary>
    public required decimal Bid { get; init; }

    /// <summary>Gets the ask price.</summary>
    public required decimal Ask { get; init; }

    /// <summary>Gets the mid price (bid + ask) / 2.</summary>
    public decimal Mid => (Bid + Ask) / 2m;

    /// <summary>Gets the bid-ask spread.</summary>
    public decimal Spread => Ask - Bid;

    /// <summary>Gets the last traded price.</summary>
    public decimal? Last { get; init; }

    /// <summary>Gets the volume traded today.</summary>
    public long Volume { get; init; }

    /// <summary>Gets the open interest.</summary>
    public long OpenInterest { get; init; }

    /// <summary>Gets the implied volatility (if available).</summary>
    public decimal? ImpliedVolatility { get; init; }

    /// <summary>Gets the delta (if available).</summary>
    public decimal? Delta { get; init; }

    /// <summary>Gets the gamma (if available).</summary>
    public decimal? Gamma { get; init; }

    /// <summary>Gets the theta (if available).</summary>
    public decimal? Theta { get; init; }

    /// <summary>Gets the vega (if available).</summary>
    public decimal? Vega { get; init; }

    /// <summary>Gets the timestamp of this quote.</summary>
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Option right (call or put).
/// </summary>
public enum OptionRight
{
    /// <summary>Call option.</summary>
    Call,

    /// <summary>Put option.</summary>
    Put
}

/// <summary>
/// Represents a complete options chain for a given underlying at a point in time.
/// </summary>
public sealed class OptionChainSnapshot
{
    /// <summary>Gets the underlying symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>Gets the underlying spot price.</summary>
    public required decimal SpotPrice { get; init; }

    /// <summary>Gets the snapshot timestamp.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>Gets all option contracts in this chain.</summary>
    public required IReadOnlyList<OptionContract> Contracts { get; init; }

    /// <summary>Gets contracts by expiration date.</summary>
    public IReadOnlyDictionary<DateTime, IReadOnlyList<OptionContract>> ByExpiration =>
        Contracts
            .GroupBy(c => c.Expiration)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OptionContract>)g.ToList());

    /// <summary>Gets only call contracts.</summary>
    public IReadOnlyList<OptionContract> Calls =>
        Contracts.Where(c => c.Right == OptionRight.Call).ToList();

    /// <summary>Gets only put contracts.</summary>
    public IReadOnlyList<OptionContract> Puts =>
        Contracts.Where(c => c.Right == OptionRight.Put).ToList();
}

/// <summary>
/// Represents an OHLCV bar for historical price data.
/// </summary>
public sealed class PriceBar
{
    /// <summary>Gets the symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>Gets the bar timestamp.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>Gets the open price.</summary>
    public required decimal Open { get; init; }

    /// <summary>Gets the high price.</summary>
    public required decimal High { get; init; }

    /// <summary>Gets the low price.</summary>
    public required decimal Low { get; init; }

    /// <summary>Gets the close price.</summary>
    public required decimal Close { get; init; }

    /// <summary>Gets the volume.</summary>
    public required long Volume { get; init; }
}

/// <summary>
/// Represents an earnings announcement event.
/// </summary>
public sealed class EarningsEvent
{
    /// <summary>Gets the symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>Gets the earnings announcement date.</summary>
    public required DateTime Date { get; init; }

    /// <summary>Gets the fiscal quarter (e.g., "Q1", "Q2").</summary>
    public string? FiscalQuarter { get; init; }

    /// <summary>Gets the fiscal year.</summary>
    public int? FiscalYear { get; init; }

    /// <summary>Gets whether this is before market open or after close.</summary>
    public EarningsTiming? Timing { get; init; }

    /// <summary>Gets the EPS estimate (analyst consensus).</summary>
    public decimal? EpsEstimate { get; init; }

    /// <summary>Gets the actual EPS reported (null if not yet announced).</summary>
    public decimal? EpsActual { get; init; }

    /// <summary>Gets the data source.</summary>
    public required string Source { get; init; }

    /// <summary>Gets when this record was fetched.</summary>
    public required DateTime FetchedAt { get; init; }
}

/// <summary>
/// Timing of earnings announcement relative to market hours.
/// </summary>
public enum EarningsTiming
{
    /// <summary>Before market open.</summary>
    BeforeMarketOpen,

    /// <summary>After market close.</summary>
    AfterMarketClose,

    /// <summary>During market hours (rare).</summary>
    DuringMarketHours,

    /// <summary>Timing unknown.</summary>
    Unknown
}

/// <summary>
/// Complete market data snapshot for strategy evaluation.
/// </summary>
public sealed class MarketDataSnapshot
{
    /// <summary>Gets the symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>Gets the evaluation timestamp.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>Gets the current spot price.</summary>
    public required decimal SpotPrice { get; init; }

    /// <summary>Gets historical price bars for RV calculation.</summary>
    public required IReadOnlyList<PriceBar> HistoricalBars { get; init; }

    /// <summary>Gets the options chain snapshot.</summary>
    public required OptionChainSnapshot OptionChain { get; init; }

    /// <summary>Gets the next earnings event (if within evaluation window).</summary>
    public EarningsEvent? NextEarnings { get; init; }

    /// <summary>Gets historical earnings events (for Leung-Santoli calibration).</summary>
    public required IReadOnlyList<EarningsEvent> HistoricalEarnings { get; init; }

    /// <summary>Gets the current risk-free rate.</summary>
    public required decimal RiskFreeRate { get; init; }

    /// <summary>Gets the dividend yield.</summary>
    public required decimal DividendYield { get; init; }

    /// <summary>Gets 30-day average volume.</summary>
    public required decimal AverageVolume30Day { get; init; }
}

/// <summary>
/// Result of a data quality validation check.
/// </summary>
public sealed class DataQualityResult
{
    /// <summary>Gets the validator component ID.</summary>
    public required string ValidatorId { get; init; }

    /// <summary>Gets the validation result.</summary>
    public required ValidationStatus Status { get; init; }

    /// <summary>Gets the validation message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets any validation warnings (non-fatal).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>Gets the data element that was validated.</summary>
    public required string DataElement { get; init; }

    /// <summary>Gets the timestamp of validation.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Validation status enumeration.
/// </summary>
public enum ValidationStatus
{
    /// <summary>Validation passed.</summary>
    Passed,

    /// <summary>Validation passed with warnings.</summary>
    PassedWithWarnings,

    /// <summary>Validation failed.</summary>
    Failed
}