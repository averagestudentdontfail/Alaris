// TSUN053A.cs - Unit tests for STDT010A session data requirements model

using Alaris.Core.Model;
using Xunit;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STDT010A session data requirements model.
/// Component ID: TSUN053A
/// </summary>
/// <remarks>
/// Tests validate:
/// - Validation logic catches invalid configurations
/// - Computed properties calculate correctly
/// - Default values are sensible
/// - Edge cases are handled
/// </remarks>
public sealed class TSUN053A
{
    // ═══════════════════════════════════════════════════════════════════════
    // Validation Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_ValidRequirements_ReturnsSuccess()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM", "BAC" }
        };

        (bool isValid, string? error) = requirements.Validate();

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_EmptySymbols_ReturnsFalse()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = Array.Empty<string>()
        };

        (bool isValid, string? error) = requirements.Validate();

        Assert.False(isValid);
        Assert.Contains("symbol", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_StartDateAfterEndDate_ReturnsFalse()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 12, 31),
            EndDate = new DateTime(2024, 1, 1),
            Symbols = new[] { "JPM" }
        };

        (bool isValid, string? error) = requirements.Validate();

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_SameStartAndEndDate_ReturnsFalse()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 6, 15),
            EndDate = new DateTime(2024, 6, 15),
            Symbols = new[] { "JPM" }
        };

        (bool isValid, string? error) = requirements.Validate();

        Assert.False(isValid);
    }

    [Fact]
    public void Validate_NegativeSignalWindowMinDays_ReturnsFalse()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" },
            SignalWindowMinDays = -1
        };

        (bool isValid, string? error) = requirements.Validate();

        Assert.False(isValid);
    }

    [Fact]
    public void Validate_SignalWindowMaxLessThanMin_ReturnsFalse()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" },
            SignalWindowMinDays = 10,
            SignalWindowMaxDays = 5
        };

        (bool isValid, string? error) = requirements.Validate();

        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WarmupDaysTooSmall_ReturnsFalse()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" },
            WarmupDays = 10 // Less than minimum 30
        };

        (bool isValid, string? error) = requirements.Validate();

        Assert.False(isValid);
        Assert.Contains("warmup", error, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Computed Property Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void PriceDataStart_SubtractsWarmupDays()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 6, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" },
            WarmupDays = 120
        };

        DateTime expected = new DateTime(2024, 6, 1).AddDays(-120);

        Assert.Equal(expected, requirements.PriceDataStart);
    }

    [Fact]
    public void EarningsLookaheadEnd_AddsLookaheadDays()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 6, 30),
            Symbols = new[] { "JPM" },
            EarningsLookaheadDays = 120
        };

        DateTime expected = new DateTime(2024, 6, 30).AddDays(120);

        Assert.Equal(expected, requirements.EarningsLookaheadEnd);
    }

    [Fact]
    public void AllSymbols_IncludesBenchmarkWhenNotInList()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM", "BAC" },
            BenchmarkSymbol = "SPY"
        };

        IReadOnlyList<string> allSymbols = requirements.AllSymbols;

        Assert.Contains("SPY", allSymbols);
        Assert.Equal(3, allSymbols.Count);
    }

    [Fact]
    public void AllSymbols_DoesNotDuplicateBenchmarkWhenAlreadyIncluded()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "SPY", "JPM", "BAC" },
            BenchmarkSymbol = "SPY"
        };

        IReadOnlyList<string> allSymbols = requirements.AllSymbols;

        Assert.Equal(3, allSymbols.Count);
        Assert.Single(allSymbols.Where(s => s == "SPY"));
    }

    [Fact]
    public void AllSymbols_BenchmarkMatchingIsCaseInsensitive()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "spy", "JPM" }, // lowercase spy
            BenchmarkSymbol = "SPY" // uppercase SPY
        };

        IReadOnlyList<string> allSymbols = requirements.AllSymbols;

        // Should not duplicate because "spy" == "SPY" case-insensitively
        Assert.Equal(2, allSymbols.Count);
    }

    [Fact]
    public void AllSymbols_EmptySymbols_ReturnsOnlyBenchmark()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = Array.Empty<string>(),
            BenchmarkSymbol = "SPY"
        };

        IReadOnlyList<string> allSymbols = requirements.AllSymbols;

        Assert.Single(allSymbols);
        Assert.Equal("SPY", allSymbols[0]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Default Value Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Defaults_SignalWindowMinDays_IsFive()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" }
        };

        Assert.Equal(5, requirements.SignalWindowMinDays);
    }

    [Fact]
    public void Defaults_SignalWindowMaxDays_IsSeven()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" }
        };

        Assert.Equal(7, requirements.SignalWindowMaxDays);
    }

    [Fact]
    public void Defaults_WarmupDays_Is120()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" }
        };

        Assert.Equal(120, requirements.WarmupDays);
    }

    [Fact]
    public void Defaults_BenchmarkSymbol_IsSPY()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" }
        };

        Assert.Equal("SPY", requirements.BenchmarkSymbol);
    }

    [Fact]
    public void Defaults_OptionsRequiredDates_IsEmpty()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" }
        };

        Assert.Empty(requirements.OptionsRequiredDates);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Immutability Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void WithOptionsRequiredDates_CreatesNewInstance()
    {
        STDT010A original = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM" }
        };

        DateTime[] dates = new[] { new DateTime(2024, 3, 15), new DateTime(2024, 6, 15) };
        STDT010A updated = original.WithOptionsRequiredDates(dates);

        Assert.Empty(original.OptionsRequiredDates);
        Assert.Equal(2, updated.OptionsRequiredDates.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetSummary Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetSummary_ContainsDateRange()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM", "BAC" }
        };

        string summary = requirements.GetSummary();

        Assert.Contains("2024-01-01", summary, StringComparison.Ordinal);
        Assert.Contains("2024-12-31", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSummary_ContainsSymbolCount()
    {
        STDT010A requirements = new()
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "JPM", "BAC", "WFC" }
        };

        string summary = requirements.GetSummary();

        Assert.Contains("3 symbols", summary, StringComparison.Ordinal);
    }
}
