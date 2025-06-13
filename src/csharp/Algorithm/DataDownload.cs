// src/csharp/Algorithm/DataDownload.cs
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Alaris.Algorithm
{
    /// <summary>
    /// A robust and simplified algorithm specifically designed for downloading historical data.
    /// It adds securities one by one with proper error handling to prevent initialization failures.
    /// </summary>
    public class DataDownload : QCAlgorithm
    {
        private List<string> _symbolsToDownload = new List<string>();
        private int _dataPointsReceived = 0;

        public override void Initialize()
        {
            try
            {
                Log("=== Robust Data Download Algorithm Initializing ===");

                // Set a wide time range to ensure all available data is downloaded.
                SetStartDate(2018, 1, 1);
                SetEndDate(DateTime.Now.Date.AddDays(-1)); // End yesterday to ensure data is finalized.
                SetCash(100000); // Required by Lean, but not used.

                // Configure universe symbols from environment or use defaults
                var symbolsConfig = Environment.GetEnvironmentVariable("ALARIS_SYMBOLS");
                if (!string.IsNullOrEmpty(symbolsConfig))
                {
                    _symbolsToDownload = symbolsConfig.Split(',').Select(s => s.Trim().ToUpper()).ToList();
                }
                else
                {
                    // Default symbols - same as in main algorithm
                    _symbolsToDownload = new List<string>
                    {
                        "SPY", "QQQ", "IWM", "EFA", "VTI",
                        "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA",
                        "JPM", "BAC", "WFC", "GS", "MS",
                        "XOM", "CVX", "COP", "EOG", "SLB",
                        "JNJ", "PFE", "UNH", "ABBV", "MRK"
                    };
                }

                Log($"Configuring data download for {_symbolsToDownload.Count} symbols.");

                // Add securities to the universe one by one with error handling.
                foreach (var symbolStr in _symbolsToDownload)
                {
                    try
                    {
                        // Adding just the daily equity is enough to trigger Lean's downloader
                        // for all data resolutions associated with that symbol.
                        var equity = AddEquity(symbolStr, Resolution.Daily);

                        // Also request options to ensure their data is downloaded.
                        // The filter is broad to capture a good range of contracts.
                        var option = AddOption(equity.Symbol, Resolution.Minute);
                        option.SetFilter(-25, 25, TimeSpan.FromDays(0), TimeSpan.FromDays(180));

                        Log($"✓ Added '{symbolStr}' to universe for data download.");
                    }
                    catch (Exception ex)
                    {
                        // Log the error for a specific symbol but do not crash the entire process.
                        Error($"Failed to add '{symbolStr}' to universe: {ex.Message}");
                    }
                }

                Log($"✓ Universe configured with {Securities.Count} securities.");
                Log("Data download will begin automatically. This may take a significant amount of time.");
                Log("=== Initialization Complete ===");
            }
            catch (Exception ex)
            {
                Error($"A critical error occurred during initialization: {ex.Message}");
                Error($"Stack trace: {ex.StackTrace}");
                Quit("Initialization failed catastrophically.");
            }
        }

        /// <summary>
        /// The OnData event is used here simply to provide progress feedback in the logs.
        /// </summary>
        public override void OnData(Slice data)
        {
            _dataPointsReceived += data.Bars.Count;
            if (_dataPointsReceived > 0 && _dataPointsReceived % 10000 == 0)
            {
                Log($"... processed {_dataPointsReceived} data points ...");
            }
        }

        public override void OnEndOfAlgorithm()
        {
            Log("=== Data Download Algorithm Finished ===");
            Log($"Total data points processed: {_dataPointsReceived}");
            Log("Check the 'data' directory in your project's build folder to verify the downloaded files.");
            Log("You can now run backtests using the downloaded data.");
        }
    }
}