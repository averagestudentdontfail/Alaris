// STDT010A.cs - Session data requirements model for backtesting

using System;
using System.Collections.Generic;

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
    private const int DefaultSignalWindowMinDays = 5;
    private const int DefaultSignalWindowMaxDays = 7;
    private const int DefaultWarmupDays = 120;
    private const int DefaultEarningsLookaheadDays = 120;
    private const int MinimumSignalWindowDays = 1;
    private const int MinimumWarmupDays = 30;
    private const string DefaultBenchmarkSymbol = "SPY";

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
    public int SignalWindowMinDays { get; init; } = DefaultSignalWindowMinDays;
    
    /// <summary>
    /// Maximum days before earnings for signal generation.
    /// Default: 7 (from STLN001A strategy)
    /// </summary>
    public int SignalWindowMaxDays { get; init; } = DefaultSignalWindowMaxDays;
    
    /// <summary>
    /// Days of price history required for warmup (volatility calculation).
    /// Default: 120 (covers 45-day warmup with buffer for holidays)
    /// </summary>
    public int WarmupDays { get; init; } = DefaultWarmupDays;
    
    /// <summary>
    /// Days of earnings lookahead required after EndDate.
    /// Default: 120 (covers 90-day lookahead with buffer)
    /// </summary>
    public int EarningsLookaheadDays { get; init; } = DefaultEarningsLookaheadDays;
    
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
    public string BenchmarkSymbol { get; init; } = DefaultBenchmarkSymbol;
    
    /// <summary>
    /// All symbols including benchmark.
    /// </summary>
    public IReadOnlyList<string> AllSymbols
    {
        get
        {
            ArgumentNullException.ThrowIfNull(Symbols);

            if (Symbols.Count == 0)
            {
                return new[] { BenchmarkSymbol };
            }

            if (ContainsSymbol(Symbols, BenchmarkSymbol))
            {
                return Symbols;
            }

            string[] combined = new string[Symbols.Count + 1];
            for (int i = 0; i < Symbols.Count; i++)
            {
                combined[i] = Symbols[i];
            }

            combined[combined.Length - 1] = BenchmarkSymbol;
            return combined;
        }
    }
    
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
        this with { OptionsRequiredDates = CopyDates(dates) };
    
    // ═══════════════════════════════════════════════════════════════════════
    // Validation
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Validates the requirements model.
    /// </summary>
    public (bool IsValid, string? Error) Validate()
    {
        if (Symbols is null)
        {
            return (false, "Symbols collection is required");
        }

        if (StartDate >= EndDate)
        {
            return (false, "StartDate must be before EndDate");
        }
        
        if (Symbols.Count == 0)
        {
            return (false, "At least one symbol is required");
        }
        
        if (SignalWindowMinDays < MinimumSignalWindowDays || SignalWindowMaxDays < SignalWindowMinDays)
        {
            return (false, "Invalid signal window parameters");
        }
        
        if (WarmupDays < MinimumWarmupDays)
        {
            return (false, "WarmupDays must be at least 30 for volatility calculation");
        }

        if (EarningsLookaheadDays < 0)
        {
            return (false, "EarningsLookaheadDays must be non-negative");
        }

        if (string.IsNullOrWhiteSpace(BenchmarkSymbol))
        {
            return (false, "BenchmarkSymbol is required");
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

    private static bool ContainsSymbol(IReadOnlyList<string> symbols, string benchmarkSymbol)
    {
        StringComparer comparer = StringComparer.OrdinalIgnoreCase;

        for (int i = 0; i < symbols.Count; i++)
        {
            if (comparer.Equals(symbols[i], benchmarkSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<DateTime> CopyDates(IReadOnlyList<DateTime> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);

        if (dates.Count == 0)
        {
            return Array.Empty<DateTime>();
        }

        DateTime[] copy = new DateTime[dates.Count];
        for (int i = 0; i < dates.Count; i++)
        {
            copy[i] = dates[i];
        }

        return copy;
    }
}
