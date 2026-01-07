// TSUN054A.cs - Unit tests for earnings data parsing and options date computation

using System.Text.Json;
using Xunit;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for earnings data parsing and options date computation.
/// Component ID: TSUN054A
/// </summary>
/// <remarks>
/// Tests validate:
/// - Symbol matching is case-insensitive
/// - Evaluation window calculation is correct
/// - Weekend filtering works
/// - Edge cases are handled (no earnings, future earnings, etc.)
/// </remarks>
public sealed class TSUN054A
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Mirror the cached earnings structure from APsv002A
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Used for JSON deserialization")]
    private sealed class CachedEarningsDay
    {
        public DateTime Date { get; init; }
        public DateTime FetchedAt { get; init; }
        public IReadOnlyList<CachedEarningsEvent> Earnings { get; init; } = Array.Empty<CachedEarningsEvent>();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Used for JSON deserialization")]
    private sealed class CachedEarningsEvent
    {
        public string Symbol { get; init; } = string.Empty;
        public DateTime Date { get; init; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Symbol Matching Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SymbolMatching_SameCase_Matches()
    {
        HashSet<string> symbolSet = new(StringComparer.OrdinalIgnoreCase) { "JPM", "BAC" };

        Assert.Contains("JPM", symbolSet);
        Assert.Contains("BAC", symbolSet);
    }

    [Fact]
    public void SymbolMatching_DifferentCase_Matches()
    {
        HashSet<string> symbolSet = new(StringComparer.OrdinalIgnoreCase) { "JPM", "BAC" };

        Assert.Contains("jpm", symbolSet);
        Assert.Contains("Jpm", symbolSet);
        Assert.Contains("bac", symbolSet);
    }

    [Fact]
    public void SymbolMatching_NonExistentSymbol_DoesNotMatch()
    {
        HashSet<string> symbolSet = new(StringComparer.OrdinalIgnoreCase) { "JPM", "BAC" };

        Assert.DoesNotContain("WFC", symbolSet);
        Assert.DoesNotContain("AAPL", symbolSet);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Evaluation Window Calculation Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EvaluationWindow_CalculatesCorrectDates()
    {
        // Earnings on April 12, with 5-7 day window
        DateTime earningsDate = new DateTime(2024, 4, 12);
        int minDays = 5;
        int maxDays = 7;

        List<DateTime> evalDates = new();
        for (int d = minDays; d <= maxDays; d++)
        {
            evalDates.Add(earningsDate.AddDays(-d));
        }

        // Should get April 5, 6, 7 (7, 6, 5 days before April 12)
        Assert.Contains(new DateTime(2024, 4, 5), evalDates);
        Assert.Contains(new DateTime(2024, 4, 6), evalDates);
        Assert.Contains(new DateTime(2024, 4, 7), evalDates);
        Assert.Equal(3, evalDates.Count);
    }

    [Fact]
    public void EvaluationWindow_WithWeekendFiltering_SkipsWeekends()
    {
        // Earnings on April 15 (Monday), with 5-7 day window
        DateTime earningsDate = new DateTime(2024, 4, 15);
        int minDays = 5;
        int maxDays = 7;

        List<DateTime> evalDates = new();
        for (int d = minDays; d <= maxDays; d++)
        {
            DateTime evalDate = earningsDate.AddDays(-d);
            if (evalDate.DayOfWeek != DayOfWeek.Saturday && evalDate.DayOfWeek != DayOfWeek.Sunday)
            {
                evalDates.Add(evalDate);
            }
        }

        // April 15 - 5 = April 10 (Wed) ✓
        // April 15 - 6 = April 9 (Tue) ✓
        // April 15 - 7 = April 8 (Mon) ✓
        Assert.Equal(3, evalDates.Count);
        Assert.DoesNotContain(evalDates, d => d.DayOfWeek == DayOfWeek.Saturday);
        Assert.DoesNotContain(evalDates, d => d.DayOfWeek == DayOfWeek.Sunday);
    }

    [Fact]
    public void EvaluationWindow_SpanningWeekend_SkipsWeekendDays()
    {
        // Earnings on April 10 (Wednesday), with 5-7 day window
        // 5 days before = April 5 (Fri) ✓
        // 6 days before = April 4 (Thu) ✓
        // 7 days before = April 3 (Wed) ✓
        DateTime earningsDate = new DateTime(2024, 4, 10);
        int minDays = 3;
        int maxDays = 6;

        List<DateTime> evalDates = new();
        for (int d = minDays; d <= maxDays; d++)
        {
            DateTime evalDate = earningsDate.AddDays(-d);
            if (evalDate.DayOfWeek != DayOfWeek.Saturday && evalDate.DayOfWeek != DayOfWeek.Sunday)
            {
                evalDates.Add(evalDate);
            }
        }

        // April 10 - 3 = April 7 (Sun) ✗
        // April 10 - 4 = April 6 (Sat) ✗
        // April 10 - 5 = April 5 (Fri) ✓
        // April 10 - 6 = April 4 (Thu) ✓
        Assert.Equal(2, evalDates.Count);
        Assert.Contains(new DateTime(2024, 4, 5), evalDates);
        Assert.Contains(new DateTime(2024, 4, 4), evalDates);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Session Range Filtering Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SessionRangeFiltering_EvalDateBeforeStart_IsExcluded()
    {
        DateTime sessionStart = new DateTime(2024, 4, 10);
        DateTime sessionEnd = new DateTime(2024, 12, 31);
        DateTime earningsDate = new DateTime(2024, 4, 12);
        int minDays = 5;
        int maxDays = 7;

        List<DateTime> evalDates = new();
        for (int d = minDays; d <= maxDays; d++)
        {
            DateTime evalDate = earningsDate.AddDays(-d);
            if (evalDate >= sessionStart && evalDate <= sessionEnd)
            {
                evalDates.Add(evalDate);
            }
        }

        // April 12 - 5 = April 7 < April 10 (excluded)
        // April 12 - 6 = April 6 < April 10 (excluded)
        // April 12 - 7 = April 5 < April 10 (excluded)
        Assert.Empty(evalDates);
    }

    [Fact]
    public void SessionRangeFiltering_PartialOverlap_IncludesOnlyValid()
    {
        DateTime sessionStart = new DateTime(2024, 4, 6);
        DateTime sessionEnd = new DateTime(2024, 12, 31);
        DateTime earningsDate = new DateTime(2024, 4, 12);
        int minDays = 5;
        int maxDays = 7;

        List<DateTime> evalDates = new();
        for (int d = minDays; d <= maxDays; d++)
        {
            DateTime evalDate = earningsDate.AddDays(-d);
            if (evalDate >= sessionStart && evalDate <= sessionEnd)
            {
                evalDates.Add(evalDate);
            }
        }

        // April 12 - 5 = April 7 ≥ April 6 ✓
        // April 12 - 6 = April 6 ≥ April 6 ✓
        // April 12 - 7 = April 5 < April 6 ✗
        Assert.Equal(2, evalDates.Count);
        Assert.Contains(new DateTime(2024, 4, 7), evalDates);
        Assert.Contains(new DateTime(2024, 4, 6), evalDates);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // JSON Parsing Tests (Integration with actual format)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void JsonParsing_RealNasdaqFormat_ParsesCorrectly()
    {
        // Actual format from NASDAQ earnings cache
        string json = """
        {
            "date": "2024-04-12T00:00:00+10:00",
            "fetchedAt": "2026-01-07T00:09:28.0671809Z",
            "earnings": [
                {
                    "symbol": "JPM",
                    "date": "2024-04-12T00:00:00+10:00",
                    "fiscalQuarter": "Mar/2024",
                    "timing": 3,
                    "epsEstimate": 4.18,
                    "epsActual": 4.63
                },
                {
                    "symbol": "WFC",
                    "date": "2024-04-12T00:00:00+10:00",
                    "fiscalQuarter": "Mar/2024",
                    "timing": 3
                }
            ]
        }
        """;

        CachedEarningsDay? cached = JsonSerializer.Deserialize<CachedEarningsDay>(json, JsonOptions);

        Assert.NotNull(cached);
        Assert.Equal(2, cached.Earnings.Count);
        Assert.Equal("JPM", cached.Earnings[0].Symbol);
        Assert.Equal("WFC", cached.Earnings[1].Symbol);
    }

    [Fact]
    public void JsonParsing_ExtractSymbolsForUniverse_Works()
    {
        string json = """
        {
            "date": "2024-04-12T00:00:00Z",
            "earnings": [
                { "symbol": "JPM", "date": "2024-04-12T00:00:00Z" },
                { "symbol": "BAC", "date": "2024-04-12T00:00:00Z" },
                { "symbol": "WFC", "date": "2024-04-12T00:00:00Z" }
            ]
        }
        """;

        HashSet<string> universeSymbols = new(StringComparer.OrdinalIgnoreCase) { "JPM", "BAC" };

        CachedEarningsDay? cached = JsonSerializer.Deserialize<CachedEarningsDay>(json, JsonOptions);

        Assert.NotNull(cached);
        
        List<CachedEarningsEvent> matchingEvents = cached.Earnings
            .Where(e => universeSymbols.Contains(e.Symbol))
            .ToList();

        Assert.Equal(2, matchingEvents.Count);
        Assert.DoesNotContain(matchingEvents, e => e.Symbol == "WFC");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Edge Cases
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EdgeCase_NoEarningsInFile_ReturnsEmptyList()
    {
        string json = """
        {
            "date": "2024-07-04T00:00:00Z",
            "earnings": []
        }
        """;

        CachedEarningsDay? cached = JsonSerializer.Deserialize<CachedEarningsDay>(json, JsonOptions);

        Assert.NotNull(cached);
        Assert.Empty(cached.Earnings);
    }

    [Fact]
    public void EdgeCase_EarningsForDifferentSymbols_NoMatches()
    {
        string json = """
        {
            "date": "2024-04-12T00:00:00Z",
            "earnings": [
                { "symbol": "AAPL", "date": "2024-04-12T00:00:00Z" },
                { "symbol": "MSFT", "date": "2024-04-12T00:00:00Z" }
            ]
        }
        """;

        HashSet<string> universeSymbols = new(StringComparer.OrdinalIgnoreCase) { "JPM", "BAC" };

        CachedEarningsDay? cached = JsonSerializer.Deserialize<CachedEarningsDay>(json, JsonOptions);

        Assert.NotNull(cached);
        
        List<CachedEarningsEvent> matchingEvents = cached.Earnings
            .Where(e => universeSymbols.Contains(e.Symbol))
            .ToList();

        Assert.Empty(matchingEvents);
    }

    [Fact]
    public void EdgeCase_HashSetDeduplication_Works()
    {
        // Multiple earnings events can generate the same evaluation date
        HashSet<DateTime> dates = new();
        
        // Two separate earnings events on different days
        DateTime earnings1 = new DateTime(2024, 4, 12);
        DateTime earnings2 = new DateTime(2024, 4, 13);
        
        // But they might share overlapping evaluation dates
        dates.Add(earnings1.AddDays(-5)); // April 7
        dates.Add(earnings2.AddDays(-6)); // April 7 - same date!
        
        Assert.Single(dates); // HashSet deduplicates
    }

    [Fact]
    public void EdgeCase_MinDaysEqualsMaxDays_SingleDate()
    {
        DateTime earningsDate = new DateTime(2024, 4, 12);
        int minDays = 5;
        int maxDays = 5; // Same as min

        List<DateTime> evalDates = new();
        for (int d = minDays; d <= maxDays; d++)
        {
            evalDates.Add(earningsDate.AddDays(-d));
        }

        Assert.Single(evalDates);
        Assert.Equal(new DateTime(2024, 4, 7), evalDates[0]);
    }
}
