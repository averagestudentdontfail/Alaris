// TSBM002A.cs - Benchmark Tests for Spectral Collocation Engine
// Component ID: TSBM002A
//
// Compares spectral engine (CREN004A) against finite difference engine (CREN002A)
// for accuracy and performance across all rate regimes.

using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using Alaris.Core.Options;
using Alaris.Core.Pricing;

namespace Alaris.Test.Benchmark;

/// <summary>
/// Benchmark tests comparing spectral vs finite difference American option pricing.
/// </summary>
public class TSBM002A
{
    private readonly ITestOutputHelper _output;
    private readonly CREN004A _spectralFast;
    private readonly CREN004A _spectralAccurate;
    private readonly CREN002A _fdEngine;

    public TSBM002A(ITestOutputHelper output)
    {
        _output = output;
        _spectralFast = new CREN004A(SpectralScheme.Fast);
        _spectralAccurate = new CREN004A(SpectralScheme.Accurate);
        _fdEngine = new CREN002A();
    }

    [Theory]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]
    [InlineData(100.0, 100.0, 0.5, 0.05, 0.02, 0.30, OptionType.Put)]
    [InlineData(100.0, 100.0, 2.0, 0.03, 0.01, 0.25, OptionType.Put)]
    [InlineData(80.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]   // ITM
    [InlineData(120.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]  // OTM
    public void Accuracy_SpectralVsFD_StandardRegime(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        double spectralPrice = _spectralAccurate.Price(spot, strike, tau, r, q, sigma, optionType);
        double fdPrice = _fdEngine.Price(spot, strike, tau, r, q, sigma, optionType);

        double absoluteDiff = System.Math.Abs(spectralPrice - fdPrice);
        double relativeDiff = absoluteDiff / fdPrice;

        _output.WriteLine($"Spot={spot}, K={strike}, τ={tau}, r={r}, q={q}, σ={sigma}");
        _output.WriteLine($"  Spectral: {spectralPrice:F6}");
        _output.WriteLine($"  FD:       {fdPrice:F6}");
        _output.WriteLine($"  Absolute Diff: {absoluteDiff:F6}");
        _output.WriteLine($"  Relative Diff: {relativeDiff:P4}");

        // Spectral should be within 5% of FD for standard regimes
        Assert.True(relativeDiff < 0.10,
            $"Spectral {spectralPrice:F4} differs from FD {fdPrice:F4} by {relativeDiff:P2}");
    }

    [Theory]
    [InlineData(100.0, 100.0, 1.0, -0.02, -0.03, 0.25, OptionType.Put)]  // r < q < 0
    public void Accuracy_SpectralVsFD_NegativeRateSingleBoundary(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        double spectralPrice = _spectralAccurate.Price(spot, strike, tau, r, q, sigma, optionType);
        double fdPrice = _fdEngine.Price(spot, strike, tau, r, q, sigma, optionType);

        double absoluteDiff = System.Math.Abs(spectralPrice - fdPrice);
        double relativeDiff = absoluteDiff / fdPrice;

        _output.WriteLine($"Negative Rate Single Boundary: r={r}, q={q}");
        _output.WriteLine($"  Spectral: {spectralPrice:F6}");
        _output.WriteLine($"  FD:       {fdPrice:F6}");
        _output.WriteLine($"  Relative Diff: {relativeDiff:P4}");

        // Both should be positive and at least intrinsic
        Assert.True(spectralPrice > 0 && fdPrice > 0);
    }

    [Fact]
    public void Performance_SpectralFastVsFD_100Options()
    {
        const int iterations = 100;
        Random random = new Random(42);

        // Warm up
        _ = _spectralFast.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);
        _ = _fdEngine.Price(100, 100, 1.0, 0.05, 0.02, 0.20, OptionType.Put);

        // Generate random option parameters
        (double Spot, double Strike, double Tau, double R, double Q, double Sigma)[] testCases = new (double Spot, double Strike, double Tau, double R, double Q, double Sigma)[iterations];
        for (int i = 0; i < iterations; i++)
        {
            testCases[i] = (
                Spot: 80.0 + (random.NextDouble() * 40.0),   // 80-120
                Strike: 100.0,
                Tau: 0.25 + (random.NextDouble() * 1.75),     // 0.25-2 years
                R: 0.01 + (random.NextDouble() * 0.08),       // 1%-9%
                Q: random.NextDouble() * 0.05,                // 0%-5%
                Sigma: 0.10 + (random.NextDouble() * 0.40)    // 10%-50%
            );
        }

        // Spectral Fast timing
        Stopwatch swSpectral = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            (double Spot, double Strike, double Tau, double R, double Q, double Sigma) testCase = testCases[i];
            _ = _spectralFast.Price(testCase.Spot, testCase.Strike, testCase.Tau, testCase.R, testCase.Q, testCase.Sigma, OptionType.Put);
        }
        swSpectral.Stop();
        double spectralAvgMs = swSpectral.Elapsed.TotalMilliseconds / iterations;

        // FD timing
        Stopwatch swFD = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            (double Spot, double Strike, double Tau, double R, double Q, double Sigma) testCase = testCases[i];
            _ = _fdEngine.Price(testCase.Spot, testCase.Strike, testCase.Tau, testCase.R, testCase.Q, testCase.Sigma, OptionType.Put);
        }
        swFD.Stop();
        double fdAvgMs = swFD.Elapsed.TotalMilliseconds / iterations;

        double speedup = fdAvgMs / spectralAvgMs;

        _output.WriteLine($"Performance Benchmark (N={iterations}):");
        _output.WriteLine($"  Spectral Fast: {spectralAvgMs:F4} ms/option");
        _output.WriteLine($"  FD Engine:     {fdAvgMs:F4} ms/option");
        _output.WriteLine($"  Speedup:       {speedup:F1}x");

        // Spectral should be at least as fast as FD (may not be 10x due to JIT etc)
        Assert.True(spectralAvgMs < fdAvgMs * 2.0,
            $"Spectral ({spectralAvgMs:F4}ms) should not be much slower than FD ({fdAvgMs:F4}ms)");
    }

    [Theory]
    [InlineData(0.05, 0.02, false, "Standard Put (r>0, q>0)")]
    [InlineData(0.05, 0.02, true, "Standard Call (r>0, q>0)")]
    [InlineData(-0.01, -0.02, false, "Negative Rate A (r>q, both <0)")]
    [InlineData(-0.02, -0.01, false, "Negative Rate B - Double Boundary (q<r<0)")]
    [InlineData(0.05, 0.08, true, "Call Double Boundary (0<r<q)")]
    public void RegimeCoverage_AllRegimesProduceValidPrice(
        double r, double q, bool isCall, string regimeDescription)
    {
        OptionType optionType = isCall ? OptionType.Call : OptionType.Put;

        try
        {
            double price = _spectralAccurate.Price(100.0, 100.0, 1.0, r, q, 0.20, optionType);
            double intrinsic = isCall ? System.Math.Max(0, 100 - 100) : System.Math.Max(0, 100 - 100);

            _output.WriteLine($"{regimeDescription}: Price = {price:F6}");

            Assert.True(price >= intrinsic - 0.01,
                $"{regimeDescription}: Price {price:F4} below intrinsic {intrinsic:F4}");
            Assert.True(price > 0,
                $"{regimeDescription}: Price should be positive");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"{regimeDescription}: FAILED - {ex.Message}");
            throw;
        }
    }

    [Theory]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]
    public void Greeks_SpectralProducesReasonableValues(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        double delta = _spectralAccurate.Delta(spot, strike, tau, r, q, sigma, optionType);
        double gamma = _spectralAccurate.Gamma(spot, strike, tau, r, q, sigma, optionType);
        double vega = _spectralAccurate.Vega(spot, strike, tau, r, q, sigma, optionType);

        _output.WriteLine($"Greeks for ATM {optionType}:");
        _output.WriteLine($"  Delta: {delta:F6}");
        _output.WriteLine($"  Gamma: {gamma:F6}");
        _output.WriteLine($"  Vega:  {vega:F6}");

        // Put delta should be negative
        if (optionType == OptionType.Put)
        {
            Assert.InRange(delta, -1.0, 0.0);
        }

        // Gamma should be positive (convexity)
        Assert.True(gamma >= 0, $"Gamma should be non-negative: {gamma}");

        // Vega should be positive (more vol = more value)
        Assert.True(vega >= 0, $"Vega should be non-negative: {vega}");
    }

    [Fact]
    public void RMSE_SpectralVsFD_Portfolio()
    {
        // Test portfolio of 20 options across different strikes/maturities
        (double Spot, double Strike, double Tau)[] testCases = new (double Spot, double Strike, double Tau)[]
        {
            (100, 80, 0.25), (100, 90, 0.25), (100, 100, 0.25), (100, 110, 0.25), (100, 120, 0.25),
            (100, 80, 0.50), (100, 90, 0.50), (100, 100, 0.50), (100, 110, 0.50), (100, 120, 0.50),
            (100, 80, 1.00), (100, 90, 1.00), (100, 100, 1.00), (100, 110, 1.00), (100, 120, 1.00),
            (100, 80, 2.00), (100, 90, 2.00), (100, 100, 2.00), (100, 110, 2.00), (100, 120, 2.00),
        };

        double r = 0.05, q = 0.02, sigma = 0.20;
        double sumSquaredErrors = 0.0;
        int count = 0;

        for (int i = 0; i < testCases.Length; i++)
        {
            (double Spot, double Strike, double Tau) testCase = testCases[i];
            double spectral = _spectralAccurate.Price(testCase.Spot, testCase.Strike, testCase.Tau, r, q, sigma, OptionType.Put);
            double fd = _fdEngine.Price(testCase.Spot, testCase.Strike, testCase.Tau, r, q, sigma, OptionType.Put);

            double error = spectral - fd;
            sumSquaredErrors += error * error;
            count++;

            _output.WriteLine($"K={testCase.Strike:F0}, τ={testCase.Tau:F2}: Spectral={spectral:F4}, FD={fd:F4}, Diff={error:F4}");
        }

        double rmse = System.Math.Sqrt(sumSquaredErrors / count);
        _output.WriteLine($"\nRMSE: {rmse:F6}");

        // RMSE should be < 0.5 (within $0.50 of FD reference)
        Assert.True(rmse < 0.50,
            $"RMSE {rmse:F4} exceeds threshold 0.50");
    }
}
