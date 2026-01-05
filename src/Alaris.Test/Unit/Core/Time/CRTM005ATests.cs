// CRTM005ATests.cs - Unit tests for native Date type
// Tests date arithmetic and serial number conversion for Alaris date semantics

using Alaris.Core.Time;
using Xunit;

namespace Alaris.Test.Unit.Core.Time;

public class CRTM005ATests
{
    #region Construction Tests

    [Fact]
    public void Constructor_ValidDate_CreatesCorrectSerialNumber()
    {
        var date = new CRTM005A(1, CRTM005AMonth.January, 2000);
        
        Assert.True(date.SerialNumber > 0);
        Assert.Equal(1, date.Day);
        Assert.Equal(1, date.Month);  // Month is int (1-12)
        Assert.Equal(2000, date.Year);
    }

    [Fact]
    public void Constructor_LeapYear_Feb29Valid()
    {
        var date = new CRTM005A(29, CRTM005AMonth.February, 2024);
        
        Assert.Equal(29, date.Day);
        Assert.Equal(2, date.Month);  // February = 2
        Assert.Equal(2024, date.Year);
    }

    [Fact]
    public void FromDateTime_RoundTrips()
    {
        var dt = new DateTime(2024, 6, 15);
        CRTM005A date = CRTM005A.FromDateTime(dt);
        DateTime back = date.ToDateTime();
        
        Assert.Equal(dt, back);
    }

    #endregion

    #region Arithmetic Tests

    [Fact]
    public void Subtraction_TwoDates_ReturnsDaysBetween()
    {
        var date1 = new CRTM005A(1, CRTM005AMonth.January, 2024);
        var date2 = new CRTM005A(31, CRTM005AMonth.January, 2024);
        
        int daysBetween = date2 - date1;
        
        Assert.Equal(30, daysBetween);
    }

    [Fact]
    public void AddDays_30Days_ReturnsCorrectDate()
    {
        var date = new CRTM005A(15, CRTM005AMonth.January, 2024);
        CRTM005A result = date.AddDays(30);
        
        Assert.Equal(14, result.Day);
        Assert.Equal(2, result.Month);  // February = 2
    }

    [Fact]
    public void AddDays_CrossingYearBoundary_Works()
    {
        var date = new CRTM005A(25, CRTM005AMonth.December, 2024);
        CRTM005A result = date.AddDays(10);
        
        Assert.Equal(2025, result.Year);
        Assert.Equal(1, result.Month);  // January = 1
    }

    [Fact]
    public void AddDays_Negative_SubtractsDays()
    {
        var date = new CRTM005A(15, CRTM005AMonth.February, 2024);
        CRTM005A result = date.AddDays(-20);
        
        Assert.Equal(1, result.Month);  // January = 1
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void Comparison_EarlierDate_IsLess()
    {
        var earlier = new CRTM005A(1, CRTM005AMonth.January, 2024);
        var later = new CRTM005A(1, CRTM005AMonth.February, 2024);
        
        Assert.True(earlier < later);
        Assert.True(later > earlier);
        Assert.True(earlier <= later);
        Assert.True(later >= earlier);
        Assert.False(earlier == later);
    }

    [Fact]
    public void Equality_SameDate_AreEqual()
    {
        var date1 = new CRTM005A(15, CRTM005AMonth.June, 2024);
        var date2 = new CRTM005A(15, CRTM005AMonth.June, 2024);
        
        Assert.True(date1 == date2);
        Assert.True(date1.Equals(date2));
        Assert.Equal(date1.GetHashCode(), date2.GetHashCode());
    }

    #endregion

    #region Day of Week Tests

    [Fact]
    public void DayOfWeek_KnownDate_ReturnsCorrect()
    {
        // June 15, 2024 is a Saturday
        var date = new CRTM005A(15, CRTM005AMonth.June, 2024);
        
        Assert.Equal(DayOfWeek.Saturday, date.DayOfWeek);
    }

    [Fact]
    public void DayOfWeek_Saturday_IsWeekend()
    {
        var saturday = new CRTM005A(15, CRTM005AMonth.June, 2024);
        
        bool isWeekend = saturday.DayOfWeek == DayOfWeek.Saturday || saturday.DayOfWeek == DayOfWeek.Sunday;
        Assert.True(isWeekend);
    }

    [Fact]
    public void DayOfWeek_Wednesday_IsNotWeekend()
    {
        var wednesday = new CRTM005A(12, CRTM005AMonth.June, 2024);
        
        bool isWeekend = wednesday.DayOfWeek == DayOfWeek.Saturday || wednesday.DayOfWeek == DayOfWeek.Sunday;
        Assert.False(isWeekend);
    }

    #endregion
}
