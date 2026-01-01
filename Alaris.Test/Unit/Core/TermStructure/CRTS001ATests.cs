// CRTS001ATests.cs - Unit tests for Yield Curve / Flat Forward
// Tests discount factors, zero rates, and forward rates

using Alaris.Core.TermStructure;
using Alaris.Core.Time;
using Xunit;

namespace Alaris.Test.Unit.Core.TermStructure;

public class CRTS001ATests
{
    private const double Tolerance = 1e-10;

    #region Discount Factor Tests

    [Fact]
    public void DiscountFactor_AtZero_ReturnsOne()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        var curve = new CRTS001AFlatForward(referenceDate, 0.05);
        
        double df = curve.DiscountFactor(0.0);
        
        Assert.Equal(1.0, df, precision: 10);
    }

    [Fact]
    public void DiscountFactor_PositiveRate_LessThanOne()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        double rate = 0.05;
        var curve = new CRTS001AFlatForward(referenceDate, rate);
        
        double df1 = curve.DiscountFactor(1.0);
        double df2 = curve.DiscountFactor(2.0);
        
        Assert.True(df1 < 1.0, "Discount factor should be less than 1 for positive rate");
        Assert.True(df2 < df1, "Discount factor should decrease with time");
    }

    [Fact]
    public void DiscountFactor_MatchesExponentialFormula()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        double rate = 0.05;
        double T = 2.0;
        var curve = new CRTS001AFlatForward(referenceDate, rate);
        
        double df = curve.DiscountFactor(T);
        double expected = System.Math.Exp(-rate * T);
        
        Assert.Equal(expected, df, Tolerance);
    }

    [Fact]
    public void DiscountFactor_ZeroRate_ReturnsOne()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        var curve = new CRTS001AFlatForward(referenceDate, 0.0);
        
        double df = curve.DiscountFactor(5.0);
        
        Assert.Equal(1.0, df, Tolerance);
    }

    [Fact]
    public void DiscountFactor_NegativeRate_GreaterThanOne()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        var curve = new CRTS001AFlatForward(referenceDate, -0.01);
        
        double df = curve.DiscountFactor(1.0);
        
        Assert.True(df > 1.0, "Discount factor should be > 1 for negative rate");
    }

    #endregion

    #region Zero Rate Tests

    [Fact]
    public void ZeroRate_FlatCurve_ReturnsFlatRate()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        double rate = 0.05;
        var curve = new CRTS001AFlatForward(referenceDate, rate);
        
        double zeroRate = curve.ZeroRate(1.0);
        
        Assert.Equal(rate, zeroRate, Tolerance);
    }

    [Fact]
    public void ZeroRate_ConsistentWithDiscountFactor()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        double rate = 0.05;
        double T = 2.0;
        var curve = new CRTS001AFlatForward(referenceDate, rate);
        
        double df = curve.DiscountFactor(T);
        double zeroRate = curve.ZeroRate(T);
        double dfFromZero = System.Math.Exp(-zeroRate * T);
        
        Assert.Equal(df, dfFromZero, Tolerance);
    }

    #endregion

    #region Forward Rate Tests

    [Fact]
    public void ForwardRate_FlatCurve_ReturnsFlatRate()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        double rate = 0.05;
        var curve = new CRTS001AFlatForward(referenceDate, rate);
        
        double fwdRate = curve.ForwardRate(1.0);
        
        Assert.Equal(rate, fwdRate, Tolerance);
    }

    [Fact]
    public void ForwardRate_ConsistentWithDiscountFactors()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        double rate = 0.05;
        double T1 = 1.0;
        double T2 = 2.0;
        var curve = new CRTS001AFlatForward(referenceDate, rate);
        
        double df1 = curve.DiscountFactor(T1);
        double df2 = curve.DiscountFactor(T2);
        double impliedFwd = -System.Math.Log(df2 / df1) / (T2 - T1);
        
        // For flat forward, the instantaneous forward rate at any point equals the flat rate
        double fwdRate = curve.ForwardRate(T1);
        
        Assert.Equal(rate, fwdRate, Tolerance);
        Assert.Equal(rate, impliedFwd, Tolerance);
    }

    #endregion

    #region Rate Property Tests

    [Fact]
    public void Rate_Property_ReturnsConstructorValue()
    {
        var referenceDate = new CRTM005A(1, CRTM005AMonth.January, 2024);
        double rate = 0.0375;
        var curve = new CRTS001AFlatForward(referenceDate, rate);
        
        Assert.Equal(rate, curve.Rate, Tolerance);
    }

    #endregion
}
