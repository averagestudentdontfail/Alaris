// CREN003AInvariantTests.cs - Mathematical Invariant Tests for Unified Pricing Engine
// Component ID: CREN003A Invariant Tests
//
// Validates that the pricing engine maintains mathematical invariants:
// 1. American >= European (early exercise premium >= 0)
// 2. American >= Intrinsic (no-arbitrage floor)
// 3. Deterministic behaviour (same inputs = same outputs)
// 4. Greeks within valid ranges

using System;
using Xunit;
using Xunit.Abstractions;
using Alaris.Core.Options;
using Alaris.Core.Pricing;

namespace Alaris.Test.Unit.Core.Pricing;

/// <summary>
/// Mathematical invariant tests for the unified American option pricing engine.
/// </summary>
public class CREN003AInvariantTests
{
    private readonly ITestOutputHelper _output;
    private readonly CREN003A _engine;

    public CREN003AInvariantTests(ITestOutputHelper output)
    {
        _output = output;
        _engine = new CREN003A();
    }

    // ========== Invariant 1: American >= European ==========

    [Theory]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]
    [InlineData(100.0, 100.0, 0.5, 0.05, 0.02, 0.30, OptionType.Put)]
    [InlineData(100.0, 100.0, 2.0, 0.03, 0.01, 0.25, OptionType.Put)]
    [InlineData(80.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]   // ITM
    [InlineData(120.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]  // OTM
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Call)]
    [InlineData(120.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Call)] // ITM
    [InlineData(80.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Call)]  // OTM
    public void Invariant_AmericanPriceGreaterThanOrEqualToEuropean(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        bool isCall = optionType == OptionType.Call;

        double american = _engine.Price(spot, strike, tau, r, q, sigma, optionType);
        double european = BlackScholesEuropean(spot, strike, tau, r, q, sigma, isCall);

        _output.WriteLine($"{optionType} S={spot}, K={strike}, τ={tau}");
        _output.WriteLine($"  American: {american:F6}");
        _output.WriteLine($"  European: {european:F6}");
        _output.WriteLine($"  EEP:      {american - european:F6}");

        Assert.True(american >= european - 1e-10,
            $"American {american:F6} < European {european:F6}");
    }

    // ========== Invariant 2: American >= Intrinsic ==========

    [Theory]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]
    [InlineData(80.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]   // Deep ITM
    [InlineData(50.0, 100.0, 0.5, 0.05, 0.02, 0.20, OptionType.Put)]   // Very deep ITM
    [InlineData(120.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Call)] // Deep ITM
    [InlineData(150.0, 100.0, 0.5, 0.05, 0.02, 0.20, OptionType.Call)] // Very deep ITM
    public void Invariant_AmericanPriceGreaterThanOrEqualToIntrinsic(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        double american = _engine.Price(spot, strike, tau, r, q, sigma, optionType);
        double intrinsic = optionType == OptionType.Call
            ? System.Math.Max(0, spot - strike)
            : System.Math.Max(0, strike - spot);

        _output.WriteLine($"{optionType} S={spot}, K={strike}, τ={tau}");
        _output.WriteLine($"  American:  {american:F6}");
        _output.WriteLine($"  Intrinsic: {intrinsic:F6}");

        Assert.True(american >= intrinsic - 1e-10,
            $"American {american:F6} < Intrinsic {intrinsic:F6}");
    }

    // ========== Invariant 3: Determinism ==========

    [Theory]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Call)]
    public void Invariant_DeterministicPricing(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        // Price the same option 10 times
        double[] prices = new double[10];
        for (int i = 0; i < 10; i++)
        {
            prices[i] = _engine.Price(spot, strike, tau, r, q, sigma, optionType);
        }

        // All prices should be identical
        double first = prices[0];
        for (int i = 1; i < 10; i++)
        {
            Assert.Equal(first, prices[i]);
        }

        _output.WriteLine($"Determinism verified: {first:F10} (10 calls identical)");
    }

    // ========== Invariant 4: Greeks Bounds ==========

    [Theory]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Call)]
    public void Invariant_DeltaInValidRange(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        double delta = _engine.Delta(spot, strike, tau, r, q, sigma, optionType);

        _output.WriteLine($"{optionType} Delta: {delta:F6}");

        if (optionType == OptionType.Call)
        {
            // Call delta in [0, 1]
            Assert.InRange(delta, -0.1, 1.1);
        }
        else
        {
            // Put delta in [-1, 0]
            Assert.InRange(delta, -1.1, 0.1);
        }
    }

    [Theory]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Call)]
    public void Invariant_GammaNonNegative(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        double gamma = _engine.Gamma(spot, strike, tau, r, q, sigma, optionType);

        _output.WriteLine($"{optionType} Gamma: {gamma:F6}");

        // Gamma should be non-negative (convexity of option payoff)
        Assert.True(gamma >= -0.001, $"Gamma should be >= 0, got {gamma}");
    }

    [Theory]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Put)]
    [InlineData(100.0, 100.0, 1.0, 0.05, 0.02, 0.20, OptionType.Call)]
    public void Invariant_VegaNonNegative(
        double spot, double strike, double tau, double r, double q, double sigma, OptionType optionType)
    {
        double vega = _engine.Vega(spot, strike, tau, r, q, sigma, optionType);

        _output.WriteLine($"{optionType} Vega: {vega:F6}");

        // Vega should be non-negative (more vol = more value)
        Assert.True(vega >= -0.001, $"Vega should be >= 0, got {vega}");
    }

    // ========== Invariant 5: All Rate Regimes Produce Valid Prices ==========

    [Theory]
    [InlineData(0.05, 0.02, OptionType.Put, "Standard r>0, q>0")]
    [InlineData(-0.01, -0.02, OptionType.Put, "Negative r<q<0")]
    [InlineData(-0.02, -0.03, OptionType.Put, "Negative q<r<0")]
    [InlineData(0.05, 0.08, OptionType.Call, "Dividend q>r>0")]
    public void Invariant_AllRegimesProduceValidPrices(
        double r, double q, OptionType optionType, string description)
    {
        double price = _engine.Price(100.0, 100.0, 1.0, r, q, 0.20, optionType);

        _output.WriteLine($"{description}: Price = {price:F6}");

        Assert.True(double.IsFinite(price), $"{description}: Price is not finite");
        Assert.True(price >= 0, $"{description}: Price is negative");
    }

    // ========== Helper Methods ==========

    private static double BlackScholesEuropean(double spot, double strike, double tau, double r, double q, double sigma, bool isCall)
    {
        if (tau <= 0)
        {
            return isCall ? System.Math.Max(0, spot - strike) : System.Math.Max(0, strike - spot);
        }

        double sqrtT = System.Math.Sqrt(tau);
        double d1 = (System.Math.Log(spot / strike) + ((r - q + (0.5 * sigma * sigma)) * tau)) / (sigma * sqrtT);
        double d2 = d1 - (sigma * sqrtT);

        double discountS = System.Math.Exp(-q * tau);
        double discountK = System.Math.Exp(-r * tau);

        if (isCall)
        {
            return (spot * discountS * NormalCDF(d1)) - (strike * discountK * NormalCDF(d2));
        }

        return (strike * discountK * NormalCDF(-d2)) - (spot * discountS * NormalCDF(-d1));
    }

    private static double NormalCDF(double x)
    {
        return 0.5 * (1.0 + Erf(x / System.Math.Sqrt(2.0)));
    }

    private static double Erf(double x)
    {
        double a1 = 0.254829592;
        double a2 = -0.284496736;
        double a3 = 1.421413741;
        double a4 = -1.453152027;
        double a5 = 1.061405429;
        double p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        x = System.Math.Abs(x);
        double t = 1.0 / (1.0 + (p * x));
        double y = 1.0 - ((((((a5 * t) + a4) * t) + a3) * t + a2) * t + a1) * t * System.Math.Exp(-x * x);
        return sign * y;
    }
}
