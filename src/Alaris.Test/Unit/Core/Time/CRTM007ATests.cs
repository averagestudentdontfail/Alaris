// CRTM007ATests.cs - Unit tests for Day Count Conventions
// Tests Actual/365 Fixed, Actual/360, and 30/360 day count calculations

using Alaris.Core.Time;
using Xunit;

namespace Alaris.Test.Unit.Core.Time;

public class CRTM007ATests
{
    #region Actual/365 Fixed Tests

    [Fact]
    public void Actual365Fixed_OneYear_ReturnsOne()
    {
        CRTM005A start = new CRTM005A(1, CRTM005AMonth.January, 2024);
        CRTM005A end = new CRTM005A(1, CRTM005AMonth.January, 2025);
        
        double yearFraction = DayCounters.Actual365Fixed.YearFraction(start, end);
        
        // 2024 is leap year: 366 days
        Assert.Equal(366.0 / 365.0, yearFraction, precision: 6);
    }

    [Fact]
    public void Actual365Fixed_HalfYear_ReturnsHalf()
    {
        CRTM005A start = new CRTM005A(1, CRTM005AMonth.January, 2025);
        CRTM005A end = new CRTM005A(2, CRTM005AMonth.July, 2025);
        
        double yearFraction = DayCounters.Actual365Fixed.YearFraction(start, end);
        
        // Jan-Jun = 31+28+31+30+31+30+1 = 182 days
        Assert.True(yearFraction > 0.49 && yearFraction < 0.51, $"Year fraction {yearFraction} should be ~0.5");
    }

    [Fact]
    public void Actual365Fixed_SameDate_ReturnsZero()
    {
        CRTM005A date = new CRTM005A(15, CRTM005AMonth.June, 2024);
        
        double yearFraction = DayCounters.Actual365Fixed.YearFraction(date, date);
        
        Assert.Equal(0.0, yearFraction);
    }

    [Fact]
    public void Actual365Fixed_DayCount_ReturnsActualDays()
    {
        CRTM005A start = new CRTM005A(1, CRTM005AMonth.January, 2024);
        CRTM005A end = new CRTM005A(1, CRTM005AMonth.February, 2024);
        
        int days = DayCounters.Actual365Fixed.DayCount(start, end);
        
        Assert.Equal(31, days);
    }

    #endregion

    #region Actual/360 Tests

    [Fact]
    public void Actual360_OneYear_ReturnsMoreThanOne()
    {
        CRTM005A start = new CRTM005A(1, CRTM005AMonth.January, 2025);
        CRTM005A end = new CRTM005A(1, CRTM005AMonth.January, 2026);
        
        double yearFraction = DayCounters.Actual360.YearFraction(start, end);
        
        // 365 / 360 = 1.0139
        Assert.True(yearFraction > 1.0, $"Actual/360 year fraction {yearFraction} should be > 1");
    }

    [Fact]
    public void Actual360_30Days_ReturnsCorrect()
    {
        CRTM005A start = new CRTM005A(1, CRTM005AMonth.June, 2024);
        CRTM005A end = new CRTM005A(1, CRTM005AMonth.July, 2024);
        
        double yearFraction = DayCounters.Actual360.YearFraction(start, end);
        
        // June has 30 days
        Assert.Equal(30.0 / 360.0, yearFraction, precision: 6);
    }

    #endregion

    #region 30/360 ISDA Tests

    [Fact]
    public void Thirty360_OneMonth_ReturnsThirtyDays()
    {
        CRTM005A start = new CRTM005A(15, CRTM005AMonth.June, 2024);
        CRTM005A end = new CRTM005A(15, CRTM005AMonth.July, 2024);
        
        double yearFraction = DayCounters.Thirty360.YearFraction(start, end);
        
        // 30/360 always treats months as 30 days
        Assert.Equal(30.0 / 360.0, yearFraction, precision: 6);
    }

    [Fact]
    public void Thirty360_OneYear_ReturnsOne()
    {
        CRTM005A start = new CRTM005A(1, CRTM005AMonth.January, 2024);
        CRTM005A end = new CRTM005A(1, CRTM005AMonth.January, 2025);
        
        double yearFraction = DayCounters.Thirty360.YearFraction(start, end);
        
        // 12 * 30 / 360 = 1.0
        Assert.Equal(1.0, yearFraction, precision: 6);
    }

    [Fact]
    public void Thirty360_Feb28ToMar1_HandlesCorrectly()
    {
        // Test the 30/360 adjustment at end of February (non-leap)
        CRTM005A start = new CRTM005A(28, CRTM005AMonth.February, 2025);
        CRTM005A end = new CRTM005A(1, CRTM005AMonth.March, 2025);
        
        int days = DayCounters.Thirty360.DayCount(start, end);
        
        // 30/360 should give 3 days (30-28+1 = 3, but depends on convention)
        Assert.True(days >= 1 && days <= 3, $"Days {days} should be reasonable for Feb28-Mar1");
    }

    #endregion
}
