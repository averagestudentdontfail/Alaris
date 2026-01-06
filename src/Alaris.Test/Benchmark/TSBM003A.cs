// TSBM003A.cs - Spectral vs Finite Difference Performance Benchmarks
// Component ID: TSBM003A
//
// Performance comparison between spectral collocation (CREN004A) and 
// finite difference (CREN002A) engines across different scenarios.
// Based on Andersen-Lake-Offengenden (2016) benchmark methodology.

using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using Alaris.Core.Options;
using Alaris.Core.Pricing;

namespace Alaris.Test.Benchmark;

/// <summary>
/// Performance benchmarks comparing spectral collocation vs finite difference.
/// </summary>
public class TSBM003A
{
    private readonly ITestOutputHelper _output;
    private readonly CREN004A _spectralFast;
    private readonly CREN004A _spectralAccurate;
    private readonly CREN004A _spectralHighPrecision;
    private readonly CREN002A _fdEngine;

    public TSBM003A(ITestOutputHelper output)
    {
        _output = output;
        _spectralFast = new CREN004A(SpectralScheme.Fast);
        _spectralAccurate = new CREN004A(SpectralScheme.Accurate);
        _spectralHighPrecision = new CREN004A(SpectralScheme.HighPrecision);
        _fdEngine = new CREN002A();
    }

    [Fact]
    public void SingleOption_SpeedComparison()
    {
        const int warmupIterations = 10;
        const int testIterations = 100;

        // Warm up JIT
        for (int i = 0; i < warmupIterations; i++)
        {
            _ = _spectralFast.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
            _ = _spectralAccurate.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
            _ = _fdEngine.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        }

        // Spectral Fast timing
        Stopwatch swFast = Stopwatch.StartNew();
        for (int i = 0; i < testIterations; i++)
        {
            _ = _spectralFast.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        }
        swFast.Stop();
        double fastAvgUs = swFast.Elapsed.TotalMicroseconds / testIterations;

        // Spectral Accurate timing
        Stopwatch swAccurate = Stopwatch.StartNew();
        for (int i = 0; i < testIterations; i++)
        {
            _ = _spectralAccurate.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        }
        swAccurate.Stop();
        double accurateAvgUs = swAccurate.Elapsed.TotalMicroseconds / testIterations;

        // FD timing
        Stopwatch swFD = Stopwatch.StartNew();
        for (int i = 0; i < testIterations; i++)
        {
            _ = _fdEngine.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        }
        swFD.Stop();
        double fdAvgUs = swFD.Elapsed.TotalMicroseconds / testIterations;

        _output.WriteLine("== Single Option Timing (µs) ==");
        _output.WriteLine($"Spectral Fast:     {fastAvgUs:F1}");
        _output.WriteLine($"Spectral Accurate: {accurateAvgUs:F1}");
        _output.WriteLine($"Finite Difference: {fdAvgUs:F1}");
        _output.WriteLine($"FD/Spectral Ratio: {fdAvgUs / accurateAvgUs:F1}x");

        // Both should be reasonably fast
        Assert.True(accurateAvgUs < 10000, $"Spectral should be < 10ms, got {accurateAvgUs}µs");
    }

    [Fact]
    public void Portfolio_ThroughputComparison()
    {
        const int portfolioSize = 500;
        Random random = new Random(42);

        // Generate portfolio
        (double Spot, double Strike, double Tau, double R, double Q, double Sigma)[] options =
            new (double Spot, double Strike, double Tau, double R, double Q, double Sigma)[portfolioSize];
        for (int i = 0; i < portfolioSize; i++)
        {
            options[i] = (
                Spot: 100.0,
                Strike: 80.0 + (random.NextDouble() * 40.0),
                Tau: 0.1 + (random.NextDouble() * 2.0),
                R: 0.01 + (random.NextDouble() * 0.08),
                Q: random.NextDouble() * 0.05,
                Sigma: 0.10 + (random.NextDouble() * 0.40));
        }

        // Warm up
        _ = _spectralAccurate.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        _ = _fdEngine.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);

        // Spectral
        Stopwatch swSpectral = Stopwatch.StartNew();
        for (int i = 0; i < options.Length; i++)
        {
            (double Spot, double Strike, double Tau, double R, double Q, double Sigma) opt = options[i];
            _ = _spectralAccurate.Price(opt.Spot, opt.Strike, opt.Tau, opt.R, opt.Q, opt.Sigma, OptionType.Put);
        }
        swSpectral.Stop();

        // FD
        Stopwatch swFD = Stopwatch.StartNew();
        for (int i = 0; i < options.Length; i++)
        {
            (double Spot, double Strike, double Tau, double R, double Q, double Sigma) opt = options[i];
            _ = _fdEngine.Price(opt.Spot, opt.Strike, opt.Tau, opt.R, opt.Q, opt.Sigma, OptionType.Put);
        }
        swFD.Stop();

        double spectralThroughput = portfolioSize / swSpectral.Elapsed.TotalSeconds;
        double fdThroughput = portfolioSize / swFD.Elapsed.TotalSeconds;

