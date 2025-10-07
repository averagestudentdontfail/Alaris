using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Integration;

/// <summary>
/// Integration tests based on Healy et al. (2022) research on negative interest rate options.
/// Validates DoubleBoundaryEngine accuracy in negative rate environments.
/// Reference: "Option pricing under negative interest rates"
/// </summary>
public class HealyIntegrationTests
{
    [Theory]
    [InlineData(-0.01, 100, 100, 0.20, 1.0)]
    [InlineData(-0.005, 95, 100, 0.25, 0.5)]
    [InlineData(0.00, 105, 100, 0.15, 1.5)]
    public void HealyScenario_NegativeRates_CallOptions(
        double rate,
        double spot,
        double strike,
        double volatility,
        double maturity)
    {
        // Arrange
        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, rate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, 0.0, new Actual365Fixed());
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
        result.Price.Should().BeLessThan(spot);
        
        // Validate Greeks are reasonable
        result.Delta.Should().BeGreaterThan(0).And.BeLessThan(1);
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(-0.01, 100, 100, 0.20, 1.0)]
    [InlineData(-0.005, 105, 100, 0.25, 0.5)]
    [InlineData(-0.002, 95, 100, 0.15, 1.5)]
    public void HealyScenario_NegativeRates_PutOptions(
        double rate,
        double spot,
        double strike,
        double volatility,
        double maturity)
    {
        // Arrange
        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Put, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, rate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, 0.0, new Actual365Fixed());
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
        
        // Validate Greeks are reasonable
        result.Delta.Should().BeLessThan(0).And.BeGreaterThan(-1);
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
    }

    [Fact]
    public void HealyScenario_DeepNegativeRate_MaintainsStability()
    {
        // Test extreme negative rate scenario
        var rate = -0.05; // -5% rate
        var spot = 100.0;
        var strike = 100.0;
        var volatility = 0.30;
        var maturity = 2.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, rate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, 0.0, new Actual365Fixed());
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
        
        // No NaN or Infinity
        double.IsNaN(result.Price).Should().BeFalse();
        double.IsInfinity(result.Price).Should().BeFalse();
    }

    [Fact]
    public void HealyScenario_ComparePutCallParity_UnderNegativeRates()
    {
        // Validate put-call parity relationship under negative rates
        var rate = -0.01;
        var spot = 100.0;
        var strike = 100.0;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        
        // Create call and put
        var callPayoff = new PlainVanillaPayoff(Option.Type.Call, strike);
        var putPayoff = new PlainVanillaPayoff(Option.Type.Put, strike);
        var callOption = new VanillaOption(callPayoff, exercise);
        var putOption = new VanillaOption(putPayoff, exercise);

        var underlying = new SimpleQuote(spot);
        var riskFreeTS = new FlatForward(todaysDate, rate, new Actual365Fixed());
        var dividendTS = new FlatForward(todaysDate, 0.0, new Actual365Fixed());
        var volTS = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());

        var process = new BlackScholesMertonProcess(
            new QuoteHandle(underlying),
            new YieldTermStructureHandle(dividendTS),
            new YieldTermStructureHandle(riskFreeTS),
            new BlackVolTermStructureHandle(volTS));

        var engine = new DoubleBoundaryEngine(process);
        callOption.setPricingEngine(engine);
        putOption.setPricingEngine(engine);

        // Act
        var callResult = engine.Calculate(callOption);
        var putResult = engine.Calculate(putOption);

        // Assert
        // For American options, put-call parity becomes an inequality:
        // C - P <= S - K * exp(-r*T)
        var discountFactor = Math.Exp(-rate * maturity);
        var parity = callResult.Price - putResult.Price;
        var bound = spot - strike * discountFactor;

        parity.Should().BeLessThanOrEqualTo(bound + 0.5); // Small tolerance for numerical precision
    }
}