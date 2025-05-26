// src/csharp/Monitoring/PerformanceMonitor.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alaris.Monitoring
{
    public class PerformanceMonitor : IDisposable
    {
        private readonly ConcurrentDictionary<string, List<double>> _measurements = new ConcurrentDictionary<string, List<double>>();
        private readonly ConcurrentDictionary<string, Stopwatch> _activeStopwatches = new ConcurrentDictionary<string, Stopwatch>();
        private readonly Timer _reportingTimer;
        private bool _disposed = false;

        public PerformanceMonitor()
        {
            // Report metrics every 10 seconds
            _reportingTimer = new Timer(ReportMetrics, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public void StartMeasurement(string operationName)
        {
            var stopwatch = Stopwatch.StartNew();
            _activeStopwatches.TryAdd(operationName, stopwatch);
        }

        public void EndMeasurement(string operationName)
        {
            if (_activeStopwatches.TryRemove(operationName, out Stopwatch? stopwatch))
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                
                _measurements.AddOrUpdate(operationName, 
                    new List<double> { elapsedMs },
                    (key, list) => 
                    {
                        lock (list)
                        {
                            list.Add(elapsedMs);
                            // Keep only last 1000 measurements
                            if (list.Count > 1000)
                            {
                                list.RemoveAt(0);
                            }
                        }
                        return list;
                    });
            }
        }

        public PerformanceStats GetStats(string operationName)
        {
            if (!_measurements.TryGetValue(operationName, out List<double>? measurements) || measurements == null)
            {
                return new PerformanceStats();
            }

            lock (measurements)
            {
                if (measurements.Count == 0)
                {
                    return new PerformanceStats();
                }

                var sorted = measurements.OrderBy(x => x).ToList();
                
                return new PerformanceStats
                {
                    Count = measurements.Count,
                    Average = measurements.Average(),
                    Min = measurements.Min(),
                    Max = measurements.Max(),
                    Median = sorted[sorted.Count / 2],
                    P95 = sorted[(int)(sorted.Count * 0.95)],
                    P99 = sorted[(int)(sorted.Count * 0.99)]
                };
            }
        }

        public Dictionary<string, PerformanceStats> GetAllStats()
        {
            var result = new Dictionary<string, PerformanceStats>();
            
            foreach (var kvp in _measurements)
            {
                result[kvp.Key] = GetStats(kvp.Key);
            }
            
            return result;
        }

        private void ReportMetrics(object? state)
        {
            if (_disposed) return;

            try
            {
                var stats = GetAllStats();
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Performance Metrics:");
                
                foreach (var kvp in stats)
                {
                    var stat = kvp.Value;
                    if (stat.Count > 0)
                    {
                        Console.WriteLine($"  {kvp.Key}: Avg={stat.Average:F2}ms, P95={stat.P95:F2}ms, P99={stat.P99:F2}ms, Count={stat.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reporting metrics: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _reportingTimer?.Dispose();
                _disposed = true;
            }
        }
    }

    public struct PerformanceStats
    {
        public int Count;
        public double Average;
        public double Min;
        public double Max;
        public double Median;
        public double P95;
        public double P99;
    }
}