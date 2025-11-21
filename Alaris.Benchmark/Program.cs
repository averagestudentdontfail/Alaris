using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace Alaris.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        // Use InProcess for environments without external process support
        var config = DefaultConfig.Instance
            .AddJob(Job.ShortRun
                .WithToolchain(InProcessNoEmitToolchain.Instance));

        var switcher = new BenchmarkSwitcher(new[]
        {
            typeof(YangZhangBenchmark),
            typeof(TermStructureBenchmark),
            typeof(DoubleBoundaryBenchmark)
        });

        switcher.Run(args, config);
    }
}
