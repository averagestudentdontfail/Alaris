// src/csharp/Algorithm/DataDownload.cs
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Securities;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Alaris.Algorithm
{
    /// <summary>
    /// Simple algorithm specifically designed for data downloading.
    /// This algorithm initializes the universe and lets Lean handle data downloading
    /// without any shared memory communication or trading logic.
    /// </summary>
    public class DataDownload : QCAlgorithm
    {
        private List<string> _symbols = new List<string>();
        private int _dataPointsReceived = 0;
        private Dictionary<string, DateTime> _lastDataTime = new Dictionary<string, DateTime>();

        public override void Initialize()
        {
            try
            {
                Log("=== Data Download Algorithm Initializing ===");
                
                // Set up time range for data download
                SetStartDate(2020, 1, 1);
                SetEndDate(DateTime.Now.AddDays(-1)); // Yesterday to ensure data availability
                SetCash(100000); // Not used for data download, but required

                // Configure universe symbols from environment or use defaults
                var symbolsConfig = Environment.GetEnvironmentVariable("ALARIS_SYMBOLS");
                if (!string.IsNullOrEmpty(symbolsConfig))
                {
                    _symbols = symbolsConfig.Split(',').Select(s => s.Trim()).ToList();
                }
                else
                {
                    // Default symbols - same as in main algorithm
                    _symbols = new List<string>
                    {
                        "SPY", "QQQ", "IWM", "EFA", "VTI",
                        "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA",
                        "JPM", "BAC", "WFC", "GS", "MS",
                        "XOM", "CVX", "COP", "EOG", "SLB",
                        "JNJ", "PFE", "UNH", "ABBV", "MRK"
                    };
                }

                Log($"Configuring data download for {_symbols.Count} symbols:");
                foreach (var symbol in _symbols)
                {
                    Log($"  - {symbol}");
                }

                // Add securities to universe
                foreach (var symbol in _symbols)
                {
                    try
                    {
                        // Add equity with daily resolution for data download
                        var equity = AddEquity(symbol, Resolution.Daily, Market.USA);
                        
                        // Also add minute resolution for more granular data
                        var equityMinute = AddEquity(symbol, Resolution.Minute, Market.USA);
                        
                        // Add options if available
                        try
                        {
                            var option = AddOption(symbol, Resolution.Daily);
                            option.SetFilter(universe => universe.IncludeWeeklys()
                                                               .Strikes(-10, +10)
                                                               .Expiration(TimeSpan.FromDays(0), TimeSpan.FromDays(90)));
                        }
                        catch (Exception ex)
                        {
                            Log($"Note: Could not add options for {symbol}: {ex.Message}");
                        }

                        _lastDataTime[symbol] = DateTime.MinValue;
                        Log($"✓ Added {symbol} to universe");
                    }
                    catch (Exception ex)
                    {
                        Error($"Failed to add {symbol} to universe: {ex.Message}");
                    }
                }

                // Schedule periodic progress updates
                Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromHours(1)), () =>
                {
                    LogDataProgress();
                });

                Log($"✓ Data download algorithm initialized successfully");
                Log($"✓ Universe configured with {Securities.Count} securities");
                Log($"✓ Data download will begin automatically");
                Log("=== Initialization Complete ===");
            }
            catch (Exception ex)
            {
                Error($"Failed to initialize data download algorithm: {ex.Message}");
                Error($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to fail the algorithm
            }
        }

        public override void OnData(Slice data)
        {
            try
            {
                // Process and count data points
                foreach (var kvp in data.Bars)
                {
                    var symbol = kvp.Key.Value;
                    var bar = kvp.Value;
                    
                    _dataPointsReceived++;
                    _lastDataTime[symbol] = bar.Time;
                    
                    // Log progress every 1000 data points
                    if (_dataPointsReceived % 1000 == 0)
                    {
                        Log($"Data progress: {_dataPointsReceived} data points received");
                    }
                }

                // Process options data if available
                if (data.OptionChains != null)
                {
                    foreach (var chain in data.OptionChains)
                    {
                        var underlying = chain.Key.Underlying.Value;
                        var optionCount = chain.Value.Count();
                        
                        if (optionCount > 0)
                        {
                            Debug($"Options data: {underlying} has {optionCount} contracts");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"Error processing data: {ex.Message}");
            }
        }

        private void LogDataProgress()
        {
            try
            {
                Log($"=== Data Download Progress ===");
                Log($"Total data points received: {_dataPointsReceived}");
                Log($"Securities in universe: {Securities.Count}");
                
                var symbolsWithData = _lastDataTime.Where(kvp => kvp.Value > DateTime.MinValue).Count();
                Log($"Symbols with data: {symbolsWithData}/{_symbols.Count}");
                
                if (symbolsWithData > 0)
                {
                    Log("Latest data timestamps:");
                    foreach (var kvp in _lastDataTime.Where(x => x.Value > DateTime.MinValue).Take(5))
                    {
                        Log($"  {kvp.Key}: {kvp.Value:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    if (_lastDataTime.Count > 5)
                    {
                        Log($"  ... and {_lastDataTime.Count - 5} more symbols");
                    }
                }
                
                Log("===============================");
            }
            catch (Exception ex)
            {
                Error($"Error logging progress: {ex.Message}");
            }
        }

        public override void OnEndOfAlgorithm()
        {
            try
            {
                Log("=== Data Download Complete ===");
                Log($"Total data points processed: {_dataPointsReceived}");
                Log($"Securities processed: {Securities.Count}");
                
                var symbolsWithData = _lastDataTime.Where(kvp => kvp.Value > DateTime.MinValue).Count();
                Log($"Symbols with data: {symbolsWithData}/{_symbols.Count}");
                
                if (symbolsWithData > 0)
                {
                    Log("Final data summary:");
                    foreach (var kvp in _lastDataTime.Where(x => x.Value > DateTime.MinValue))
                    {
                        Log($"  ✓ {kvp.Key}: Latest data {kvp.Value:yyyy-MM-dd}");
                    }
                }
                
                var symbolsWithoutData = _symbols.Where(s => !_lastDataTime.ContainsKey(s) || _lastDataTime[s] == DateTime.MinValue).ToList();
                if (symbolsWithoutData.Any())
                {
                    Log("Symbols without data:");
                    foreach (var symbol in symbolsWithoutData)
                    {
                        Log($"  ✗ {symbol}: No data received");
                    }
                }
                
                Log("===============================");
                
                // Provide guidance based on results
                if (symbolsWithData == 0)
                {
                    Log("⚠ No data was downloaded. This might indicate:");
                    Log("  - API credentials issue");
                    Log("  - Data subscription limitations");
                    Log("  - Network connectivity problems");
                    Log("  - QuantConnect account data quotas exceeded");
                }
                else if (symbolsWithData < _symbols.Count)
                {
                    Log($"⚠ Partial data download: {symbolsWithData}/{_symbols.Count} symbols");
                    Log("  - Some symbols may not be available");
                    Log("  - Check data subscription for missing symbols");
                }
                else
                {
                    Log($"✓ Complete data download: {symbolsWithData}/{_symbols.Count} symbols");
                    Log("✓ Data is ready for backtesting");
                }
            }
            catch (Exception ex)
            {
                Error($"Error in OnEndOfAlgorithm: {ex.Message}");
            }
        }
    }
}