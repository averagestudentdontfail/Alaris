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
    /// Simple data download algorithm - no shared memory, no complex logic
    /// Just downloads data for the configured symbols
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
                Log("=== Simple Data Download Algorithm Starting ===");
                
                // Set up time range for data download (last 4 years)
                SetStartDate(2021, 1, 1);
                SetEndDate(DateTime.Now.AddDays(-1));
                SetCash(100000); // Required but not used for data download

                // Get symbols from environment or use defaults
                var symbolsFromEnv = Environment.GetEnvironmentVariable("ALARIS_SYMBOLS");
                if (!string.IsNullOrEmpty(symbolsFromEnv))
                {
                    _symbols = symbolsFromEnv.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                    Log($"Using symbols from environment: {string.Join(", ", _symbols)}");
                }
                else
                {
                    // Default symbols
                    _symbols = new List<string>
                    {
                        "SPY", "QQQ", "IWM", "EFA", "VTI",
                        "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA"
                    };
                    Log($"Using default symbols: {string.Join(", ", _symbols)}");
                }

                Log($"Configuring data download for {_symbols.Count} symbols");

                // Add securities to universe
                foreach (var symbol in _symbols)
                {
                    try
                    {
                        Log($"Adding {symbol} to universe...");
                        
                        // Add equity with daily resolution
                        var equity = AddEquity(symbol, Resolution.Daily, Market.USA);
                        equity.SetDataNormalizationMode(DataNormalizationMode.Adjusted);
                        
                        _lastDataTime[symbol] = DateTime.MinValue;
                        Log($"✓ Successfully added {symbol}");
                    }
                    catch (Exception ex)
                    {
                        Error($"Failed to add {symbol}: {ex.Message}");
                    }
                }

                Log($"✓ Data download algorithm initialized with {Securities.Count} securities");
                Log("✓ Data download will begin automatically");
                Log("=== Initialization Complete ===");
            }
            catch (Exception ex)
            {
                Error($"CRITICAL: Algorithm initialization failed: {ex.Message}");
                Error($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public override void OnData(Slice data)
        {
            try
            {
                // Count data points received
                foreach (var kvp in data.Bars)
                {
                    var symbol = kvp.Key.Value;
                    var bar = kvp.Value;
                    
                    _dataPointsReceived++;
                    _lastDataTime[symbol] = bar.Time;
                    
                    // Log every 500 data points
                    if (_dataPointsReceived % 500 == 0)
                    {
                        Log($"Progress: {_dataPointsReceived} data points received");
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"Error in OnData: {ex.Message}");
            }
        }

        public override void OnEndOfAlgorithm()
        {
            try
            {
                Log("=== Data Download Summary ===");
                Log($"Total data points processed: {_dataPointsReceived}");
                Log($"Securities configured: {Securities.Count}");
                
                var symbolsWithData = _lastDataTime.Where(kvp => kvp.Value > DateTime.MinValue).Count();
                Log($"Symbols with data: {symbolsWithData}/{_symbols.Count}");
                
                if (symbolsWithData > 0)
                {
                    Log("Symbols with data received:");
                    foreach (var kvp in _lastDataTime.Where(x => x.Value > DateTime.MinValue))
                    {
                        Log($"  ✓ {kvp.Key}: Latest {kvp.Value:yyyy-MM-dd}");
                    }
                }
                
                if (_dataPointsReceived == 0)
                {
                    Log("⚠ WARNING: No data was received during the download process");
                    Log("This might indicate API credential issues or data subscription limits");
                }
                else
                {
                    Log($"✓ Data download completed successfully with {_dataPointsReceived} data points");
                }
                
                Log("=== End Summary ===");
            }
            catch (Exception ex)
            {
                Error($"Error in OnEndOfAlgorithm: {ex.Message}");
            }
        }
    }
}