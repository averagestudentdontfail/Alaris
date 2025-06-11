// src/csharp/Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using QuantConnect.Configuration;
using System.CommandLine;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Util;

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
                Console.WriteLine("\nStarting Alaris with configuration:");
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
                
                // Set environment variables for the algorithm to access
                Environment.SetEnvironmentVariable("ALARIS_SYMBOL", symbol);
                Environment.SetEnvironmentVariable("ALARIS_STRATEGY", strategy);

                // Configure Lean using the parsed arguments
                var liveMode = !mode.Equals("backtest", StringComparison.OrdinalIgnoreCase);
                Config.Set("environment", liveMode ? "live-trading" : "backtesting");
                Config.Set("live-mode", liveMode.ToString().ToLower());
                
                // Set the algorithm class and location
                Config.Set("algorithm-type-name", "Alaris.Algorithm.ArbitrageAlgorithm");
                Config.Set("algorithm-location", "Alaris.Lean.dll"); 
                
                // Set data resolution from command line
                Config.Set("resolution", frequency);
                
                // Set backtesting dates if applicable
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
                else // For live/paper trading, set brokerage
                {
                    Config.Set("live-mode-brokerage", "InteractiveBrokersBrokerage");
                }
                
                // Set debug mode
                Config.Set("debug-mode", debug.ToString().ToLower());
                Log.DebuggingEnabled = debug;

                
                Console.WriteLine("Initializing and running Lean engine in-process...");
                
                var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
                systemHandlers.Initialize();

                var engine = new Engine(systemHandlers, Composer.Instance, false);
                engine.Run();

                Console.WriteLine("\nAlaris Lean Process completed successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error running Alaris engine:");
                throw;
            }
            
            return Task.CompletedTask;
        }
    }
}