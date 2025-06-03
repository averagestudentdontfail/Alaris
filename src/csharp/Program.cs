// src/csharp/Program.cs
using System;
using System.Threading.Tasks;
using QuantConnect.Configuration;
using QuantConnect.Util;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Packets;
using QuantConnect.AlgorithmFactory;
using Alaris.Algorithm;
using System.CommandLine;
using System.CommandLine.Invocation;

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
                    "Trading mode: live, paper, or backtest");
                modeOption.AddAlias("-m");
                
                var strategyOption = new Option<string>(
                    "--strategy",
                    "Strategy mode: DeltaNeutral, GammaScalping, VolatilityTiming, or RelativeValue");
                strategyOption.AddAlias("-t");
                
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
                    var mode = context.ParseResult.GetValueForOption(modeOption)?.ToLower() ?? "live";
                    var strategy = context.ParseResult.GetValueForOption(strategyOption)?.ToLower() ?? "deltaneutral";
                    var startDate = context.ParseResult.GetValueForOption(startDateOption);
                    var endDate = context.ParseResult.GetValueForOption(endDateOption);
                    var frequency = context.ParseResult.GetValueForOption(frequencyOption)?.ToLower() ?? "minute";
                    var debug = context.ParseResult.GetValueForOption(debugOption);

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
                        }

                        // Store configuration in environment for algorithm access
                        Environment.SetEnvironmentVariable("ALARIS_SYMBOL", symbol);
                        Environment.SetEnvironmentVariable("ALARIS_MODE", mode);
                        Environment.SetEnvironmentVariable("ALARIS_STRATEGY", strategy);

                        Console.WriteLine($"Starting backtest for {symbol} from {startDate} to {endDate}");
                        
                        // Initialize and run the backtest engine
                        var engine = new LeanEngineSystemHandlers();
                        var results = new BacktestingResultHandler();
                        var transactions = new BacktestingTransactionHandler();
                        var realtime = new BacktestingRealTimeHandler();
                        var dataFeed = new FileSystemDataFeed();
                        var setup = new BacktestingSetupHandler();
                        var mapFileProvider = new LocalDiskMapFileProvider();
                        var factorFileProvider = new LocalDiskFactorFileProvider();
                        var dataProvider = new DefaultDataProvider();

                        // Initialize the engine
                        var engineInitialized = engine.Initialize(
                            job,
                            mapFileProvider,
                            factorFileProvider,
                            dataProvider,
                            results,
                            transactions,
                            realtime,
                            dataFeed,
                            setup,
                            null,
                            null
                        );

                        if (!engineInitialized)
                        {
                            Console.WriteLine("Failed to initialize backtest engine");
                            return 1;
                        }

                        // Run the backtest
                        var algorithmManager = new AlgorithmManager(false);
                        var status = await algorithmManager.Run(job, engine, results, transactions, realtime, dataFeed, setup);
                        
                        if (status == AlgorithmStatus.Running)
                        {
                            Console.WriteLine("Backtest completed successfully");
                        }
                        else
                        {
                            Console.WriteLine($"Backtest failed with status: {status}");
                        }

                        // Cleanup
                        engine.Dispose();
                        results.Dispose();
                        transactions.Dispose();
                        realtime.Dispose();
                        dataFeed.Dispose();
                        setup.Dispose();
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

                        // Store configuration in environment for algorithm access
                        Environment.SetEnvironmentVariable("ALARIS_SYMBOL", symbol);
                        Environment.SetEnvironmentVariable("ALARIS_MODE", mode);
                        Environment.SetEnvironmentVariable("ALARIS_STRATEGY", strategy);

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
            // Set up basic configuration
            Config.Set("data-directory", "./Data");
            Config.Set("cache-location", "./Cache");
            Config.Set("results-destination-folder", "./Results");
            
            // Configure trading mode
            Config.Set("live-mode", mode == "live" ? "true" : "false");
            Config.Set("paper-trading", mode == "paper" ? "true" : "false");
            
            // Configure data frequency
            Config.Set("data-resolution", frequency);
            
            // Configure logging - always enable console logging
            Config.Set("log-handler", "QuantConnect.Logging.CompositeLogHandler");
            Config.Set("log-level", debug ? "Debug" : "Info");
            Config.Set("debug-mode", debug ? "true" : "false");
            
            // Force console output
            Config.Set("console-write", "true");
            Config.Set("console-write-level", debug ? "Debug" : "Info");
            
            // Add more detailed logging
            Console.WriteLine($"Starting Alaris algorithm with configuration:");
            Console.WriteLine($"Mode: {mode}");
            Console.WriteLine($"Symbol: {symbol}");
            Console.WriteLine($"Strategy: {strategy}");
            Console.WriteLine($"Frequency: {frequency}");
            Console.WriteLine($"Debug Mode: {debug}");
            Console.WriteLine($"Start Date: {startDate}");
            Console.WriteLine($"End Date: {endDate}");
            Console.WriteLine($"Data Directory: {Config.Get("data-directory")}");
            Console.WriteLine($"Results Directory: {Config.Get("results-destination-folder")}");
            Console.WriteLine($"Log Level: {Config.Get("log-level")}");
            
            if (mode == "live" || mode == "paper")
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
            
            // Risk management
            Config.Set("maximum-data-points-per-chart-series", "1000000");
            Config.Set("force-exchange-always-open", "false");
            
            // Performance optimization
            Config.Set("show-missing-data-logs", "false");
            Config.Set("maximum-chart-series", "30");
            
            // Store custom configuration
            if (!string.IsNullOrEmpty(symbol))
            {
                Config.Set("alaris-trading-symbol", symbol);
            }
            Config.Set("alaris-strategy-mode", strategy);
            
            Console.WriteLine("Lean configuration completed");
        }
    }
}