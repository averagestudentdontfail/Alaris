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

                rootCommand.AddOption(symbolOption);
                rootCommand.AddOption(modeOption);
                rootCommand.AddOption(strategyOption);
                rootCommand.AddOption(startDateOption);
                rootCommand.AddOption(endDateOption);

                rootCommand.SetHandler(async (context) =>
                {
                    var symbol = context.ParseResult.GetValueForOption(symbolOption);
                    var mode = context.ParseResult.GetValueForOption(modeOption)?.ToLower() ?? "live";
                    var strategy = context.ParseResult.GetValueForOption(strategyOption)?.ToLower() ?? "deltaneutral";
                    var startDate = context.ParseResult.GetValueForOption(startDateOption);
                    var endDate = context.ParseResult.GetValueForOption(endDateOption);

                    // Configure Lean based on mode
                    ConfigureLean(mode, symbol, strategy, startDate, endDate);

                    // Create the algorithm job packet
                    var job = new LiveNodePacket
                    {
                        Type = mode == "backtest" ? PacketType.BacktestNode : PacketType.LiveNode,
                        Algorithm = System.Text.Encoding.UTF8.GetBytes(typeof(ArbitrageAlgorithm).AssemblyQualifiedName ?? ""),
                        Channel = "",
                        UserId = 1,
                        ProjectId = 1,
                        DeployId = "",
                        CompileId = "",
                        Version = "1.0.0",
                        Language = QuantConnect.Language.CSharp
                    };

                    if (mode == "backtest" && !string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                    {
                        if (DateTime.TryParse(startDate, out DateTime start) && DateTime.TryParse(endDate, out DateTime end))
                        {
                            job.BacktestId = Guid.NewGuid().ToString();
                            job.BacktestStartDate = start;
                            job.BacktestEndDate = end;
                        }
                    }

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

        private static void ConfigureLean(string mode, string? symbol, string strategy, string? startDate, string? endDate)
        {
            // Set up basic configuration
            Config.Set("data-directory", "./Data");
            Config.Set("cache-location", "./Cache");
            Config.Set("results-destination-folder", "./Results");
            
            // Configure trading mode
            Config.Set("live-mode", mode == "live" ? "true" : "false");
            Config.Set("paper-trading", mode == "paper" ? "true" : "false");
            
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