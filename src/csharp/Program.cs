// src/csharp/Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using QuantConnect.Configuration;
using System.CommandLine;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Lean.Engine.Results;

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
                var rootCommand = new RootCommand("Alaris Trading System - Lean Integration");
                
                var symbolOption = new Option<string>("--symbol", "Trading symbol (e.g., SPY)");
                symbolOption.SetDefaultValue("SPY");
                
                var modeOption = new Option<string>("--mode", "Trading mode: live, paper, or backtest");
                modeOption.SetDefaultValue("backtest");
                
                var strategyOption = new Option<string>("--strategy", "Strategy mode");
                strategyOption.SetDefaultValue("deltaneutral");
                
                var startDateOption = new Option<string>("--start-date", "Backtest start date (YYYY-MM-DD)");
                var endDateOption = new Option<string>("--end-date", "Backtest end date (YYYY-MM-DD)");
                
                var frequencyOption = new Option<string>("--frequency", "Data frequency: minute, hour, or daily");
                frequencyOption.SetDefaultValue("minute");
                
                var debugOption = new Option<bool>("--debug", "Enable debug logging");

                rootCommand.AddOption(symbolOption);
                rootCommand.AddOption(modeOption);
                rootCommand.AddOption(strategyOption);
                rootCommand.AddOption(startDateOption);
                rootCommand.AddOption(endDateOption);
                rootCommand.AddOption(frequencyOption);
                rootCommand.AddOption(debugOption);

                rootCommand.SetHandler(async (context) =>
                {
                    var symbol = context.ParseResult.GetValueForOption(symbolOption) ?? "SPY";
                    var mode = context.ParseResult.GetValueForOption(modeOption) ?? "backtest";
                    var strategy = context.ParseResult.GetValueForOption(strategyOption) ?? "deltaneutral";
                    var startDate = context.ParseResult.GetValueForOption(startDateOption);
                    var endDate = context.ParseResult.GetValueForOption(endDateOption);
                    var frequency = context.ParseResult.GetValueForOption(frequencyOption) ?? "minute";
                    var debug = context.ParseResult.GetValueForOption(debugOption);

                    await RunAlaris(symbol, mode, strategy, startDate, endDate, frequency, debug);
                });

                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fatal error in Alaris Lean Process:");
                return 1;
            }
        }

        private static Task RunAlaris(string symbol, string mode, string strategy, 
                                          string? startDate, string? endDate, string frequency, bool debug)
        {
            try
            {
                // Display configuration
                Console.WriteLine($"\nStarting Alaris with configuration:");
                Console.WriteLine($"  Symbol: {symbol}");
                Console.WriteLine($"  Mode: {mode}");
                Console.WriteLine($"  Strategy: {strategy}");
                Console.WriteLine($"  Frequency: {frequency}");
                Console.WriteLine($"  Debug: {debug}");
                if (mode == "backtest")
                {
                    Console.WriteLine($"  Start date: {startDate}");
                    Console.WriteLine($"  End date: {endDate}");
                }
                Console.WriteLine();
                
                // --- CRITICAL FIX: Correct Data Path Resolution ---
                
                // Get the current working directory (should be project root)
                string currentDirectory = Directory.GetCurrentDirectory();
                Console.WriteLine($"[DEBUG] Current directory: {currentDirectory}");
                
                // Look for build directory in current directory or parent directories
                string? buildDirectory = FindBuildDirectory(currentDirectory);
                
                if (buildDirectory == null)
                {
                    throw new DirectoryNotFoundException(
                        "Could not find build directory. Please run from project root or ensure build directory exists.");
                }
                
                string dataFolderPath = Path.Combine(buildDirectory, "data");
                string cacheFolderPath = Path.Combine(buildDirectory, "cache");
                string resultsFolderPath = Path.Combine(buildDirectory, "results");
                
                Console.WriteLine($"[INFO] Build directory: {buildDirectory}");
                Console.WriteLine($"[INFO] Data folder: {dataFolderPath}");
                Console.WriteLine($"[INFO] Cache folder: {cacheFolderPath}");
                Console.WriteLine($"[INFO] Results folder: {resultsFolderPath}");
                
                // Validate that data directory exists
                if (!Directory.Exists(dataFolderPath))
                {
                    throw new DirectoryNotFoundException(
                        $"Data directory not found at: {dataFolderPath}\n" +
                        "Please run 'cmake --build . --target setup-data' to set up data.");
                }
                
                // Validate essential data files exist
                string marketHoursFile = Path.Combine(dataFolderPath, "market-hours", "market-hours-database.json");
                if (!File.Exists(marketHoursFile))
                {
                    throw new FileNotFoundException(
                        $"Required data file not found: {marketHoursFile}\n" +
                        "Please run 'cmake --build . --target setup-data' to download required data files.");
                }
                
                Console.WriteLine($"[INFO] ✓ Data validation passed");
                
                // Set environment variables for the algorithm to access
                Environment.SetEnvironmentVariable("ALARIS_SYMBOL", symbol);
                Environment.SetEnvironmentVariable("ALARIS_STRATEGY", strategy);

                // Configure Lean using the parsed arguments
                var liveMode = !mode.Equals("backtest", StringComparison.OrdinalIgnoreCase);
                Config.Set("environment", liveMode ? "live-trading" : "backtesting");
                Config.Set("live-mode", liveMode.ToString().ToLower());
                
                Config.Set("algorithm-type-name", "Alaris.Algorithm.ArbitrageAlgorithm");
                Config.Set("algorithm-location", typeof(Program).Assembly.Location);
                
                // --- CRITICAL: Set correct data paths ---
                Config.Set("data-folder", dataFolderPath);
                Config.Set("cache-location", cacheFolderPath);
                Config.Set("results-destination-folder", resultsFolderPath);
                
                Config.Set("resolution", frequency);
                
                if (!liveMode)
                {
                     if (DateTime.TryParse(startDate, out var start))
                     {
                        Config.Set("start-date", start.ToString("yyyyMMdd"));
                     }
                     if (DateTime.TryParse(endDate, out var end))
                     {
                        Config.Set("end-date", end.ToString("yyyyMMdd"));
                     }
                }
                else 
                {
                    Config.Set("live-mode-brokerage", "InteractiveBrokersBrokerage");
                }
                
                Config.Set("debug-mode", debug.ToString().ToLower());
                Log.DebuggingEnabled = debug;

                Console.WriteLine("Initializing and running Lean engine in-process...");
                
                var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
                var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
                systemHandlers.Initialize();
                
                string assemblyPath = Config.Get("algorithm-location");
                var algorithmManager = new AlgorithmManager(liveMode, null);
                
                // Initialize the Lean manager with correct arguments
                systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, new BacktestNodePacket(), algorithmManager);
                
                var engine = new Engine(systemHandlers, algorithmHandlers, liveMode);
                engine.Run(new BacktestNodePacket(), algorithmManager, assemblyPath, WorkerThread.Instance);

                Console.WriteLine("\nAlaris Lean Process completed successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error running Alaris engine:");
                throw;
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Find the build directory by searching current directory and parent directories
        /// </summary>
        private static string? FindBuildDirectory(string startDirectory)
        {
            string currentDir = startDirectory;
            
            // Search up to 5 levels up
            for (int i = 0; i < 5; i++)
            {
                // Check for build directory in current directory
                string buildDir = Path.Combine(currentDir, "build");
                if (Directory.Exists(buildDir))
                {
                    // Verify it's a valid build directory by checking for data subdirectory
                    string dataDir = Path.Combine(buildDir, "data");
                    if (Directory.Exists(dataDir))
                    {
                        return buildDir;
                    }
                }
                
                // Move up one directory
                string parentDir = Directory.GetParent(currentDir)?.FullName;
                if (parentDir == null || parentDir == currentDir)
                {
                    break; // Reached root
                }
                currentDir = parentDir;
            }
            
            return null;
        }
    }
}