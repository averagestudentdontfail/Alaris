// STDT010A.cs - Session data requirements model for backtesting

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alaris.Core.Model;

/// <summary>
/// Explicit model for session data requirements.
/// Component ID: STDT010A
/// 
/// Design Principles:
/// - All data dependencies are explicitly declared
/// - Computed properties derive requirements from strategy parameters
/// - Assumptions are testable and observable
/// </summary>
public sealed record STDT010A
{
    // ═══════════════════════════════════════════════════════════════════════
    // Core Session Parameters
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// First trading day of the backtest.
    /// </summary>
    public required DateTime StartDate { get; init; }
    
    /// <summary>
    /// Last trading day of the backtest.
    /// </summary>
    public required DateTime EndDate { get; init; }
    
    /// <summary>
    /// Symbols to include in the backtest universe.
    /// </summary>
    public required IReadOnlyList<string> Symbols { get; init; }
    
    // ═══════════════════════════════════════════════════════════════════════
    // Strategy Parameters (Signal Window)
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Minimum days before earnings for signal generation.
    /// Default: 5 (from STLN001A strategy)
    /// </summary>
    public int SignalWindowMinDays { get; init; } = 5;
    
    /// <summary>
    /// Maximum days before earnings for signal generation.
    /// Default: 7 (from STLN001A strategy)
    /// </summary>
    public int SignalWindowMaxDays { get; init; } = 7;
    
    /// <summary>
    /// Days of price history required for warmup (volatility calculation).
    /// Default: 120 (covers 45-day warmup with buffer for holidays)
    /// </summary>
    public int WarmupDays { get; init; } = 120;
    
    /// <summary>
    /// Days of earnings lookahead required after EndDate.
    /// Default: 120 (covers 90-day lookahead with buffer)
    /// </summary>
    public int EarningsLookaheadDays { get; init; } = 120;
    
    // ═══════════════════════════════════════════════════════════════════════
    // Computed Requirements
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Last date for which earnings data is required.
    /// Algorithm looks ahead 90 days; we add buffer.
    /// </summary>
    public DateTime EarningsLookaheadEnd => EndDate.AddDays(EarningsLookaheadDays);
    
    /// <summary>
    /// First date for which price data is required.
    /// Accounts for warmup period for volatility calculations.
    /// </summary>
    public DateTime PriceDataStart => StartDate.AddDays(-WarmupDays);
    
    /// <summary>
    /// Benchmark symbol for performance comparison.
    /// </summary>
    public string BenchmarkSymbol { get; init; } = "SPY";
    
    /// <summary>
    /// All symbols including benchmark.
    /// </summary>
    public IReadOnlyList<string> AllSymbols => 
        Symbols.Contains(BenchmarkSymbol, StringComparer.OrdinalIgnoreCase)
            ? Symbols
            : Symbols.Append(BenchmarkSymbol).ToList();
    
    // ═══════════════════════════════════════════════════════════════════════
    // Options Data Requirements
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Dates where options data is required for signal generation.
    /// Populated by analyzing earnings calendar.
    /// </summary>
    public IReadOnlyList<DateTime> OptionsRequiredDates { get; init; } = Array.Empty<DateTime>();
    
    /// <summary>
    /// Creates a copy with computed options dates.
    /// </summary>
    public STDT010A WithOptionsRequiredDates(IReadOnlyList<DateTime> dates) =>
        this with { OptionsRequiredDates = dates };
    
    // ═══════════════════════════════════════════════════════════════════════
    // Validation
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Validates the requirements model.
    /// </summary>
    public (bool IsValid, string? Error) Validate()
    {
        if (StartDate >= EndDate)
        {
            return (false, "StartDate must be before EndDate");
        }
        
        if (Symbols.Count == 0)
        {
            return (false, "At least one symbol is required");
        }
        
        if (SignalWindowMinDays < 1 || SignalWindowMaxDays < SignalWindowMinDays)
        {
            return (false, "Invalid signal window parameters");
        }
        
        if (WarmupDays < 30)
        {
            return (false, "WarmupDays must be at least 30 for volatility calculation");
        }
        
        return (true, null);
    }
    
    /// <summary>
    /// Gets a summary string for logging.
    /// </summary>
    public string GetSummary() =>
        $"Session: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}, " +
        $"{Symbols.Count} symbols, " +
        $"Earnings lookahead to {EarningsLookaheadEnd:yyyy-MM-dd}, " +
        $"Options dates: {OptionsRequiredDates.Count}";
}
