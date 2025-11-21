using BenchmarkDotNet.Attributes;
using Alaris.Strategy.Core;

namespace Alaris.Benchmark;

/// <summary>
/// Benchmarks for IV term structure analysis.
/// Measures performance of ArrayPool + index-based sorting optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class TermStructureBenchmark
{
    private List<TermStructurePoint> _points = null!;
    private TermStructure _termStructure = null!;

    [Params(5, 10, 20)]
    public int PointCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _termStructure = new TermStructure();
        _points = GenerateTermStructurePoints(PointCount);
    }

    [Benchmark(Description = "Term structure analysis")]
    public TermStructureAnalysis Analyze()
    {
        return _termStructure.Analyze(_points);
    }

    [Benchmark(Description = "Repeated analysis (100x)")]
    public double RepeatedAnalysis()
    {
        double totalSlope = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = _termStructure.Analyze(_points);
            totalSlope += result.Slope;
        }
        return totalSlope;
    }

    private static List<TermStructurePoint> GenerateTermStructurePoints(int count)
    {
        var points = new List<TermStructurePoint>(count);
        var random = new Random(42);

        // Generate slightly inverted term structure (typical pre-earnings pattern)
        double baseIV = 0.35;
        double slope = -0.0005; // Slight negative slope

        for (int i = 0; i < count; i++)
        {
            int dte = 7 + (i * 7); // 7, 14, 21, 28... days
            double iv = baseIV + (slope * dte) + (random.NextDouble() - 0.5) * 0.02;

            points.Add(new TermStructurePoint
            {
                DaysToExpiry = dte,
                ImpliedVolatility = Math.Max(0.1, iv),
                Strike = 100.0
            });
        }

        return points;
    }
}