        _output.WriteLine($"== Portfolio Throughput (N={portfolioSize}) ==");
        _output.WriteLine($"Spectral: {spectralThroughput:F0} options/sec ({swSpectral.ElapsedMilliseconds}ms total)");
        _output.WriteLine($"FD:       {fdThroughput:F0} options/sec ({swFD.ElapsedMilliseconds}ms total)");
        _output.WriteLine($"Ratio:    {spectralThroughput / fdThroughput:F2}x");

        // Should process reasonable throughput
        Assert.True(spectralThroughput > 50, $"Should process > 50 options/sec, got {spectralThroughput:F0}");
    }

    [Fact]
    public void AccuracySpeedTradeoff_SchemeComparison()
    {
        // Reference: use high precision spectral as benchmark
        double reference = _spectralHighPrecision.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);

        // Measure each scheme
        (string Name, CREN004A Engine)[] schemes = new (string Name, CREN004A Engine)[]
        {
            ("Fast", _spectralFast),
            ("Accurate", _spectralAccurate),
            ("HighPrecision", _spectralHighPrecision)
        };

        _output.WriteLine("== Accuracy vs Speed Tradeoff ==");
        _output.WriteLine($"Reference (HighPrecision): {reference:F8}");
        _output.WriteLine("");

        for (int i = 0; i < schemes.Length; i++)
        {
            (string Name, CREN004A Engine) scheme = schemes[i];
            string name = scheme.Name;
            CREN004A engine = scheme.Engine;
            const int iterations = 50;

            // Time it
            Stopwatch sw = Stopwatch.StartNew();
            double price = 0;
            for (int i = 0; i < iterations; i++)
            {
                price = engine.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
            }
            sw.Stop();

            double error = System.Math.Abs(price - reference);
            double avgUs = sw.Elapsed.TotalMicroseconds / iterations;

            _output.WriteLine($"{name,-15}: Price={price:F6}, Error={error:E2}, Time={avgUs:F0}µs");
        }
    }

    [Fact]
    public void Greeks_TimingComparison()
    {
        const int iterations = 50;

        // Warm up
        _ = _spectralAccurate.Delta(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        _ = _fdEngine.Delta(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);

        // Spectral Greeks (computed via central differencing, so 2-3x price time)
        Stopwatch swSpectral = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = _spectralAccurate.Delta(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
            _ = _spectralAccurate.Gamma(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
            _ = _spectralAccurate.Vega(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        }
        swSpectral.Stop();

        // FD Greeks
        Stopwatch swFD = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = _fdEngine.Delta(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
            _ = _fdEngine.Gamma(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
            _ = _fdEngine.Vega(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        }
        swFD.Stop();

        _output.WriteLine("== Greeks Computation (Δ + Γ + ν) ==");
        _output.WriteLine($"Spectral: {swSpectral.ElapsedMilliseconds}ms for {iterations} iterations");
        _output.WriteLine($"FD:       {swFD.ElapsedMilliseconds}ms for {iterations} iterations");
    }

    [Theory]
    [InlineData(0.1)]   // 1 month
    [InlineData(0.25)]  // 3 months
    [InlineData(0.5)]   // 6 months
    [InlineData(1.0)]   // 1 year
    [InlineData(2.0)]   // 2 years
    public void MaturityScaling_Performance(double tau)
    {
        const int iterations = 20;

        Stopwatch swSpectral = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = _spectralAccurate.Price(100, 100, tau, 0.05, 0.02, 0.20, OptionType.Put);
        }
        swSpectral.Stop();

        Stopwatch swFD = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = _fdEngine.Price(100, 100, tau, 0.05, 0.02, 0.20, OptionType.Put);
        }
        swFD.Stop();

        double spectralUs = swSpectral.Elapsed.TotalMicroseconds / iterations;
        double fdUs = swFD.Elapsed.TotalMicroseconds / iterations;

        _output.WriteLine($"τ={tau:F2}: Spectral={spectralUs:F0}µs, FD={fdUs:F0}µs");
    }

    [Theory]
    [InlineData(0.05, 0.02, false, "Standard Put")]
    [InlineData(0.05, 0.02, true, "Standard Call")]
    [InlineData(-0.01, -0.02, false, "Negative r<q")]
    [InlineData(-0.02, -0.01, false, "Double Boundary")]
    public void RegimeCoverage_BothEnginesWork(double r, double q, bool isCall, string regime)
    {
        OptionType optionType = isCall ? OptionType.Call : OptionType.Put;

        double spectralPrice = _spectralAccurate.Price(100, 100, 1.0, r, q, 0.20, optionType);
        double fdPrice = _fdEngine.Price(100, 100, 1.0, r, q, 0.20, optionType);

        _output.WriteLine($"{regime}: Spectral={spectralPrice:F4}, FD={fdPrice:F4}");

        Assert.True(double.IsFinite(spectralPrice), $"Spectral price must be finite in {regime}");
        Assert.True(double.IsFinite(fdPrice), $"FD price must be finite in {regime}");
    }
}
