// src/csharp/Algorithm/DataDownload.cs - Fixed Version
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Alaris.Algorithm
{
    /// <summary>
    /// Production-grade data download algorithm designed to work with QuantConnect's ApiDataProvider.
    /// This algorithm properly configures the universe and handles the data download process
    /// with comprehensive error handling and progress reporting.
    /// </summary>
    public class DataDownload : QCAlgorithm
    {
        private readonly Dictionary<string, Symbol> _targetSymbols = new Dictionary<string, Symbol>();
        private readonly Dictionary<string, int> _dataPointsPerSymbol = new Dictionary<string, int>();
        private readonly Dictionary<string, DateTime> _lastDataPerSymbol = new Dictionary<string, DateTime>();
        private readonly List<string> _failedSymbols = new List<string>();
        
        private int _totalDataPoints = 0;
        private DateTime _algorithmStart;
        private TimeSpan _progressReportInterval = TimeSpan.FromMinutes(1);
        private DateTime _lastProgressReport = DateTime.MinValue;

        // Configuration
        private readonly List<string> _symbolsToDownload = new List<string>();
        private Resolution _targetResolution = Resolution.Daily;
        private bool _includeOptions = true;
        private bool _validateDataQuality = true;

        public override void Initialize()
        {
            try
            {
                _algorithmStart = DateTime.UtcNow;
                
                Log("=== Production Data Download Algorithm Starting ===");
                LogSystemConfiguration();

                // Configure the algorithm time range for comprehensive data download
                ConfigureTimeRange();

                // Load symbol configuration
                LoadSymbolConfiguration();

                // Set algorithm parameters optimized for data download
                SetCash(100000); // Required by Lean but not used for downloads
                
                // Configure universe with error handling
                ConfigureUniverse();

                // Log final configuration
                LogFinalConfiguration();

                Log("=== Data Download Algorithm Initialized Successfully ===");
                Log($"Total symbols configured: {_targetSymbols.Count}");
                Log($"Target resolution: {_targetResolution}");
                Log($"Include options: {_includeOptions}");
                Log($"Time range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Error($"Critical error during algorithm initialization: {ex.Message}");
                Error($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to stop execution
            }
        }

        private void LogSystemConfiguration()
        {
            Log("=== System Configuration ===");
            Log($"Data Provider: {Config.Get("data-provider", "Not Set")}");
            Log($"User ID: {Config.Get("job-user-id", "Not Set")}");
            Log($"Organization ID: {Config.Get("job-organization-id", "Not Set")}");
            Log($"Data Purchase Limit: {Config.Get("data-purchase-limit", "Not Set")}");
            Log($"API URL: {Config.Get("api-url", "Not Set")}");
            Log($"Environment: {Config.Get("environment", "Not Set")}");
            Log("==============================");
        }

        private void ConfigureTimeRange()
        {
            // Configure for comprehensive historical data download
            // Start from a reasonable historical point
            var startDate = new DateTime(2018, 1, 1);
            var endDate = DateTime.Now.Date.AddDays(-1); // End yesterday to ensure data completeness

            // Allow override from environment variables
            var envStartDate = Environment.GetEnvironmentVariable("ALARIS_DATA_START_DATE");
            var envEndDate = Environment.GetEnvironmentVariable("ALARIS_DATA_END_DATE");

            if (!string.IsNullOrEmpty(envStartDate) && DateTime.TryParse(envStartDate, out var customStart))
            {
                startDate = customStart;
                Log($"Using custom start date from environment: {startDate:yyyy-MM-dd}");
            }

            if (!string.IsNullOrEmpty(envEndDate) && DateTime.TryParse(envEndDate, out var customEnd))
            {
                endDate = customEnd;
                Log($"Using custom end date from environment: {endDate:yyyy-MM-dd}");
            }

            SetStartDate(startDate);
            SetEndDate(endDate);

            Log($"Configured time range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        }

        private void LoadSymbolConfiguration()
        {
            // Load symbols from environment variable or use production defaults
            var symbolsConfig = Environment.GetEnvironmentVariable("ALARIS_DOWNLOAD_SYMBOLS");
            var resolutionConfig = Environment.GetEnvironmentVariable("ALARIS_DOWNLOAD_RESOLUTION");
            var includeOptionsConfig = Environment.GetEnvironmentVariable("ALARIS_DOWNLOAD_OPTIONS");

            if (!string.IsNullOrEmpty(symbolsConfig))
            {
                _symbolsToDownload.AddRange(symbolsConfig.Split(',').Select(s => s.Trim().ToUpper()));
                Log($"Loaded {_symbolsToDownload.Count} symbols from environment configuration");
            }
            else
            {
                // Production default symbols - comprehensive set for volatility arbitrage
                _symbolsToDownload.AddRange(new[]
                {
                    // Major ETFs
                    "SPY", "QQQ", "IWM", "EFA", "VTI", "TLT", "GLD", "VIX",
                    
                    // Technology
                    "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "TSLA", "META", "NFLX",
                    
                    // Financial
                    "JPM", "BAC", "WFC", "GS", "MS", "C", "BRK.B", "V",
                    
                    // Healthcare
                    "JNJ", "PFE", "UNH", "ABBV", "MRK", "TMO", "DHR", "ABT",
                    
                    // Energy
                    "XOM", "CVX", "COP", "EOG", "SLB", "MPC", "PSX", "VLO",
                    
                    // Consumer
                    "KO", "PEP", "PG", "WMT", "HD", "MCD", "DIS", "NKE"
                });
                Log($"Using production default symbols: {_symbolsToDownload.Count} symbols");
            }

            // Configure resolution
            if (!string.IsNullOrEmpty(resolutionConfig) && Enum.TryParse<Resolution>(resolutionConfig, true, out var resolution))
            {
                _targetResolution = resolution;
                Log($"Using resolution from environment: {_targetResolution}");
            }
            else
            {
                _targetResolution = Resolution.Daily; // Default for comprehensive download
                Log($"Using default resolution: {_targetResolution}");
            }

            // Configure options inclusion
            if (!string.IsNullOrEmpty(includeOptionsConfig) && bool.TryParse(includeOptionsConfig, out var includeOptions))
            {
                _includeOptions = includeOptions;
            }
            else
            {
                _includeOptions = true; // Default to include options for volatility arbitrage
            }

            Log($"Options inclusion: {_includeOptions}");
        }

        private void ConfigureUniverse()
        {
            Log("Configuring trading universe...");
            
            var successfulSymbols = 0;
            var failedSymbols = 0;

            foreach (var symbolString in _symbolsToDownload)
            {
                try
                {
                    Log($"Adding symbol: {symbolString}");

                    // Add the underlying equity
                    var equity = AddEquity(symbolString, _targetResolution, Market.USA);
                    
                    // Configure the security for optimal data collection
                    var security = Securities[equity.Symbol];
                    security.SetDataNormalizationMode(DataNormalizationMode.Adjusted);
                    security.SetLeverage(1.0m); // Set leverage for proper risk calculations

                    // Store the symbol for tracking
                    _targetSymbols[symbolString] = equity.Symbol;
                    _dataPointsPerSymbol[symbolString] = 0;
                    _lastDataPerSymbol[symbolString] = DateTime.MinValue;

                    Log($"✓ Successfully added equity: {symbolString}");

                    // Add options chain if enabled
                    if (_includeOptions)
                    {
                        try
                        {
                            var option = AddOption(equity.Symbol, _targetResolution);
                            
                            // Configure option filter for comprehensive but reasonable data collection
                            option.SetFilter(universe => universe.IncludeWeeklys()
                                                               .Strikes(-20, +20) // Wide strike range
                                                               .Expiration(TimeSpan.FromDays(0), TimeSpan.FromDays(365))); // Up to 1 year

                            Log($"✓ Successfully added options for: {symbolString}");
                        }
                        catch (Exception optEx)
                        {
                            Log($"⚠ Could not add options for {symbolString}: {optEx.Message}");
                            // Continue - equity data is more important
                        }
                    }

                    successfulSymbols++;
                }
                catch (Exception ex)
                {
                    Error($"✗ Failed to add symbol {symbolString}: {ex.Message}");
                    _failedSymbols.Add(symbolString);
                    failedSymbols++;
                }
            }

            Log($"Universe configuration complete:");
            Log($"  ✓ Successful: {successfulSymbols} symbols");
            Log($"  ✗ Failed: {failedSymbols} symbols");
            
            if (failedSymbols > 0)
            {
                Log($"Failed symbols: {string.Join(", ", _failedSymbols)}");
            }

            if (successfulSymbols == 0)
            {
                throw new InvalidOperationException("No symbols were successfully added to the universe!");
            }
        }

        private void LogFinalConfiguration()
        {
            Log("=== Final Algorithm Configuration ===");
            Log($"Successfully configured symbols: {_targetSymbols.Count}");
            Log($"Target resolution: {_targetResolution}");
            Log($"Start date: {StartDate:yyyy-MM-dd}");
            Log($"End date: {EndDate:yyyy-MM-dd}");
            Log($"Include options: {_includeOptions}");
            Log($"Data quality validation: {_validateDataQuality}");
            Log($"Progress report interval: {_progressReportInterval.TotalMinutes} minutes");
            Log("=====================================");
        }

        public override void OnData(Slice data)
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                
                // Process equity data
                ProcessEquityData(data);
                
                // Process options data if available
                if (data.OptionChains != null && data.OptionChains.Count > 0)
                {
                    ProcessOptionsData(data);
                }

                // Periodic progress reporting
                if (currentTime - _lastProgressReport >= _progressReportInterval)
                {
                    ReportProgress();
                    _lastProgressReport = currentTime;
                }

                // Validate data quality periodically
                if (_validateDataQuality && _totalDataPoints % 10000 == 0 && _totalDataPoints > 0)
                {
                    ValidateDataQuality();
                }
            }
            catch (Exception ex)
            {
                Error($"Error processing data slice: {ex.Message}");
                // Don't throw - continue processing other data
            }
        }

        private void ProcessEquityData(Slice data)
        {
            foreach (var bar in data.Bars.Values)
            {
                _totalDataPoints++;
                
                var symbolString = bar.Symbol.Value;
                if (_dataPointsPerSymbol.ContainsKey(symbolString))
                {
                    _dataPointsPerSymbol[symbolString]++;
                    _lastDataPerSymbol[symbolString] = bar.Time;

                    // Log first few data points for each symbol for verification
                    if (_dataPointsPerSymbol[symbolString] <= 3)
                    {
                        Log($"Data point for {symbolString}: {bar.Time:yyyy-MM-dd}, " +
                            $"O:{bar.Open:F2}, H:{bar.High:F2}, L:{bar.Low:F2}, C:{bar.Close:F2}, V:{bar.Volume}");
                    }
                }
            }
        }

        private void ProcessOptionsData(Slice data)
        {
            foreach (var optionChain in data.OptionChains.Values)
            {
                foreach (var contract in optionChain)
                {
                    _totalDataPoints++;
                    
                    // Log sample options data for verification
                    if (_totalDataPoints % 5000 == 0)
                    {
                        Log($"Options data: {contract.Symbol}, " +
                            $"Strike: {contract.Strike}, Expiry: {contract.Expiry:yyyy-MM-dd}, " +
                            $"Bid: {contract.BidPrice:F4}, Ask: {contract.AskPrice:F4}");
                    }
                }
            }
        }

        private void ReportProgress()
        {
            try
            {
                var elapsed = DateTime.UtcNow - _algorithmStart;
                var symbolsWithData = _dataPointsPerSymbol.Values.Count(x => x > 0);
                
                Log("=== Download Progress Report ===");
                Log($"Elapsed time: {elapsed.TotalMinutes:F1} minutes");
                Log($"Total data points processed: {_totalDataPoints:N0}");
                Log($"Symbols with data: {symbolsWithData}/{_targetSymbols.Count}");
                Log($"Average data points per symbol: {(symbolsWithData > 0 ? _totalDataPoints / symbolsWithData : 0):N0}");
                
                // Show top 5 symbols by data volume
                var topSymbols = _dataPointsPerSymbol
                    .Where(x => x.Value > 0)
                    .OrderByDescending(x => x.Value)
                    .Take(5)
                    .ToList();
                
                if (topSymbols.Any())
                {
                    Log("Top symbols by data volume:");
                    foreach (var symbol in topSymbols)
                    {
                        Log($"  {symbol.Key}: {symbol.Value:N0} data points, last: {_lastDataPerSymbol[symbol.Key]:yyyy-MM-dd}");
                    }
                }

                // Check for symbols without data
                var symbolsWithoutData = _dataPointsPerSymbol
                    .Where(x => x.Value == 0)
                    .Select(x => x.Key)
                    .ToList();

                if (symbolsWithoutData.Any() && symbolsWithoutData.Count <= 10)
                {
                    Log($"Symbols without data yet: {string.Join(", ", symbolsWithoutData)}");
                }
                else if (symbolsWithoutData.Count > 10)
                {
                    Log($"Symbols without data yet: {symbolsWithoutData.Count} symbols");
                }

                Log("==============================");
            }
            catch (Exception ex)
            {
                Error($"Error reporting progress: {ex.Message}");
            }
        }

        private void ValidateDataQuality()
        {
            try
            {
                Log("=== Data Quality Validation ===");
                
                var issues = new List<string>();
                var warnings = new List<string>();

                foreach (var kvp in _dataPointsPerSymbol)
                {
                    var symbol = kvp.Key;
                    var count = kvp.Value;
                    
                    if (count == 0)
                    {
                        issues.Add($"{symbol}: No data received");
                    }
                    else if (count < 10)
                    {
                        warnings.Add($"{symbol}: Only {count} data points (may be normal for new symbols)");
                    }
                    else
                    {
                        // Check if we have current price data
                        if (_targetSymbols.TryGetValue(symbol, out var symbolObj) && Securities.ContainsKey(symbolObj))
                        {
                            var security = Securities[symbolObj];
                            if (security.Price <= 0)
                            {
                                warnings.Add($"{symbol}: No current price data");
                            }
                        }
                    }
                }

                // Report validation results
                if (issues.Count == 0 && warnings.Count == 0)
                {
                    Log("✓ Data quality validation passed - no issues detected");
                }
                else
                {
                    if (issues.Count > 0)
                    {
                        Log($"✗ Found {issues.Count} data quality issues:");
                        foreach (var issue in issues.Take(10)) // Limit output
                        {
                            Log($"  - {issue}");
                        }
                        if (issues.Count > 10)
                        {
                            Log($"  ... and {issues.Count - 10} more issues");
                        }
                    }

                    if (warnings.Count > 0)
                    {
                        Log($"⚠ Found {warnings.Count} data quality warnings:");
                        foreach (var warning in warnings.Take(5)) // Limit output
                        {
                            Log($"  - {warning}");
                        }
                        if (warnings.Count > 5)
                        {
                            Log($"  ... and {warnings.Count - 5} more warnings");
                        }
                    }
                }

                Log("==============================");
            }
            catch (Exception ex)
            {
                Error($"Error during data quality validation: {ex.Message}");
            }
        }

        public override void OnEndOfAlgorithm()
        {
            try
            {
                var totalElapsed = DateTime.UtcNow - _algorithmStart;
                
                Log("=== Production Data Download Complete ===");
                Log($"Total execution time: {totalElapsed.TotalMinutes:F1} minutes");
                Log($"Total data points processed: {_totalDataPoints:N0}");
                
                // Final statistics
                GenerateFinalReport();
                
                // Provide next steps guidance
                ProvideNextStepsGuidance();
                
                Log("=== End Data Download Algorithm ===");
            }
            catch (Exception ex)
            {
                Error($"Error in OnEndOfAlgorithm: {ex.Message}");
            }
        }

        private void GenerateFinalReport()
        {
            Log("\n=== Final Download Report ===");
            
            var symbolsWithData = _dataPointsPerSymbol.Where(x => x.Value > 0).ToList();
            var symbolsWithoutData = _dataPointsPerSymbol.Where(x => x.Value == 0).ToList();
            
            Log($"Summary:");
            Log($"  ✓ Symbols with data: {symbolsWithData.Count}");
            Log($"  ✗ Symbols without data: {symbolsWithoutData.Count}");
            Log($"  ✗ Failed to configure: {_failedSymbols.Count}");
            Log($"  Total target symbols: {_symbolsToDownload.Count}");
            
            if (symbolsWithData.Any())
            {
                var avgDataPoints = symbolsWithData.Average(x => x.Value);
                var maxDataPoints = symbolsWithData.Max(x => x.Value);
                var minDataPoints = symbolsWithData.Min(x => x.Value);
                
                Log($"\nData volume statistics:");
                Log($"  Average data points per symbol: {avgDataPoints:F0}");
                Log($"  Maximum data points: {maxDataPoints:N0}");
                Log($"  Minimum data points: {minDataPoints:N0}");
                
                // Show most successful downloads
                var topDownloads = symbolsWithData
                    .OrderByDescending(x => x.Value)
                    .Take(10)
                    .ToList();
                
                Log($"\nTop downloads:");
                foreach (var download in topDownloads)
                {
                    Log($"  {download.Key}: {download.Value:N0} data points");
                }
            }
            
            if (symbolsWithoutData.Any())
            {
                Log($"\nSymbols without data: {string.Join(", ", symbolsWithoutData.Select(x => x.Key))}");
            }
            
            if (_failedSymbols.Any())
            {
                Log($"\nFailed symbols: {string.Join(", ", _failedSymbols)}");
            }
            
            Log("==============================");
        }

        private void ProvideNextStepsGuidance()
        {
            var successfulSymbols = _dataPointsPerSymbol.Count(x => x.Value > 0);
            
            Log("\n=== Next Steps ===");
            
            if (successfulSymbols > 0)
            {
                Log("✓ Data download completed successfully!");
                Log("\nRecommended next steps:");
                Log("1. Run backtest: './start-alaris.sh backtest'");
                Log("2. Verify data files in the 'data' directory");
                Log("3. Review any symbols that failed to download");
                Log("4. Consider paper trading: './start-alaris.sh paper'");
                
                if (successfulSymbols < _symbolsToDownload.Count)
                {
                    Log($"\nNote: {_symbolsToDownload.Count - successfulSymbols} symbols failed to download.");
                    Log("This may be due to:");
                    Log("- Symbols not available in QuantConnect's data");
                    Log("- Network issues during download");
                    Log("- Insufficient data subscription");
                    Log("- API rate limiting");
                }
            }
            else
            {
                Log("✗ No data was successfully downloaded!");
                Log("\nTroubleshooting steps:");
                Log("1. Verify QuantConnect API credentials");
                Log("2. Check network connectivity");
                Log("3. Ensure data subscription is active");
                Log("4. Review the error logs for specific issues");
                Log("5. Try downloading a smaller symbol set");
            }
            
            Log("=================");
        }
    }
}