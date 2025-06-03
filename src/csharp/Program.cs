// src/csharp/Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using QuantConnect.Configuration;
using QuantConnect.Util; // For Composer and WorkerThread
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Packets;
using QuantConnect.AlgorithmFactory;
using Alaris.Algorithm;
using System.CommandLine;
using System.CommandLine.Invocation;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.Storage;
using QuantConnect.Lean.Engine.HistoryProvider;
using QuantConnect.Data.Auxiliary;

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

                        // Create the algorithm job packet
                        AlgorithmNodePacket job;
                        if (mode == "backtest")
                        {
                            job = new BacktestNodePacket
                            {
                                Type = PacketType.BacktestNode,
                                Algorithm = System.Text.Encoding.UTF8.GetBytes(typeof(ArbitrageAlgorithm).AssemblyQualifiedName ?? ""),
                                Channel = "",
                                UserId = 1,
                                ProjectId = 1,
                                CompileId = "",
                                Version = "1.0.0",
                                Language = QuantConnect.Language.CSharp,
                                BacktestId = Guid.NewGuid().ToString()
                            };

                            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                            {
                                if (DateTime.TryParse(startDate, out DateTime start) && DateTime.TryParse(endDate, out DateTime end))
                                {
                                    var backtestJob = (BacktestNodePacket)job;
                                    backtestJob.PeriodStart = start;
                                    backtestJob.PeriodFinish = end;
                                }
                                else
                                {
                                    Console.WriteLine($"Warning: Could not parse dates. Using default date range.");
                                }
                            }

                            Console.WriteLine($"Starting backtest for {symbol ?? "default portfolio"} from {startDate} to {endDate}");

                            try
                            {
                                // Use standard Lean engine initialization but with corrected configuration
                                var composer = QuantConnect.Util.Composer.Instance;
                                Console.WriteLine("Composer instance obtained");
                                
                                // Try to get system handlers using FromConfiguration
                                Console.WriteLine("Creating system handlers...");
                                var systemHandlers = QuantConnect.Lean.Engine.LeanEngineSystemHandlers.FromConfiguration(composer);
                                Console.WriteLine("System handlers created successfully");
                                
                                Console.WriteLine("Creating algorithm handlers...");
                                var algorithmHandlers = QuantConnect.Lean.Engine.LeanEngineAlgorithmHandlers.FromConfiguration(composer);
                                Console.WriteLine("Algorithm handlers created successfully");

                                // Initialize the engine
                                var engine = new QuantConnect.Lean.Engine.Engine(systemHandlers, algorithmHandlers, false);

                            // Get the path to the algorithm assembly (usually the current assembly)
                            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            Console.WriteLine($"Algorithm assembly path: {assemblyPath}");
                            
                            // Use the default worker thread
                            var workerThread = QuantConnect.Util.WorkerThread.Instance;
                            
                            // Create the algorithm manager
                            var algorithmManager = new QuantConnect.Lean.Engine.AlgorithmManager(false, job);
                            
                            // Initialize the Lean manager
                            systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, job, algorithmManager);

                            Console.WriteLine("Starting Lean Engine...");
                            
                            // Run the backtest using the correct Lean Engine signature
                            engine.Run(job, algorithmManager, assemblyPath, workerThread);

                            Console.WriteLine("Backtest completed.");

                            // Cleanup
                            systemHandlers.Dispose();
                            algorithmHandlers.Dispose();
                        }
                        }
                        else
                        {
                            job = new LiveNodePacket
                            {
                                Type = PacketType.LiveNode,
                                Algorithm = System.Text.Encoding.UTF8.GetBytes(typeof(ArbitrageAlgorithm).AssemblyQualifiedName ?? ""),
                                Channel = "",
                                UserId = 1,
                                ProjectId = 1,
                                DeployId = "",
                                CompileId = "",
                                Version = "1.0.0",
                                Language = QuantConnect.Language.CSharp
                            };

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
                        Console.WriteLine("4. Check log files in the results directory for more details");
                        
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

                // Set up basic configuration with absolute paths
                Config.Set("data-directory", dataDir);
                Config.Set("cache-location", cacheDir);
                Config.Set("results-destination-folder", resultsDir);
                
                // Core Lean configuration
                Config.Set("environment", "backtesting");
                Config.Set("algorithm-type-name", "Alaris.Algorithm.ArbitrageAlgorithm");
                Config.Set("algorithm-language", "CSharp");
                
                // Configure trading mode
                Config.Set("live-mode", mode == "live" ? "true" : "false");
                
                // Configure data frequency
                Config.Set("data-resolution", frequency);
                
                // Configure logging
                Config.Set("log-handler", "QuantConnect.Logging.CompositeLogHandler");
                Config.Set("job-user-id", "1");
                Config.Set("api-access-token", "");
                Config.Set("job-project-id", "1");
                Config.Set("job-organization-id", "");
                
                // Set debug configuration
                if (debug)
                {
                    Config.Set("debug-mode", "true");
                    Config.Set("log-level", "Debug");
                }
                else
                {
                    Config.Set("debug-mode", "false");
                    Config.Set("log-level", "Trace"); // Use Trace for detailed output
                }

                // Backtest specific configuration
                if (mode == "backtest")
                {
                    // Core handlers for backtesting
                    Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed");
                    Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");
                    Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.ConsoleSetupHandler");
                    Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler");
                    Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler");
                    Config.Set("history-provider", "QuantConnect.Lean.Engine.HistoryProvider.SubscriptionDataReaderHistoryProvider");
                    
                    // Data provider settings
                    Config.Set("data-provider", "QuantConnect.Lean.Engine.DataFeeds.DefaultDataProvider");
                    Config.Set("map-file-provider", "QuantConnect.Lean.Engine.DataFeeds.LocalDiskMapFileProvider");
                    Config.Set("factor-file-provider", "QuantConnect.Lean.Engine.DataFeeds.LocalDiskFactorFileProvider");
                    Config.Set("data-permission-manager", "QuantConnect.Data.Auxiliary.DataPermissionManager");
                    
                    // Object store and caching
                    Config.Set("object-store", "QuantConnect.Lean.Engine.Storage.LocalObjectStore");
                    Config.Set("data-cache-provider", "QuantConnect.Lean.Engine.DataFeeds.SingleEntryDataCacheProvider");
                    
                    // Remove problematic lean-manager-type - let MEF auto-discover
                    
                    // Algorithm settings
                    Config.Set("algorithm-location", "QuantConnect.Algorithm.CSharp.dll");
                    
                    // Market settings
                    Config.Set("force-exchange-always-open", "true");
                    Config.Set("show-missing-data-logs", "false");
                    
                    // Performance settings
                    Config.Set("enable-automatic-indicator-warm-up", "false");
                    Config.Set("regression-update-statistics", "false");
                }
                else if (mode == "live" || mode == "paper")
                {
                    // Configure for live/paper trading with Interactive Brokers
                    Config.Set("live-mode-brokerage", "InteractiveBrokersBrokerage");
                    Config.Set("data-feed-handler", "InteractiveBrokersBrokerage");
                    
                    // Interactive Brokers configuration
                    Config.Set("ib-host", "127.0.0.1");
                    Config.Set("ib-port", "4001"); // TWS/IB Gateway port
                    Config.Set("ib-account", "DU123456");
                    Config.Set("ib-user-name", "");
                    Config.Set("ib-password", "");
                    Config.Set("ib-agent-description", "Individual");
                }
                
                // Risk management and performance optimization
                Config.Set("maximum-data-points-per-chart-series", "1000000");
                Config.Set("maximum-chart-series", "30");
                Config.Set("maximum-runtime-minutes", "0"); // No timeout
                Config.Set("maximum-orders", "0"); // No limit
                
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
                Console.WriteLine($"Log Level: {Config.Get("log-level")}");
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