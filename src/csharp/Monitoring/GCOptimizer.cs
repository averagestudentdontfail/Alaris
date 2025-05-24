// src/csharp/Monitoring/GCOptimizer.cs
using System;
using System.Runtime;

namespace Alaris.Monitoring
{
    public class GCOptimizer : IDisposable
    {
        private bool _disposed = false;

        public GCOptimizer()
        {
            // Configure GC for server scenarios
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            
            // Use server GC if available
            if (GCSettings.IsServerGC)
            {
                Console.WriteLine("Server GC is enabled - optimal for throughput");
            }
            else
            {
                Console.WriteLine("Workstation GC is enabled - consider using server GC for better performance");
            }

            // Set large object heap compaction mode
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        }

        public void OptimizeForLowLatency()
        {
            // Switch to sustained low latency mode for critical periods
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        }

        public void OptimizeForThroughput()
        {
            // Switch to batch mode for non-critical periods
            GCSettings.LatencyMode = GCLatencyMode.Batch;
        }

        public void ForceCompaction()
        {
            // Force garbage collection and compaction
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public GCMemoryInfo GetMemoryInfo()
        {
            return GC.GetGCMemoryInfo();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Reset to default latency mode
                GCSettings.LatencyMode = GCLatencyMode.Interactive;
                _disposed = true;
            }
        }
    }
}