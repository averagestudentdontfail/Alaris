// =============================================================================
// DTmd003ATests.cs - Corporate Actions Model Tests
// Component: TSun011A | Category: Unit | Variant: A (Primary)
// =============================================================================
// Tests for DTmd003A corporate action model adjustment computations.
// =============================================================================

using Alaris.Infrastructure.Data.Model;
using Xunit;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for DTmd003A corporate action model.
/// </summary>
public sealed class DTmd003ATests
{
    // =========================================================================
    // Stock Split Tests
    // =========================================================================

    [Fact]
    public void GetPriceMultiplier_TwoForOneSplit_ReturnsHalf()
    {
        // Arrange: 2:1 split (2 new shares for 1 old share)
        var action = new DTmd003A(
            Symbol: "AAPL",
            ExDate: new DateTime(2024, 1, 15),
            Type: CorporateActionType.Split,
            Factor: 2m,
            Description: "2-for-1 stock split");

        // Act
        var multiplier = action.GetPriceMultiplier();

        // Assert: Price should be halved (1/2 = 0.5)
        Assert.Equal(0.5m, multiplier);
    }

    [Fact]
    public void GetStrikeMultiplier_TwoForOneSplit_ReturnsHalf()
    {
        // Arrange
        var action = new DTmd003A(
            Symbol: "AAPL",
            ExDate: new DateTime(2024, 1, 15),
            Type: CorporateActionType.Split,
            Factor: 2m,
            Description: "2-for-1 stock split");

        // Act
        var multiplier = action.GetStrikeMultiplier();

        // Assert: Strike should be halved
        Assert.Equal(0.5m, multiplier);
    }

    [Fact]
    public void GetPriceMultiplier_FourForOneSplit_ReturnsQuarter()
    {
        // Arrange: 4:1 split (like NVDA 2024)
        var action = new DTmd003A(
            Symbol: "NVDA",
            ExDate: new DateTime(2024, 6, 10),
            Type: CorporateActionType.Split,
            Factor: 4m,
            Description: "4-for-1 stock split");

        // Act
        var multiplier = action.GetPriceMultiplier();

        // Assert: Price should be quartered (1/4 = 0.25)
        Assert.Equal(0.25m, multiplier);
    }

    // =========================================================================
    // Reverse Split Tests
    // =========================================================================

    [Fact]
    public void GetPriceMultiplier_OneForTenReverseSplit_ReturnsTen()
    {
        // Arrange: 1:10 reverse split (10 old shares become 1 new share)
        var action = new DTmd003A(
            Symbol: "GE",
            ExDate: new DateTime(2024, 1, 1),
            Type: CorporateActionType.ReverseSplit,
            Factor: 10m,
            Description: "1-for-10 reverse split");

        // Act
        var multiplier = action.GetPriceMultiplier();

        // Assert: Price should be multiplied by 10
        Assert.Equal(10m, multiplier);
    }

    [Fact]
    public void GetStrikeMultiplier_OneForTenReverseSplit_ReturnsTen()
    {
        // Arrange
        var action = new DTmd003A(
            Symbol: "GE",
            ExDate: new DateTime(2024, 1, 1),
            Type: CorporateActionType.ReverseSplit,
            Factor: 10m,
            Description: "1-for-10 reverse split");

        // Act
        var multiplier = action.GetStrikeMultiplier();

        // Assert: Strike should be multiplied by 10
        Assert.Equal(10m, multiplier);
    }

    // =========================================================================
    // Stock Dividend Tests
    // =========================================================================

    [Fact]
    public void GetPriceMultiplier_FivePercentStockDividend_ReturnsAdjustedMultiplier()
    {
        // Arrange: 5% stock dividend (0.05 new shares per old share)
        var action = new DTmd003A(
            Symbol: "TEST",
            ExDate: new DateTime(2024, 1, 1),
            Type: CorporateActionType.StockDividend,
            Factor: 0.05m,
            Description: "5% stock dividend");

        // Act
        var multiplier = action.GetPriceMultiplier();

        // Assert: Price adjusted by 1/(1+0.05) = 0.952380952...
        Assert.True(multiplier > 0.952m && multiplier < 0.953m);
    }

