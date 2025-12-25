// TSUN022A.cs - Trading calendar unit tests

using Alaris.Strategy.Calendar;
using FluentAssertions;
using Xunit;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for the NYSE trading calendar (STCL001A).
/// </summary>
/// <remarks>
/// Tests validate that the calendar correctly excludes weekends and holidays.
/// </remarks>
public sealed class TSUN022A : IDisposable
{
    private readonly STCL001A _calendar;

    public TSUN022A()
    {
        _calendar = new STCL001A();
    }

    public void Dispose()
    {
        _calendar.Dispose();
    }


    /// <summary>
    /// Verifies weekends are not business days.
    /// </summary>
    [Fact]
    public void IsBusinessDay_Weekend_ReturnsFalse()
    {
        // Saturday
        DateTime saturday = new DateTime(2024, 12, 7);
        _calendar.IsBusinessDay(saturday).Should().BeFalse("Saturday is not a business day");

        // Sunday
        DateTime sunday = new DateTime(2024, 12, 8);
        _calendar.IsBusinessDay(sunday).Should().BeFalse("Sunday is not a business day");
    }

    /// <summary>
    /// Verifies weekdays are business days (when not holidays).
    /// </summary>
    [Fact]
    public void IsBusinessDay_Weekday_ReturnsTrue()
    {
        // Normal Monday-Friday
        DateTime monday = new DateTime(2024, 12, 9);
        _calendar.IsBusinessDay(monday).Should().BeTrue("Monday is a business day");

        DateTime friday = new DateTime(2024, 12, 13);
        _calendar.IsBusinessDay(friday).Should().BeTrue("Friday is a business day");
    }



    /// <summary>
    /// Verifies NYSE holidays are not business days.
    /// </summary>
    [Theory]
    [InlineData(2024, 1, 1, "New Year's Day")]
    [InlineData(2024, 7, 4, "Independence Day")]
    [InlineData(2024, 12, 25, "Christmas Day")]
    public void IsBusinessDay_NyseHoliday_ReturnsFalse(int year, int month, int day, string holiday)
    {
        DateTime date = new DateTime(year, month, day);
        _calendar.IsBusinessDay(date).Should().BeFalse($"{holiday} should not be a business day");
    }



    /// <summary>
    /// Verifies business days calculation excludes weekends.
    /// </summary>
    [Fact]
    public void GetBusinessDaysBetween_OneWeek_ReturnsFiveDays()
    {
        // Monday to next Monday = 5 business days
        DateTime monday = new DateTime(2024, 12, 9);
        DateTime nextMonday = new DateTime(2024, 12, 16);

        int businessDays = _calendar.GetBusinessDaysBetween(monday, nextMonday);
        businessDays.Should().Be(5, "one week has 5 business days");
    }

    /// <summary>
    /// Verifies business days calculation handles same day correctly.
    /// </summary>
    [Fact]
    public void GetBusinessDaysBetween_SameDay_ReturnsZero()
    {
        DateTime date = new DateTime(2024, 12, 9);
        int businessDays = _calendar.GetBusinessDaysBetween(date, date);
        businessDays.Should().Be(0, "same day should return zero");
    }

    /// <summary>
    /// Verifies business days calculation handles reversed dates.
    /// </summary>
    [Fact]
    public void GetBusinessDaysBetween_ReversedDates_ReturnsZero()
    {
        DateTime earlier = new DateTime(2024, 12, 9);
        DateTime later = new DateTime(2024, 12, 16);

        int businessDays = _calendar.GetBusinessDaysBetween(later, earlier);
        businessDays.Should().Be(0, "reversed dates should return zero");
    }



    /// <summary>
    /// Verifies time-to-expiry calculation with calendar is more accurate than approximation.
    /// </summary>
    [Fact]
    public void GetTimeToExpiryInYears_VsApproximation_DiffersNearHolidays()
    {
        // Period spanning Christmas 2024 (12/23 Monday to 12/30 Monday)
        DateTime start = new DateTime(2024, 12, 23);
        DateTime end = new DateTime(2024, 12, 30);

        double calendarResult = _calendar.GetTimeToExpiryInYears(start, end);
        
        // Approximate: 7 calendar days * 5/7 = 5 trading days
        // Actual: Dec 23 (Mon), Dec 24 (Tue), Dec 25 (holiday), Dec 26 (Thu), Dec 27 (Fri) = 4 days
        // Plus Dec 28-29 weekend excluded, Dec 30 is end (excluded) = 4 business days
        
        calendarResult.Should().BeLessThan(5.0 / 252.0, 
            "calendar should account for Christmas holiday");
        calendarResult.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Verifies typical 2-week expiry calculation.
    /// </summary>
    [Fact]
    public void GetTimeToExpiryInYears_TwoWeeks_IsApproximatelyTenDays()
    {
        // Two weeks (normal, no holidays)
        DateTime start = new DateTime(2024, 2, 5); // Monday
        DateTime end = new DateTime(2024, 2, 19);  // Following Monday

        double timeToExpiry = _calendar.GetTimeToExpiryInYears(start, end);
        
        // Should be 10 business days / 252 ≈ 0.0397
        timeToExpiry.Should().BeApproximately(10.0 / 252.0, 0.001,
            "two weeks of business days should be approximately 10/252 years");
    }

}
