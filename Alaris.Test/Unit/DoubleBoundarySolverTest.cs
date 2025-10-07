using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for the DoubleBoundarySolver class.
/// </summary>
public class DoubleBoundarySolverTests
{
    [Fact]
    public void DoubleBoundarySolver_CalculatesCallBoundary()
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

        var solver = new DoubleBoundarySolver(process, strike, maturity, rate, dividend, volatility);

        // Act
        var boundaries = solver.SolveBoundaries(spot, isCall: true);

        // Assert
        boundaries.Should().NotBeNull();
        boundaries.UpperBoundary.Should().BeGreaterThan(strike);
        boundaries.LowerBoundary.Should().Be(0);
    }

    [Fact]
    public void DoubleBoundarySolver_CalculatesPutBoundary()
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

        var solver = new DoubleBoundarySolver(process, strike, maturity, rate, dividend, volatility);

        // Act
        var boundaries = solver.SolveBoundaries(spot, isCall: false);

        // Assert
        boundaries.Should().NotBeNull();
        boundaries.LowerBoundary.Should().BeLessThan(strike);
        boundaries.UpperBoundary.Should().Be(double.PositiveInfinity);
    }

    [Fact]
    public void DoubleBoundarySolver_CalculatesOptionValue()
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

        var solver = new DoubleBoundarySolver(process, strike, maturity, rate, dividend, volatility);

        // Act
        var (value, boundaries) = solver.SolveWithValue(spot, strike, isCall: true);

        // Assert
        value.Should().BeGreaterThan(0);
        boundaries.Should().NotBeNull();
    }

    [Fact]
    public void DoubleBoundarySolver_PerformsSensitivityAnalysis()
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

        var solver = new DoubleBoundarySolver(process, strike, maturity, rate, dividend, volatility);

        // Act
        var results = solver.AnalyzeSensitivity(80, 120, 10, strike, isCall: true);

        // Assert
        results.Should().HaveCount(10);
        results.First().Spot.Should().Be(80);
        results.Last().Spot.Should().BeApproximately(120, 0.01);
    }
}