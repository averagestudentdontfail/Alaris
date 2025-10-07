using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Diagnostics;
using Alaris.Double;

namespace Alaris.Test.Benchmark;

/// <summary>
/// Performance benchmarks for DoubleBoundaryEngine.
/// Measures pricing speed, memory usage, and scaling characteristics.
/// </summary>
public class PerformanceBenchmarks
{
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Benchmark_SingleOptionPricing_MeasuresSpeed()
    {
        // Arrange
        var iterations = 1000;
        var spot = 100.0;
        var strike = 100.0;
        var rate = 0.05;
        var volatility = 0.20;
        var maturity = 1.0;

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

        // Warm-up
        for (int i = 0; i < 10; i++)
        {
            engine.Calculate(option);
        }

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            engine.Calculate(option);
        }
        sw.Stop();

        // Assert & Report
        var avgTime = sw.ElapsedMilliseconds / (double)iterations;
        _output.WriteLine($"Average pricing time: {avgTime:F3} ms");
        _output.WriteLine($"Total time for {iterations} iterations: {sw.ElapsedMilliseconds} ms");
        
        avgTime.Should().BeLessThan(10); // Should be fast
    }

    [Fact]
    public void Benchmark_SensitivityAnalysis_MeasuresScaling()
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var rate = 0.05;
        var volatility = 0.20;
        var maturity = 1.0;

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

        // Act
        var sw = Stopwatch.StartNew();
        var results = engine.SensitivityAnalysis(option, 80, 120, 100);
        sw.Stop();

        // Assert & Report
        _output.WriteLine($"Sensitivity analysis (100 points): {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average per point: {sw.ElapsedMilliseconds / 100.0:F2} ms");
        
        results.Should().HaveCount(100);
        sw.ElapsedMilliseconds.Should().BeLessThan(2000); // Should complete in reasonable time
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void Benchmark_VariableSteps_MeasuresLinearScaling(int steps)
    {
        // Arrange
        var spot = 100.0;
        var strike = 100.0;
        var rate = 0.05;
        var volatility = 0.20;
        var maturity = 1.0;

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

        // Act
        var sw = Stopwatch.StartNew();
        var results = engine.SensitivityAnalysis(option, 80, 120, steps);
        sw.Stop();

        // Report
        _output.WriteLine($"Steps: {steps}, Time: {sw.ElapsedMilliseconds} ms, " +
                         $"Avg: {sw.ElapsedMilliseconds / (double)steps:F2} ms/step");
        
        results.Should().HaveCount(steps);
    }

    [Fact]
    public void Benchmark_NegativeRates_ComparesWithPositiveRates()
    {
        // Compare performance between negative and positive rates
        var iterations = 100;
        var spot = 100.0;
        var strike = 100.0;
        var volatility = 0.20;
        var maturity = 1.0;

        var todaysDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(todaysDate);

        var exerciseDate = todaysDate.Add(new Period((int)(maturity * 365), TimeUnit.Days));
        var exercise = new AmericanExercise(todaysDate, exerciseDate);
        var payoff = new PlainVanillaPayoff(Option.Type.Call, strike);

        // Test with positive rate
        var positiveRate = 0.05;
        var option1 = new VanillaOption(payoff, exercise);
        var underlying1 = new SimpleQuote(spot);
        var riskFreeTS1 = new FlatForward(todaysDate, positiveRate, new Actual365Fixed());
        var dividendTS1 = new FlatForward(todaysDate, 0.0, new Actual365Fixed());
        var volTS1 = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());
        var process1 = new BlackScholesMertonProcess(
            new QuoteHandle(underlying1),
            new YieldTermStructureHandle(dividendTS1),
            new YieldTermStructureHandle(riskFreeTS1),
            new BlackVolTermStructureHandle(volTS1));
        var engine1 = new DoubleBoundaryEngine(process1);
        option1.setPricingEngine(engine1);

        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            engine1.Calculate(option1);
        }
        sw1.Stop();

        // Test with negative rate
        var negativeRate = -0.01;
        var option2 = new VanillaOption(payoff, exercise);
        var underlying2 = new SimpleQuote(spot);
        var riskFreeTS2 = new FlatForward(todaysDate, negativeRate, new Actual365Fixed());
        var dividendTS2 = new FlatForward(todaysDate, 0.0, new Actual365Fixed());
        var volTS2 = new BlackConstantVol(todaysDate, new TARGET(), volatility, new Actual365Fixed());
        var process2 = new BlackScholesMertonProcess(
            new QuoteHandle(underlying2),
            new YieldTermStructureHandle(dividendTS2),
            new YieldTermStructureHandle(riskFreeTS2),
            new BlackVolTermStructureHandle(volTS2));
        var engine2 = new DoubleBoundaryEngine(process2);
        option2.setPricingEngine(engine2);

        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            engine2.Calculate(option2);
        }
        sw2.Stop();

        // Report
        _output.WriteLine($"Positive rate (+5%): {sw1.ElapsedMilliseconds} ms");
        _output.WriteLine($"Negative rate (-1%): {sw2.ElapsedMilliseconds} ms");
        _output.WriteLine($"Performance ratio: {sw2.ElapsedMilliseconds / (double)sw1.ElapsedMilliseconds:F2}x");
        
        // Performance should be comparable
        var ratio = sw2.ElapsedMilliseconds / (double)sw1.ElapsedMilliseconds;
        ratio.Should().BeLessThan(2.0); // Negative rates shouldn't be more than 2x slower
    }
}