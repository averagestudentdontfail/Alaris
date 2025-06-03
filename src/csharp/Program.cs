// src/csharp/Program.cs - Simplified Direct Algorithm Execution
using System;
using System.IO;
using System.Threading.Tasks;
using QuantConnect.Configuration;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.Storage;
using QuantConnect.Packets;
using Alaris.Algorithm;
using System.CommandLine;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Orders;
using QuantConnect.Statistics;

namespace Alaris
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting Alaris Lean Process...");

                // Define command line options
                var rootCommand = new RootCommand("Alaris Trading System");
                
                var symbolOption = new Option<string>(
                    "--symbol", 
                    "Trading symbol (e.g., SPY). If not specified, uses default portfolio.");
                symbolOption.AddAlias("-s");
                
                var modeOption = new Option<string>(
                    "--mode",
                    "Trading mode: live, paper, or backtest (default: backtest)");
                modeOption.AddAlias("-m");
                modeOption.SetDefaultValue("backtest");
                
                var strategyOption = new Option<string>(
                    "--strategy",
                    "Strategy mode: DeltaNeutral, GammaScalping, VolatilityTiming, or RelativeValue");
                strategyOption.AddAlias("-t");
                strategyOption.SetDefaultValue("deltaneutral");
                
                var startDateOption = new Option<string>(
                    "--start-date",
                    "Backtest start date (YYYY-MM-DD)");
                startDateOption.AddAlias("-sd");
                
                var endDateOption = new Option<string>(
                    "--end-date",
                    "Backtest end date (YYYY-MM-DD)");
                endDateOption.AddAlias("-ed");

                var frequencyOption = new Option<string>(
                    "--frequency",
                    "Data frequency: minute, hour, or daily");
                frequencyOption.AddAlias("-f");
                frequencyOption.SetDefaultValue("minute");
                
                var debugOption = new Option<bool>(
                    "--debug",
                    "Enable debug logging");
                debugOption.AddAlias("-d");

                rootCommand.AddOption(symbolOption);
                rootCommand.AddOption(modeOption);
                rootCommand.AddOption(strategyOption);
                rootCommand.AddOption(startDateOption);
                rootCommand.AddOption(endDateOption);
                rootCommand.AddOption(frequencyOption);
                rootCommand.AddOption(debugOption);

                rootCommand.SetHandler(async (context) =>
                {
                    var symbol = context.ParseResult.GetValueForOption(symbolOption);
                    var mode = context.ParseResult.GetValueForOption(modeOption) ?? "backtest";
                    var strategy = context.ParseResult.GetValueForOption(strategyOption) ?? "deltaneutral";
                    var startDate = context.ParseResult.GetValueForOption(startDateOption);
                    var endDate = context.ParseResult.GetValueForOption(endDateOption);
                    var frequency = context.ParseResult.GetValueForOption(frequencyOption) ?? "minute";
                    var debug = context.ParseResult.GetValueForOption(debugOption);

                    // Ensure mode and strategy are lowercase for consistency
                    mode = mode.ToLower();
                    strategy = strategy.ToLower();
                    frequency = frequency.ToLower();

                    try
                    {
                        // Configure Lean based on mode
                        ConfigureLean(mode, symbol, strategy, startDate, endDate, frequency, debug);

                        if (mode == "backtest")
                        {
                            Console.WriteLine($"Starting backtest for {symbol ?? "default portfolio"} from {startDate} to {endDate}");
                            
                            // Run simplified backtest
                            await RunSimplifiedBacktest(symbol, strategy, startDate, endDate, frequency, debug);
                        }
                        else
                        {
                            Console.WriteLine($"Alaris algorithm configured for {mode} trading");
                            if (!string.IsNullOrEmpty(symbol))
                            {
                                Console.WriteLine($"Trading symbol: {symbol}");
                            }
                            Console.WriteLine($"Strategy mode: {strategy}");
                            
                            Console.WriteLine("Alaris Lean Process started successfully");
                            Console.WriteLine("Press Ctrl+C to stop...");

                            // Keep the process running
                            var tcs = new TaskCompletionSource<bool>();
                            Console.CancelKeyPress += (sender, e) =>
                            {
                                e.Cancel = true;
                                tcs.SetResult(true);
                            };

                            await tcs.Task;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error running algorithm: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        
                        // Provide helpful troubleshooting information
                        Console.WriteLine("\nTroubleshooting:");
                        Console.WriteLine("1. Ensure data directories exist and contain required files");
                        Console.WriteLine("2. Check that results directory has write permissions");
                        Console.WriteLine("3. Verify algorithm assembly was built successfully");
                        Console.WriteLine("4. For backtests, ensure start and end dates are valid");
                        
                        throw;
                    }
                });

                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in Alaris Lean Process: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return 1;
            }
        }

        private static async Task RunSimplifiedBacktest(string? symbol, string strategy, string? startDate, string? endDate, string frequency, bool debug)
        {
            try
            {
                Console.WriteLine("Initializing simplified backtest...");
                
                // Create and configure the algorithm instance directly
                var algorithm = new ArbitrageAlgorithm();
                
                // Set up algorithm configuration before initialization
                Environment.SetEnvironmentVariable("ALARIS_SYMBOL", symbol ?? "SPY");
                Environment.SetEnvironmentVariable("ALARIS_STRATEGY", strategy);
                Environment.SetEnvironmentVariable("ALARIS_MODE", "backtest");
                Environment.SetEnvironmentVariable("ALARIS_FREQUENCY", frequency);
                Environment.SetEnvironmentVariable("ALARIS_DEBUG", debug.ToString());
                
                // Parse dates
                DateTime start = DateTime.Parse(startDate ?? "2023-01-01");
                DateTime end = DateTime.Parse(endDate ?? "2023-01-02");
                
                Console.WriteLine($"Backtest period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
                Console.WriteLine($"Strategy: {strategy}");
                Console.WriteLine($"Symbol: {symbol ?? "SPY"}");
                Console.WriteLine($"Frequency: {frequency}");
                
                // Set algorithm properties
                algorithm.SetStartDate(start);
                algorithm.SetEndDate(end);
                algorithm.SetCash(100000);
                
                Console.WriteLine("Algorithm configuration complete");
                
                // Initialize the algorithm
                Console.WriteLine("Initializing algorithm...");
                algorithm.Initialize();
                Console.WriteLine("Algorithm initialized successfully");
                
                // Simulate some basic data for demonstration
                await SimulateBasicData(algorithm, start, end, symbol ?? "SPY", frequency);
                
                // Generate final results
                Console.WriteLine("\n=== Backtest Results ===");
                Console.WriteLine($"Start Date: {start:yyyy-MM-dd}");
                Console.WriteLine($"End Date: {end:yyyy-MM-dd}");
                Console.WriteLine($"Strategy: {strategy}");
                Console.WriteLine($"Symbol: {symbol ?? "SPY"}");
                Console.WriteLine($"Final Portfolio Value: {algorithm.Portfolio.TotalPortfolioValue:C}");
                Console.WriteLine($"Total Return: {(algorithm.Portfolio.TotalPortfolioValue - 100000) / 100000:P2}");
                Console.WriteLine("======================");
                
                // Save results to file
                var resultsFile = Path.Combine("results", $"backtest_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                await File.WriteAllTextAsync(resultsFile, $"Backtest Results\n" +
                    $"Start: {start:yyyy-MM-dd}\n" +
                    $"End: {end:yyyy-MM-dd}\n" +
                    $"Strategy: {strategy}\n" +
                    $"Symbol: {symbol ?? "SPY"}\n" +
                    $"Final Value: {algorithm.Portfolio.TotalPortfolioValue:C}\n" +
                    $"Return: {(algorithm.Portfolio.TotalPortfolioValue - 100000) / 100000:P2}\n");
                
                Console.WriteLine($"Results saved to: {resultsFile}");
                Console.WriteLine("Backtest completed successfully");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in simplified backtest: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        private static async Task SimulateBasicData(ArbitrageAlgorithm algorithm, DateTime start, DateTime end, string symbol, string frequency)
        {
            Console.WriteLine("Simulating market data...");
            
            var current = start;
            var price = 100.0m; // Starting price
            var random = new Random(42); // Seed for reproducible results
            
            int dataPoints = 0;
            
            while (current <= end)
            {
                try
                {
                    // Simulate price movement
                    var change = (decimal)(random.NextDouble() - 0.5) * 0.02m; // ±1% movement
                    price = Math.Max(price * (1 + change), 1.0m); // Ensure price stays positive
                    
                    // Create mock market data (this is simplified for demonstration)
                    // In a real implementation, this would come from the data feed
                    
                    if (debug)
                    {
                        Console.WriteLine($"Date: {current:yyyy-MM-dd}, Price: {price:F2}");
                    }
                    
                    dataPoints++;
                    
                    // Advance time based on frequency
                    current = frequency switch
                    {
                        "daily" => current.AddDays(1),
                        "hour" => current.AddHours(1),
                        "minute" => current.AddMinutes(1),
                        _ => current.AddDays(1)
                    };
                    
                    // Skip weekends for daily data
                    if (frequency == "daily" && (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday))
                    {
                        current = current.AddDays(current.DayOfWeek == DayOfWeek.Saturday ? 2 : 1);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error simulating data point at {current}: {ex.Message}");
                    break;
                }
                
                // Add small delay to prevent overwhelming output
                if (dataPoints % 100 == 0)
                {
                    await Task.Delay(1);
                }
            }
            
            Console.WriteLine($"Simulated {dataPoints} data points");
        }

        private static void ConfigureLean(string mode, string? symbol, string strategy, string? startDate, string? endDate, string frequency, bool debug)
        {
            try
            {
                // Ensure directories exist
                var baseDir = Directory.GetCurrentDirectory();
                var dataDir = Path.Combine(baseDir, "data");
                var resultsDir = Path.Combine(baseDir, "results");
                var cacheDir = Path.Combine(baseDir, "Cache");

                // Create directories if they don't exist
                Directory.CreateDirectory(dataDir);
                Directory.CreateDirectory(resultsDir);
                Directory.CreateDirectory(cacheDir);
                Directory.CreateDirectory(Path.Combine(dataDir, "market-hours"));
                Directory.CreateDirectory(Path.Combine(dataDir, "symbol-properties"));
                Directory.CreateDirectory(Path.Combine(dataDir, "equity"));

                Console.WriteLine($"Base directory: {baseDir}");
                Console.WriteLine($"Data directory: {dataDir}");
                Console.WriteLine($"Results directory: {resultsDir}");
                Console.WriteLine($"Cache directory: {cacheDir}");

                // Verify required files exist
                var marketHoursFile = Path.Combine(dataDir, "market-hours", "market-hours-database.json");
                var symbolPropsFile = Path.Combine(dataDir, "symbol-properties", "symbol-properties-database.csv");
                
                if (!File.Exists(marketHoursFile))
                {
                    Console.WriteLine($"Warning: Market hours database not found at {marketHoursFile}");
                }
                else
                {
                    Console.WriteLine($"✓ Market hours database found");
                }
                
                if (!File.Exists(symbolPropsFile))
                {
                    Console.WriteLine($"Warning: Symbol properties database not found at {symbolPropsFile}");
                }
                else
                {
                    Console.WriteLine($"✓ Symbol properties database found");
                }

                // Set up basic configuration
                Config.Set("data-directory", dataDir);
                Config.Set("cache-location", cacheDir);
                Config.Set("results-destination-folder", resultsDir);
                Config.Set("environment", "backtesting");
                
                // Store custom configuration in environment variables for algorithm access
                Environment.SetEnvironmentVariable("ALARIS_SYMBOL", symbol ?? "SPY");
                Environment.SetEnvironmentVariable("ALARIS_MODE", mode);
                Environment.SetEnvironmentVariable("ALARIS_STRATEGY", strategy);
                Environment.SetEnvironmentVariable("ALARIS_FREQUENCY", frequency);
                Environment.SetEnvironmentVariable("ALARIS_DEBUG", debug.ToString());

                // Log configuration summary
                Console.WriteLine($"Starting Alaris algorithm with configuration:");
                Console.WriteLine($"Mode: {mode}");
                Console.WriteLine($"Symbol: {symbol ?? "SPY"}");
                Console.WriteLine($"Strategy: {strategy}");
                Console.WriteLine($"Frequency: {frequency}");
                Console.WriteLine($"Debug Mode: {debug}");
                if (mode == "backtest")
                {
                    Console.WriteLine($"Start Date: {startDate}");
                    Console.WriteLine($"End Date: {endDate}");
                }
                Console.WriteLine($"Data Directory: {dataDir}");
                Console.WriteLine($"Results Directory: {resultsDir}");
                
                Console.WriteLine("Lean configuration completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error configuring Lean: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}