using BenchmarkDotNet.Attributes;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Core;

namespace Alaris.Benchmark;

/// <summary>
/// Benchmarks for Yang-Zhang volatility estimator.
/// Measures performance of ArrayPool + Span optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class YangZhangBenchmark
{
    private List<PriceBar> _priceBars = null!;
    private YangZhangEstimator _estimator = null!;

    [Params(30, 60, 90)]
    public int Window { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _estimator = new YangZhangEstimator();
        _priceBars = GenerateSyntheticPriceBars(252); // 1 year of data
    }

    [Benchmark(Description = "Single YZ calculation")]
    public double SingleCalculation()
    {
        return _estimator.Calculate(_priceBars, Window, annualized: true);
    }

    [Benchmark(Description = "Rolling YZ series")]
    public IReadOnlyList<(DateTime, double)> RollingCalculation()
    {
        return _estimator.CalculateRolling(_priceBars, Window, annualized: true);
    }

    private static List<PriceBar> GenerateSyntheticPriceBars(int count)
    {
        var bars = new List<PriceBar>(count);
        var random = new Random(42); // Fixed seed for reproducibility
        double price = 100.0;

        for (int i = 0; i < count; i++)
        {
            double dailyReturn = (random.NextDouble() - 0.5) * 0.04; // +/- 2% daily
            double open = price;
            double close = price * (1 + dailyReturn);
            double high = Math.Max(open, close) * (1 + random.NextDouble() * 0.01);
            double low = Math.Min(open, close) * (1 - random.NextDouble() * 0.01);

            bars.Add(new PriceBar
            {
                Date = DateTime.Today.AddDays(-count + i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 1_000_000 + random.Next(500_000)
            });

            price = close;
        }

        return bars;
    }
}
