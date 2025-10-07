using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for the DoubleBoundaryApproximation class.
/// </summary>
public class DoubleBoundaryApproximationTests
{
    [Fact]
    public void DoubleBoundaryApproximation_CalculatesCallBoundary()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var maturity = 1.0;
        var rate = 0.05;
        var dividend = 0.02;
        var volatility = 0.20;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, rate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividend, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var approximation = new DoubleBoundaryApproximation(process, strike, maturity, rate, dividend, volatility);

        // Act
        var result = approximation.Calculate(spot, isCall: true);

        // Assert
        result.Should().NotBeNull();
        result.UpperBoundary.Should().BeGreaterThan(strike);
    }

    [Fact]
    public void DoubleBoundaryApproximation_CalculatesPutBoundary()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var maturity = 1.0;
        var rate = 0.05;
        var dividend = 0.02;
        var volatility = 0.20;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, rate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividend, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var approximation = new DoubleBoundaryApproximation(process, strike, maturity, rate, dividend, volatility);

        // Act
        var result = approximation.Calculate(spot, isCall: false);

        // Assert
        result.Should().NotBeNull();
        result.LowerBoundary.Should().BeLessThan(strike);
    }

    [Fact]
    public void DoubleBoundaryApproximation_HandlesNegativeRates()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var maturity = 1.0;
        var rate = -0.01; // Negative rate
        var dividend = 0.00;
        var volatility = 0.20;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, rate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividend, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var approximation = new DoubleBoundaryApproximation(process, strike, maturity, rate, dividend, volatility);

        // Act
        var result = approximation.Calculate(spot, isCall: true);

        // Assert
        result.Should().NotBeNull();
        double.IsNaN(result.UpperBoundary).Should().BeFalse();
    }

    [Fact]
    public void DoubleBoundaryApproximation_ApproximatesValue()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var maturity = 1.0;
        var rate = 0.05;
        var dividend = 0.02;
        var volatility = 0.20;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, rate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividend, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var approximation = new DoubleBoundaryApproximation(process, strike, maturity, rate, dividend, volatility);
        var boundaries = approximation.Calculate(spot, isCall: true);

        // Act
        var value = approximation.ApproximateValue(spot, strike, isCall: true, boundaries);

        // Assert
        value.Should().BeGreaterThan(0);
        value.Should().BeLessThan(spot);
    }
}