    // =========================================================================
    // Cash Dividend Tests (No Price Adjustment)
    // =========================================================================

    [Fact]
    public void GetPriceMultiplier_CashDividend_ReturnsOne()
    {
        // Arrange: Cash dividend doesn't require price adjustment
        var action = new DTmd003A(
            Symbol: "MSFT",
            ExDate: new DateTime(2024, 3, 15),
            Type: CorporateActionType.CashDividend,
            Factor: 0.75m,
            Description: "$0.75 quarterly dividend");

        // Act
        var multiplier = action.GetPriceMultiplier();

        // Assert: Cash dividends don't adjust historical prices in backtesting
        Assert.Equal(1m, multiplier);
    }

    [Fact]
    public void GetStrikeMultiplier_CashDividend_ReturnsOne()
    {
        // Arrange
        var action = new DTmd003A(
            Symbol: "MSFT",
            ExDate: new DateTime(2024, 3, 15),
            Type: CorporateActionType.CashDividend,
            Factor: 0.75m,
            Description: "$0.75 quarterly dividend");

        // Act
        var multiplier = action.GetStrikeMultiplier();

        // Assert: Cash dividends don't adjust strikes
        Assert.Equal(1m, multiplier);
    }

    // =========================================================================
    // RequiresPriceAdjustment Property Tests
    // =========================================================================

    [Theory]
    [InlineData(CorporateActionType.Split, true)]
    [InlineData(CorporateActionType.ReverseSplit, true)]
    [InlineData(CorporateActionType.StockDividend, true)]
    [InlineData(CorporateActionType.CashDividend, false)]
    [InlineData(CorporateActionType.SpinOff, false)]
    [InlineData(CorporateActionType.Merger, false)]
    public void RequiresPriceAdjustment_VariousTypes_ReturnsExpected(
        CorporateActionType type, bool expected)
    {
        // Arrange
        var action = new DTmd003A(
            Symbol: "TEST",
            ExDate: DateTime.Today,
            Type: type,
            Factor: 2m,
            Description: "Test action");

        // Act & Assert
        Assert.Equal(expected, action.RequiresPriceAdjustment);
    }

    // =========================================================================
    // RequiresStrikeAdjustment Property Tests
    // =========================================================================

    [Theory]
    [InlineData(CorporateActionType.Split, true)]
    [InlineData(CorporateActionType.ReverseSplit, true)]
    [InlineData(CorporateActionType.StockDividend, false)]
    [InlineData(CorporateActionType.CashDividend, false)]
    public void RequiresStrikeAdjustment_VariousTypes_ReturnsExpected(
        CorporateActionType type, bool expected)
    {
        // Arrange
        var action = new DTmd003A(
            Symbol: "TEST",
            ExDate: DateTime.Today,
            Type: type,
            Factor: 2m,
            Description: "Test action");

        // Act & Assert
        Assert.Equal(expected, action.RequiresStrikeAdjustment);
    }

    // =========================================================================
    // Record Equality Tests
    // =========================================================================

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var date = new DateTime(2024, 1, 15);
        var action1 = new DTmd003A("AAPL", date, CorporateActionType.Split, 2m, "Split");
        var action2 = new DTmd003A("AAPL", date, CorporateActionType.Split, 2m, "Split");

        // Assert
        Assert.Equal(action1, action2);
    }

    [Fact]
    public void Equals_DifferentSymbol_ReturnsFalse()
    {
        // Arrange
        var date = new DateTime(2024, 1, 15);
        var action1 = new DTmd003A("AAPL", date, CorporateActionType.Split, 2m, "Split");
        var action2 = new DTmd003A("NVDA", date, CorporateActionType.Split, 2m, "Split");

        // Assert
        Assert.NotEqual(action1, action2);
    }
}
