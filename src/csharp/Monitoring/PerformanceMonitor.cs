// src/csharp/Monitoring/PerformanceMonitor.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Statistics;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Interfaces;
using Alaris.IPC;

namespace Alaris.Monitoring
{
    public class PerformanceMonitor : IDisposable
    {
        private readonly ConcurrentDictionary<string, List<double>> _measurements = new ConcurrentDictionary<string, List<double>>();
        private readonly ConcurrentDictionary<string, Stopwatch> _activeStopwatches = new ConcurrentDictionary<string, Stopwatch>();
        private readonly Timer _reportingTimer;
        private bool _disposed = false;
        private string _symbol;
        private StrategyMode _strategyMode;
        private readonly List<decimal> _historicalSkew = new();
        private readonly List<decimal> _historicalVol = new();
        private readonly List<decimal> _historicalPnL = new();
        private readonly Dictionary<string, PositionMetrics> _positionMetrics = new();
        private DateTime _lastUpdate = DateTime.MinValue;
        private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(5);

        public PerformanceMonitor()
        {
            // Report metrics every 10 seconds
            _reportingTimer = new Timer(ReportMetrics, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            _symbol = "SPY";
            _strategyMode = StrategyMode.DeltaNeutral;
        }

        public void Initialize(string symbol, StrategyMode strategyMode)
        {
            _symbol = symbol;
            _strategyMode = strategyMode;
            _historicalSkew.Clear();
            _historicalVol.Clear();
            _historicalPnL.Clear();
            _positionMetrics.Clear();
            _lastUpdate = DateTime.MinValue;
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

        public void ProcessOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status != OrderStatus.Filled) return;

            var symbol = orderEvent.Symbol.Value;
            if (!_positionMetrics.ContainsKey(symbol))
            {
                _positionMetrics[symbol] = new PositionMetrics();
            }

            var metrics = _positionMetrics[symbol];
            metrics.ProcessOrder(orderEvent);
        }

        public void UpdatePortfolioMetrics(SecurityPortfolioManager portfolio)
        {
            if (DateTime.Now - _lastUpdate < _updateInterval) return;

            foreach (var holding in portfolio.Securities.Values)
            {
                if (!_positionMetrics.ContainsKey(holding.Symbol.Value))
                {
                    _positionMetrics[holding.Symbol.Value] = new PositionMetrics();
                }

                var metrics = _positionMetrics[holding.Symbol.Value];
                metrics.UpdateMetrics(holding.Holdings);
            }

            _historicalPnL.Add(portfolio.TotalProfit);
            _lastUpdate = DateTime.Now;
        }

        public decimal GetHistoricalSkew()
        {
            return _historicalSkew.Any() ? _historicalSkew.Average() : 0;
        }

        public void UpdateSkew(decimal skew)
        {
            _historicalSkew.Add(skew);
            if (_historicalSkew.Count > 100)
            {
                _historicalSkew.RemoveAt(0);
            }
        }

        public void UpdateVolatility(decimal volatility)
        {
            _historicalVol.Add(volatility);
            if (_historicalVol.Count > 100)
            {
                _historicalVol.RemoveAt(0);
            }
        }

        public void GenerateReport()
        {
            Console.WriteLine("\n=== Performance Report ===");
            Console.WriteLine($"Symbol: {_symbol}");
            Console.WriteLine($"Strategy Mode: {_strategyMode}");
            Console.WriteLine($"Total Positions: {_positionMetrics.Count}");
            
            if (_historicalPnL.Any())
            {
                Console.WriteLine($"Total P&L: {_historicalPnL.Last():C}");
                Console.WriteLine($"Average P&L: {_historicalPnL.Average():C}");
                Console.WriteLine($"Max Drawdown: {CalculateMaxDrawdown():P2}");
            }

            if (_historicalVol.Any())
            {
                Console.WriteLine($"Average Volatility: {_historicalVol.Average():P2}");
                Console.WriteLine($"Volatility Range: {_historicalVol.Min():P2} - {_historicalVol.Max():P2}");
            }

            if (_historicalSkew.Any())
            {
                Console.WriteLine($"Average Skew: {_historicalSkew.Average():P2}");
                Console.WriteLine($"Skew Range: {_historicalSkew.Min():P2} - {_historicalSkew.Max():P2}");
            }

            Console.WriteLine("\nPosition Metrics:");
            foreach (var kvp in _positionMetrics)
            {
                var metrics = kvp.Value;
                Console.WriteLine($"\n{kvp.Key}:");
                Console.WriteLine($"  Trades: {metrics.TotalTrades}");
                Console.WriteLine($"  Win Rate: {metrics.WinRate:P2}");
                Console.WriteLine($"  Average P&L: {metrics.AveragePnL:C}");
                Console.WriteLine($"  Max Drawdown: {metrics.MaxDrawdown:P2}");
            }

            Console.WriteLine("\n=== End Report ===\n");
        }

        private decimal CalculateMaxDrawdown()
        {
            if (!_historicalPnL.Any()) return 0;

            var peak = _historicalPnL[0];
            var maxDrawdown = 0m;

            foreach (var pnl in _historicalPnL)
            {
                if (pnl > peak)
                {
                    peak = pnl;
                }
                else
                {
                    var drawdown = (peak - pnl) / Math.Abs(peak);
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }

            return maxDrawdown;
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

    public class PositionMetrics
    {
        public int TotalTrades { get; private set; }
        public int WinningTrades { get; private set; }
        public decimal TotalPnL { get; private set; }
        public decimal MaxDrawdown { get; private set; }
        public decimal PeakValue { get; private set; }
        public decimal CurrentValue { get; private set; }

        public decimal WinRate => TotalTrades > 0 ? (decimal)WinningTrades / TotalTrades : 0;
        public decimal AveragePnL => TotalTrades > 0 ? TotalPnL / TotalTrades : 0;

        public void ProcessOrder(OrderEvent orderEvent)
        {
            if (orderEvent.Status != OrderStatus.Filled) return;

            TotalTrades++;
            var pnl = orderEvent.FillQuantity * orderEvent.FillPrice;
            TotalPnL += pnl;

            if (pnl > 0)
            {
                WinningTrades++;
            }

            UpdateDrawdown(pnl);
        }

        public void UpdateMetrics(SecurityHolding holding)
        {
            CurrentValue = holding.HoldingsValue;
            UpdateDrawdown(0); // Update drawdown based on current value
        }

        private void UpdateDrawdown(decimal pnl)
        {
            var currentValue = CurrentValue + pnl;
            if (currentValue > PeakValue)
            {
                PeakValue = currentValue;
            }
            else
            {
                var drawdown = (PeakValue - currentValue) / Math.Abs(PeakValue);
                MaxDrawdown = Math.Max(MaxDrawdown, drawdown);
            }
        }
    }
}