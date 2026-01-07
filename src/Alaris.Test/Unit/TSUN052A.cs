// TSUN052A.cs - Unit tests for JSON deserialization edge cases

using System.Text.Json;
using Xunit;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for JSON deserialization edge cases.
/// Component ID: TSUN052A
/// </summary>
/// <remarks>
/// Tests validate that JSON deserialization handles:
/// - Case-insensitive property matching (the bug we found)
/// - Missing optional properties
/// - Empty arrays
/// - Null values
/// - Round-trip serialization/deserialization
/// </remarks>
public sealed class TSUN052A
{
    private static readonly JsonSerializerOptions CaseSensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Test model matching the earnings cache structure
    private sealed class EarningsDay
    {
        public DateTime Date { get; init; }
        public DateTime FetchedAt { get; init; }
        public IReadOnlyList<EarningsEvent> Earnings { get; init; } = Array.Empty<EarningsEvent>();
    }

    private sealed class EarningsEvent
    {
        public string Symbol { get; init; } = string.Empty;
        public DateTime Date { get; init; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Case Sensitivity Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Deserialize_LowercaseProperties_WithCaseInsensitive_MapsCorrectly()
    {
        // This is the exact JSON format from NASDAQ cache files
        string json = """
        {
            "date": "2024-04-12T00:00:00Z",
            "fetchedAt": "2026-01-07T00:09:28Z",
            "earnings": [
                { "symbol": "JPM", "date": "2024-04-12T00:00:00Z" },
                { "symbol": "WFC", "date": "2024-04-12T00:00:00Z" }
            ]
        }
        """;

        EarningsDay? result = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(result);
        Assert.Equal(2, result.Earnings.Count);
        Assert.Equal("JPM", result.Earnings[0].Symbol);
        Assert.Equal("WFC", result.Earnings[1].Symbol);
    }

    [Fact]
    public void Deserialize_LowercaseProperties_WithCaseSensitive_FailsToMap()
    {
        // This demonstrates the bug: case-sensitive deserialization fails silently
        string json = """
        {
            "date": "2024-04-12T00:00:00Z",
            "earnings": [
                { "symbol": "JPM", "date": "2024-04-12T00:00:00Z" }
            ]
        }
        """;

        EarningsDay? result = JsonSerializer.Deserialize<EarningsDay>(json, CaseSensitiveOptions);

        Assert.NotNull(result);
        // With case-sensitive, the "earnings" property doesn't map to "Earnings"
        Assert.Empty(result.Earnings);
    }

    [Fact]
    public void Deserialize_PascalCaseProperties_WorksWithBothOptions()
    {
        string json = """
        {
            "Date": "2024-04-12T00:00:00Z",
            "Earnings": [
                { "Symbol": "JPM", "Date": "2024-04-12T00:00:00Z" }
            ]
        }
        """;

        EarningsDay? caseSensitiveResult = JsonSerializer.Deserialize<EarningsDay>(json, CaseSensitiveOptions);
        EarningsDay? caseInsensitiveResult = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(caseSensitiveResult);
        Assert.NotNull(caseInsensitiveResult);
        Assert.Single(caseSensitiveResult.Earnings);
        Assert.Single(caseInsensitiveResult.Earnings);
    }

    [Fact]
    public void Deserialize_MixedCaseProperties_RequiresCaseInsensitive()
    {
        // Some APIs return inconsistent casing
        string json = """
        {
            "Date": "2024-04-12T00:00:00Z",
            "earnings": [
                { "Symbol": "JPM", "date": "2024-04-12T00:00:00Z" }
            ]
        }
        """;

        EarningsDay? result = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(result);
        Assert.Single(result.Earnings);
        Assert.Equal("JPM", result.Earnings[0].Symbol);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Null and Empty Handling Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Deserialize_EmptyEarningsArray_ReturnsEmptyList()
    {
        string json = """
        {
            "date": "2024-04-12T00:00:00Z",
            "earnings": []
        }
        """;

        EarningsDay? result = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.Earnings);
        Assert.Empty(result.Earnings);
    }

    [Fact]
    public void Deserialize_MissingEarningsProperty_UsesDefaultEmptyArray()
    {
        string json = """
        {
            "date": "2024-04-12T00:00:00Z"
        }
        """;

        EarningsDay? result = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.Earnings);
        Assert.Empty(result.Earnings);
    }

    [Fact]
    public void Deserialize_NullEarningsProperty_BecomesNull()
    {
        string json = """
        {
            "date": "2024-04-12T00:00:00Z",
            "earnings": null
        }
        """;

        EarningsDay? result = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(result);
        // JSON null overrides the default initializer
        Assert.Null(result.Earnings);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Round-Trip Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Serialize_ThenDeserialize_PreservesData()
    {
        EarningsDay original = new()
        {
            Date = new DateTime(2024, 4, 12, 0, 0, 0, DateTimeKind.Utc),
            FetchedAt = DateTime.UtcNow,
            Earnings = new[]
            {
                new EarningsEvent { Symbol = "JPM", Date = new DateTime(2024, 4, 12) },
                new EarningsEvent { Symbol = "BAC", Date = new DateTime(2024, 4, 12) }
            }
        };

        string json = JsonSerializer.Serialize(original, CaseInsensitiveOptions);
        EarningsDay? roundTripped = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Date, roundTripped.Date);
        Assert.Equal(original.Earnings.Count, roundTripped.Earnings.Count);
        Assert.Equal("JPM", roundTripped.Earnings[0].Symbol);
        Assert.Equal("BAC", roundTripped.Earnings[1].Symbol);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Edge Cases
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Deserialize_EmptySymbol_PreservesEmptyString()
    {
        string json = """
        {
            "date": "2024-04-12T00:00:00Z",
            "earnings": [
                { "symbol": "", "date": "2024-04-12T00:00:00Z" }
            ]
        }
        """;

        EarningsDay? result = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(result);
        Assert.Single(result.Earnings);
        Assert.Equal(string.Empty, result.Earnings[0].Symbol);
    }

    [Fact]
    public void Deserialize_WhitespaceSymbol_PreservesWhitespace()
    {
        string json = """
        {
            "date": "2024-04-12T00:00:00Z",
            "earnings": [
                { "symbol": "  ", "date": "2024-04-12T00:00:00Z" }
            ]
        }
        """;

        EarningsDay? result = JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions);

        Assert.NotNull(result);
        Assert.Equal("  ", result.Earnings[0].Symbol);
    }

    [Fact]
    public void Deserialize_InvalidDateFormat_ThrowsJsonException()
    {
        string json = """
        {
            "date": "not-a-date",
            "earnings": []
        }
        """;

        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<EarningsDay>(json, CaseInsensitiveOptions));
    }
}
