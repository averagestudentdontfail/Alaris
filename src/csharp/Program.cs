// src/csharp/Program.cs - Simplified version using standard Lean launcher
using System;
using System.IO;
using System.Threading.Tasks;
using QuantConnect.Configuration;
using System.CommandLine;
using System.Text.Json;
using System.Linq;
// Add this using statement for the Lean Engine
using QuantConnect.Lean.Engine;


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
                Console.WriteLine($"Fatal error in Alaris Lean Process: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return 1;
            }
        }

        private static async Task RunAlaris(string symbol, string mode, string strategy, 
                                          string? startDate, string? endDate, string frequency, bool debug)
        {
            try
            {
                // Validate and setup
                ValidateAndSetupDirectories(); //
                ValidateParameters(mode, strategy, frequency, startDate, endDate); //
                
                // Display configuration
                Console.WriteLine($"Starting Alaris with configuration:");
                Console.WriteLine($"Symbol: {symbol}");
                Console.WriteLine($"Mode: {mode}");
                Console.WriteLine($"Strategy: {strategy}");
                Console.WriteLine($"Frequency: {frequency}");
                Console.WriteLine($"Debug: {debug}");
                if (mode == "backtest")
                {
                    Console.WriteLine($"Start date: {startDate}");
                    Console.WriteLine($"End date: {endDate}");
                }
                Console.WriteLine();

                // Set environment variables for algorithm access
                SetEnvironmentVariables(symbol, mode, strategy, startDate, endDate, frequency, debug); //

                // Load and customize lean.json configuration
                await CustomizeLeanConfiguration(mode, debug, startDate, endDate); //

                // Use standard Lean launcher
                Console.WriteLine("Starting Lean Engine using standard launcher...");

                // Launch Lean as a subprocess using the lean.json config
                var leanExe = "dotnet";
                var leanDll = "QuantConnect.Lean.Launcher.dll";
                var leanPath = Path.Combine("external", "lean", "Launcher", "bin", "Debug", "net6.0", leanDll);

                if (!File.Exists(leanPath))
                {
                    Console.WriteLine($"Lean launcher not found at {leanPath}");
                    throw new FileNotFoundException($"Lean launcher not found: {leanPath}");
                }

                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = leanExe;
                process.StartInfo.Arguments = $"\"{leanPath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                Console.WriteLine("Alaris Lean Process completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running Alaris: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                PrintTroubleshootingTips(); //
                throw;
            }
        }

        private static void ValidateAndSetupDirectories() //
        {
            var baseDir = Directory.GetCurrentDirectory();
            var dataDir = Path.Combine(baseDir, "data");
            var resultsDir = Path.Combine(baseDir, "results");
            var cacheDir = Path.Combine(baseDir, "cache");

            // Create directories if they don't exist
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(resultsDir);
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(Path.Combine(dataDir, "market-hours")); //
            Directory.CreateDirectory(Path.Combine(dataDir, "symbol-properties")); //
            Directory.CreateDirectory(Path.Combine(dataDir, "equity", "usa", "map_files")); //
            Directory.CreateDirectory(Path.Combine(dataDir, "equity", "usa", "factor_files")); //

            Console.WriteLine($"Base directory: {baseDir}");
            Console.WriteLine($"Data directory: {dataDir}");
            Console.WriteLine($"Results directory: {resultsDir}");
            Console.WriteLine($"Cache directory: {cacheDir}");

            // Verify required files exist
            var marketHoursFile = Path.Combine(dataDir, "market-hours", "market-hours-database.json"); //
            var symbolPropsFile = Path.Combine(dataDir, "symbol-properties", "symbol-properties-database.csv"); //
            
            if (!File.Exists(marketHoursFile))
            {
                Console.WriteLine($"✗ Market hours database not found at {marketHoursFile}");
                Console.WriteLine("Run: scripts/setup.sh to download required files");
            }
            else
            {
                Console.WriteLine($"✓ Market hours database found");
            }
            
            if (!File.Exists(symbolPropsFile))
            {
                Console.WriteLine($"✗ Symbol properties database not found at {symbolPropsFile}");
                Console.WriteLine("Run: scripts/setup.sh to download required files");
            }
            else
            {
                Console.WriteLine($"✓ Symbol properties database found");
            }

            // Check for lean.json
            var leanConfigFile = Path.Combine(baseDir, "lean.json"); //
            if (!File.Exists(leanConfigFile))
            {
                Console.WriteLine($"✗ lean.json configuration file not found at {leanConfigFile}");
                Console.WriteLine("Create lean.json file with QuantConnect Lean configuration");
                throw new FileNotFoundException($"Required configuration file not found: {leanConfigFile}");
            }
            else
            {
                Console.WriteLine($"✓ lean.json configuration found");
            }
        }

        private static void ValidateParameters(string mode, string strategy, string frequency, 
                                             string? startDate, string? endDate) //
        {
            // Validate mode
            if (Array.IndexOf(new[] { "live", "paper", "backtest" }, mode.ToLower()) < 0) //
            {
                throw new ArgumentException($"Invalid mode: {mode}. Must be live, paper, or backtest");
            }

            // Validate strategy
            if (Array.IndexOf(new[] { "deltaneutral", "gammascalping", "volatilitytiming", "relativevalue" }, strategy.ToLower()) < 0) //
            {
                throw new ArgumentException($"Invalid strategy: {strategy}");
            }

            // Validate frequency
            if (Array.IndexOf(new[] { "minute", "hour", "daily" }, frequency.ToLower()) < 0) //
            {
                throw new ArgumentException($"Invalid frequency: {frequency}");
            }

            // Validate dates for backtest
            if (mode.ToLower() == "backtest") //
            {
                if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
                {
                    throw new ArgumentException("Start date and end date are required for backtest mode");
                }

                if (!DateTime.TryParse(startDate, out _) || !DateTime.TryParse(endDate, out _))
                {
                    throw new ArgumentException("Invalid date format. Use YYYY-MM-DD");
                }
            }
        }

        private static void SetEnvironmentVariables(string symbol, string mode, string strategy,
                                                   string? startDate, string? endDate, string frequency, bool debug) //
        {
            Environment.SetEnvironmentVariable("ALARIS_SYMBOL", symbol); //
            Environment.SetEnvironmentVariable("ALARIS_MODE", mode.ToLower()); //
            Environment.SetEnvironmentVariable("ALARIS_STRATEGY", strategy.ToLower()); //
            Environment.SetEnvironmentVariable("ALARIS_FREQUENCY", frequency.ToLower()); //
            Environment.SetEnvironmentVariable("ALARIS_DEBUG", debug.ToString()); //
            
            if (!string.IsNullOrEmpty(startDate))
                Environment.SetEnvironmentVariable("ALARIS_START_DATE", startDate); //
            if (!string.IsNullOrEmpty(endDate))
                Environment.SetEnvironmentVariable("ALARIS_END_DATE", endDate); //
        }

        private static async Task CustomizeLeanConfiguration(string mode, bool debug, 
                                                           string? startDate, string? endDate) //
        {
            var leanConfigPath = "lean.json"; //
            
            if (!File.Exists(leanConfigPath))
            {
                throw new FileNotFoundException($"lean.json not found at {leanConfigPath}");
            }

            // Read the base configuration
            var configJson = await File.ReadAllTextAsync(leanConfigPath);
            var config = JsonDocument.Parse(configJson);
            
            // Create runtime configuration overrides using QuantConnect Config
            // These will override the lean.json settings at runtime
            
            // Set environment-specific settings
            if (mode.ToLower() == "backtest") //
            {
                Config.Set("environment", "backtesting"); //
                Config.Set("live-mode", "false"); //
                
                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
                {
                    Config.Set("start-date", start.ToString("yyyy-MM-dd")); //
                }
                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
                {
                    Config.Set("end-date", end.ToString("yyyy-MM-dd")); //
                }
            }
            else
            {
                Config.Set("environment", "live-trading"); //
                Config.Set("live-mode", "true"); //
            }

            // Set debug configuration
            Config.Set("debug-mode", debug.ToString().ToLower()); //
            Config.Set("log-level", debug ? "Debug" : "Trace"); //

            // Ensure algorithm type is set
            Config.Set("algorithm-type-name", "Alaris.Algorithm.ArbitrageAlgorithm"); //
            Config.Set("algorithm-language", "CSharp"); //
            
            Console.WriteLine("Lean configuration completed");
        }

        private static void PrintTroubleshootingTips() //
        {
            Console.WriteLine();
            Console.WriteLine("Troubleshooting:");
            Console.WriteLine("1. Ensure lean.json exists in the project root");
            Console.WriteLine("2. Run scripts/setup.sh to download required data files");
            Console.WriteLine("3. Check that results directory has write permissions");
            Console.WriteLine("4. Verify algorithm assembly was built successfully");
            Console.WriteLine("5. For QuantLib integration, ensure quantlib-process is running");
            Console.WriteLine("6. Check log files in the results directory for more details");
        }
    }
}