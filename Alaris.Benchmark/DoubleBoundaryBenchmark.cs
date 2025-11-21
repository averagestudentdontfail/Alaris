using BenchmarkDotNet.Attributes;
using Alaris.Double;

namespace Alaris.Benchmark;

/// <summary>
/// Benchmarks for double boundary American option pricing.
/// Measures performance of ArrayPool optimizations in Kim solver.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class DoubleBoundaryBenchmark
{
    private DoubleBoundaryEngine _engine = null!;

    // Healy (2021) benchmark parameters: q < r < 0 regime
    private const double Spot = 100.0;
    private const double Strike = 70.0;
    private const double RiskFreeRate = -0.02;  // r = -2%
    private const double DividendYield = -0.04; // q = -4% (q < r)
    private const double Volatility = 0.20;

    [Params(1.0, 5.0, 10.0)]
    public double TimeToExpiry { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _engine = new DoubleBoundaryEngine();
    }

    [Benchmark(Description = "Compute boundaries")]
    public (double Upper, double Lower) ComputeBoundaries()
    {
        return _engine.ComputeBoundaries(
            Spot, Strike, RiskFreeRate, DividendYield, Volatility, TimeToExpiry);
    }

    [Benchmark(Description = "Full pricing")]
    public double PriceOption()
    {
        return _engine.Price(
            Spot, Strike, RiskFreeRate, DividendYield, Volatility, TimeToExpiry);
    }

    [Benchmark(Description = "Boundary computation (10x)")]
    public double RepeatedBoundaryComputation()
    {
        double sum = 0;
        for (int i = 0; i < 10; i++)
        {
            var (upper, lower) = _engine.ComputeBoundaries(
                Spot, Strike, RiskFreeRate, DividendYield, Volatility, TimeToExpiry);
            sum += upper + lower;
        }
        return sum;
    }
}
