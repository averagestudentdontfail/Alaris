// TSBT001A.cs - Backtest data requirements and verification tests

using System;
using System.Collections.Generic;
using Alaris.Core.Model;
using Alaris.Host.Application.Service;
using Xunit;

namespace Alaris.Test.Backtest;

/// <summary>
/// Test suite for backtest data requirements model and verification.
/// Component ID: TSBT001A
/// </summary>
public sealed class TSBT001A
{
    [Fact]
    public void STDT010A_EarningsLookaheadEnd_Computes120DaysAfterEndDate()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 2, 1),
            EndDate = new DateTime(2026, 1, 1),
            Symbols = new[] { "NVDA" }
        };
        
        // Act
        DateTime lookaheadEnd = req.EarningsLookaheadEnd;
        
        // Assert
        Assert.Equal(new DateTime(2026, 5, 1), lookaheadEnd);
    }
    
    [Fact]
    public void STDT010A_PriceDataStart_Computes120DaysBeforeStartDate()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 2, 1),
            EndDate = new DateTime(2026, 1, 1),
            Symbols = new[] { "NVDA" }
        };
        
        // Act
        DateTime priceStart = req.PriceDataStart;
        
        // Assert
        Assert.Equal(new DateTime(2023, 10, 4), priceStart);
    }
    
    [Fact]
    public void STDT010A_AllSymbols_IncludesBenchmarkWhenNotPresent()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "NVDA", "AAPL" }
        };
        
        // Act
        IReadOnlyList<string> allSymbols = req.AllSymbols;
        
        // Assert
        Assert.Equal(3, allSymbols.Count);
        Assert.Contains("SPY", allSymbols);
    }
    
    [Fact]
    public void STDT010A_AllSymbols_DoesNotDuplicateBenchmark()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "NVDA", "SPY", "AAPL" }
        };
        
        // Act
        IReadOnlyList<string> allSymbols = req.AllSymbols;
        
        // Assert
        Assert.Equal(3, allSymbols.Count);
    }
    
    [Fact]
    public void STDT010A_Validate_ReturnsErrorForInvalidDateRange()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 12, 31),
            EndDate = new DateTime(2024, 1, 1), // EndDate before StartDate
            Symbols = new[] { "NVDA" }
        };
        
        // Act
        bool isValid;
        string? error;
        (isValid, error) = req.Validate();
        
        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("StartDate", error, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void STDT010A_Validate_ReturnsErrorForEmptySymbols()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = Array.Empty<string>()
        };
        
        // Act
        bool isValid;
        string? error;
        (isValid, error) = req.Validate();
        
        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("symbol", error, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void STDT010A_Validate_ReturnsErrorForInvalidSignalWindow()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "NVDA" },
            SignalWindowMinDays = 7,
            SignalWindowMaxDays = 5 // Invalid: max < min
        };
        
        // Act
        bool isValid;
        string? error;
        (isValid, error) = req.Validate();
        
        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("signal window", error, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void STDT010A_Validate_ReturnsErrorForInsufficientWarmup()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "NVDA" },
            WarmupDays = 20 // Too short
        };
        
        // Act
        bool isValid;
        string? error;
        (isValid, error) = req.Validate();
        
        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("WarmupDays", error, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void STDT010A_Validate_SucceedsForValidRequirements()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "NVDA", "AAPL" }
        };
        
        // Act
        bool isValid;
        string? error;
        (isValid, error) = req.Validate();
        
        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }
    
    [Fact]
    public void STDT010A_WithOptionsRequiredDates_ReturnsNewInstanceWithDates()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "NVDA" }
        };
        List<DateTime> dates = new List<DateTime>
        {
            new DateTime(2024, 2, 15),
            new DateTime(2024, 4, 15)
        };
        
        // Act
        STDT010A updated = req.WithOptionsRequiredDates(dates);
        
        // Assert
        Assert.Empty(req.OptionsRequiredDates); // Original unchanged
        Assert.Equal(2, updated.OptionsRequiredDates.Count);
    }
    
    [Fact]
    public void STDT010A_GetSummary_ReturnsFormattedString()
    {
        // Arrange
        STDT010A req = new STDT010A
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Symbols = new[] { "NVDA", "AAPL" }
        };
        
        // Act
        string summary = req.GetSummary();
        
        // Assert
        Assert.Contains("2024-01-01", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2024-12-31", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 symbols", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DataVerificationReport_GetSummary_ReturnsSuccessWhenComplete()
    {
        // Arrange
        DataVerificationReport report = new DataVerificationReport
        {
            IsComplete = true,
            BenchmarkAvailable = true
        };
        
        // Act
        string summary = report.GetSummary();
        
        // Assert
        Assert.Contains("available", summary, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void DataVerificationReport_GetSummary_ListsMissingData()
    {
        // Arrange
        DataVerificationReport report = new DataVerificationReport
        {
            IsComplete = false,
            BenchmarkAvailable = false
        };
        report.MissingPriceData.Add("NVDA");
        report.MissingMapFiles.Add("AAPL");
        
        // Act
        string summary = report.GetSummary();
        
        // Assert
        Assert.Contains("NVDA", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AAPL", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("benchmark", summary, StringComparison.OrdinalIgnoreCase);
    }
}
