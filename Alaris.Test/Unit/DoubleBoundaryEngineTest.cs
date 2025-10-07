using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for the DoubleBoundaryEngine American option pricing engine.
/// Tests pricing accuracy, Greek calculations, and edge cases including negative rates.
/// </summary>
public class DoubleBoundaryEngineTests
{
    [Fact]
    public void DoubleBoundaryEngine_PricesATMCallOption()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var riskFreeRate = 0.05;
        var dividendYield = 0.02;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, riskFreeRate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividendYield, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);
        option.setPricingEngine(engine); // Uses implicit conversion

        // Act
        var result = engine.Calculate(option);

        // Assert
        result.Price.Should().BeGreaterThan(0);
        result.Price.Should().BeLessThan(spot); // ATM call should be less than spot
        result.Delta.Should().BeGreaterThan(0).And.BeLessThan(1);
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
        result.Theta.Should().BeLessThan(0); // Time decay
    }

    [Fact]
    public void DoubleBoundaryEngine_PricesATMPutOption()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var riskFreeRate = 0.05;
        var dividendYield = 0.02;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Put, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, riskFreeRate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividendYield, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);
        option.setPricingEngine(engine);

        // Act
        var result = engine.Calculate(option);

        // Assert
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeLessThan(0).And.BeGreaterThan(-1);
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DoubleBoundaryEngine_HandlesDeepInTheMoneyCall()
    {
        // Arrange
        var spot = 120.0;
        var strike = 100.0;
        var riskFreeRate = 0.05;
        var dividendYield = 0.02;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, riskFreeRate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividendYield, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);
        option.setPricingEngine(engine);

        // Act
        var result = engine.Calculate(option);

        // Assert
        result.Price.Should().BeGreaterThan(spot - strike); // Should exceed intrinsic value
        result.Delta.Should().BeInRange(0.85, 1.0); // Deep ITM call has delta near 1
    }

    [Fact]
    public void DoubleBoundaryEngine_HandlesDeepOutOfTheMoneyPut()
    {
        // Arrange
        var spot = 120.0;
        var strike = 100.0;
        var riskFreeRate = 0.05;
        var dividendYield = 0.02;
        var volatility = 0.20;
        var maturity = 0.25;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Put, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, riskFreeRate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividendYield, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);
        option.setPricingEngine(engine);

        // Act
        var result = engine.Calculate(option);

        // Assert
        result.Price.Should().BeGreaterThan(0).And.BeLessThan(1.0); // OTM put has small value
        result.Delta.Should().BeInRange(-0.1, 0.0); // OTM put has delta near 0
    }

    [Fact]
    public void DoubleBoundaryEngine_HandlesNegativeInterestRates()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var riskFreeRate = -0.01; // Negative rate (European scenario)
        var dividendYield = 0.00;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, riskFreeRate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividendYield, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);
        option.setPricingEngine(engine);

        // Act
        var result = engine.Calculate(option);

        // Assert
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DoubleBoundaryEngine_CalculatesAllGreeks()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var riskFreeRate = 0.05;
        var dividendYield = 0.02;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, riskFreeRate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, dividendYield, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);
        option.setPricingEngine(engine);

        // Act
        var result = engine.Calculate(option);

        // Assert
        result.Delta.Should().NotBe(0);
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
        result.Theta.Should().BeLessThan(0); // Time decay
        result.Rho.Should().NotBe(0);
    }

    [Fact]
    public void DoubleBoundaryEngine_CalculateWithTiming_ReturnsValidResults()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var riskFreeRate = 0.05;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, riskFreeRate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, 0.0, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);

        // Act
        var (result, elapsed) = engine.CalculateWithTiming(option);

        // Assert
        result.Price.Should().BeGreaterThan(0);
        elapsed.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void DoubleBoundaryEngine_SensitivityAnalysis_GeneratesCorrectProfile()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var riskFreeRate = 0.05;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, riskFreeRate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, 0.0, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);

        // Act
        var results = engine.SensitivityAnalysis(option, 80, 120, 10);

        // Assert
        results.Should().HaveCount(10);
        results.First().Spot.Should().Be(80);
        results.Last().Spot.Should().BeApproximately(120, 0.01);
        
        // Prices should increase with spot for a call
        for (int i = 1; i < results.Count; i++)
        {
            results[i].Result.Price.Should().BeGreaterThan(results[i - 1].Result.Price);
        }
    }
}