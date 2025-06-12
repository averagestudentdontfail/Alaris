// src/csharp/Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Brokerages.InteractiveBrokers;
using QuantConnect.ToolBox;

namespace Alaris
{
    /// <summary>
    /// Main entry point for the Alaris C# Lean process.
    /// This application is responsible for running the trading algorithm in various modes
    /// (live, paper, backtest, data download) by dynamically configuring the Lean engine
    /// based on master YAML/JSON configuration files.
    /// </summary>
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Alaris Trading System - Lean Integration Engine");

            var modeOption = new Option<string>(
                name: "--mode",
                description: "The operational mode for the Lean engine.",
                getDefaultValue: () => "backtest");
            modeOption.AddAlias("-m");
            modeOption.FromAmong("live", "paper", "backtest", "download");

            var configDirOption = new Option<DirectoryInfo>(
                name: "--config-dir",
                description: "Path to the configuration directory containing lean_process.yaml.",
                getDefaultValue: () => new DirectoryInfo(Path.Combine(FindProjectRoot(), "config")));
            configDirOption.AddAlias("-c");

            rootCommand.AddOption(modeOption);
            rootCommand.AddOption(configDirOption);

            rootCommand.SetHandler(async (mode, configDir) =>
            {
                await RunEngine(mode, configDir.FullName);
            }, modeOption, configDirOption);

            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// Configures and runs the Lean engine based on the selected operational mode.
        /// </summary>
        private static async Task RunEngine(string mode, string configDir)
        {
            Console.WriteLine($"--- Alaris Lean Engine Initializing [Mode: {mode.ToUpper()}] ---");

            // 1. Load Master Configuration from YAML/JSON
            var leanConfigPath = Path.Combine(configDir, "lean_process.yaml");
            var algoConfigPath = Path.Combine(configDir, "algorithm.json");

            if (!LoadConfigurationFromFiles(leanConfigPath, algoConfigPath, mode))
            {
                Log.Error("Engine startup failed due to configuration errors.");
                return;
            }

            // 2. Set Essential Paths
            string? buildDirectory = FindBuildDirectory(Directory.GetCurrentDirectory());
            if (buildDirectory == null)
            {
                Log.Error("Could not find the 'build' directory. Please run from the project root or build directory.");
                return;
            }
            Config.Set("data-folder", Path.Combine(buildDirectory, "data"));
            Config.Set("cache-location", Path.Combine(buildDirectory, "cache"));
            Config.Set("results-destination-folder", Path.Combine(buildDirectory, "results"));
            Log.Trace($"Data folder set to: {Config.Get("data-folder")}");

            // 3. Execute the requested mode
            if (mode == "download")
            {
                DownloadHistoricalData();
            }
            else
            {
                // Run the full Lean engine for backtesting or live/paper trading
                try
                {
                    var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
                    systemHandlers.Initialize();

                    var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
                    var engine = new Engine(systemHandlers, algorithmHandlers, Config.GetBool("live-mode"));
                    
                    var algorithmManager = new AlgorithmManager(Config.GetBool("live-mode"));
                    systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, new BacktestNodePacket(), algorithmManager);
                    
                    string algorithmPath = Config.Get("algorithm-location");
                    engine.Run(new BacktestNodePacket(), algorithmManager, algorithmPath, WorkerThread.Instance);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Engine run failed in {mode} mode:");
                }
                finally
                {
                    Console.WriteLine($"--- Alaris Lean Engine Shutdown [Mode: {mode.ToUpper()}] ---");
                    Engine.Main(new string[]{}); // Perform cleanup
                }
            }
        }
        
        /// <summary>
        /// Loads settings from lean_process.yaml and algorithm.json and populates
        /// the static QuantConnect.Configuration.Config class.
        /// </summary>
        private static bool LoadConfigurationFromFiles(string yamlPath, string jsonPath, string mode)
        {
            try
            {
                // --- Load lean_process.yaml ---
                var deserializer = new DeserializerBuilder().Build();
                var yamlText = File.ReadAllText(yamlPath);
                var yamlConfig = deserializer.Deserialize<dynamic>(yamlText);

                var algoConfig = yamlConfig["algorithm"];
                var brokerageConfig = yamlConfig["brokerage"];
                var universeConfig = yamlConfig["universe"];

                // --- Load algorithm.json ---
                var jsonText = File.ReadAllText(jsonPath);
                var jsonConfig = JObject.Parse(jsonText);
                
                // --- Set Core Algorithm and Brokerage Settings ---
                Config.Set("algorithm-type-name", (string)algoConfig["name"]);
                Config.Set("algorithm-location", "Alaris.Algorithm.dll"); // Assume it's in the bin directory
                Config.Set("start-date", (string)algoConfig["start_date"]);
                Config.Set("end-date", (string)algoConfig["end_date"]);
                Config.Set("cash", (string)algoConfig["cash"]);

                // --- Set IBKR Connection Details ---
                Config.Set("ib-account", (string)brokerageConfig["account"]);
                Config.Set("ib-host", (string)brokerageConfig["host"]);
                Config.Set("ib-client-id", (string)brokerageConfig["client_id"]);
                
                string port = mode == "live" 
                    ? (string)brokerageConfig["live_port"] 
                    : (string)brokerageConfig["paper_port"];
                Config.Set("ib-port", port);

                // --- Set Handlers Based on Mode ---
                bool isLive = (mode == "live" || mode == "paper");
                Config.Set("live-mode", isLive.ToString().ToLower());
                
                if (isLive)
                {
                    Config.Set("live-mode-brokerage", "InteractiveBrokersBrokerage");
                    Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.BrokerageSetupHandler");
                    Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.LiveTradingResultHandler");
                    Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.LiveTradingDataFeed");
                    Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.LiveTradingRealTimeHandler");
                    Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BrokerageTransactionHandler");
                    Config.Set("data-queue-handler", "QuantConnect.Brokerages.InteractiveBrokers.InteractiveBrokersBrokerage");
                }
                else // backtest
                {
                    Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.BacktestingSetupHandler");
                    Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");
                    Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed");
                    Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler");
                    Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler");
                }
                
                Log.Trace("Configuration loaded successfully.");
                return true;
            }
            catch(Exception ex)
            {
                Log.Error(ex, $"Failed to load or parse configuration files ({yamlPath}, {jsonPath}).");
                return false;
            }
        }

        /// <summary>
        /// Handles downloading of historical data using the configured downloader.
        /// </summary>
        private static void DownloadHistoricalData()
        {
            var yamlPath = Path.Combine(FindProjectRoot(), "config", "lean_process.yaml");
            var yamlText = File.ReadAllText(yamlPath);
            var deserializer = new DeserializerBuilder().Build();
            var yamlConfig = deserializer.Deserialize<dynamic>(yamlText);

            var symbols = ((List<object>)yamlConfig["universe"]["symbols"]).Select(s => s.ToString()).ToList();
            var resolution = Enum.Parse<Resolution>(Config.Get("resolution", "Daily"), true);
            var fromDate = DateTime.Parse(Config.Get("start-date", "2023-01-01"));
            var toDate = DateTime.Parse(Config.Get("end-date", "2024-12-31"));

            var downloader = new LeanDataDownloader(new InteractiveBrokersBrokerage());
            Console.WriteLine($"Starting download for {symbols.Count} symbols from {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd} at {resolution} resolution.");

            foreach (var symbolStr in symbols)
            {
                if(string.IsNullOrEmpty(symbolStr)) continue;
                var symbol = Symbol.Create(symbolStr, SecurityType.Equity, Market.USA);
                downloader.Download(symbol, resolution, fromDate, toDate);
            }

            Console.WriteLine("--- Data Download Process Completed ---");
        }
        
        /// <summary>
        /// Finds the root directory of the project to locate the config folder.
        /// </summary>
        private static string FindProjectRoot()
        {
            string currentDir = Directory.GetCurrentDirectory();
            while(currentDir != null)
            {
                if (Directory.Exists(Path.Combine(currentDir, "config")) && Directory.Exists(Path.Combine(currentDir, "src")))
                {
                    return currentDir;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
            return Directory.GetCurrentDirectory(); // Fallback
        }

        /// <summary>
        /// Finds the build directory by searching the current directory and its parents.
        /// </summary>
        private static string? FindBuildDirectory(string startDirectory)
        {
            string currentDir = startDirectory;
            for (int i = 0; i < 5; i++)
            {
                if (Path.GetFileName(currentDir).Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                     if(Directory.Exists(Path.Combine(currentDir, "data"))) return currentDir;
                }
                
                string buildDir = Path.Combine(currentDir, "build");
                if (Directory.Exists(buildDir) && Directory.Exists(Path.Combine(buildDir, "data")))
                {
                    return buildDir;
                }
                
                var parent = Directory.GetParent(currentDir);
                if (parent == null) break;
                currentDir = parent.FullName;
            }
            return null;
        }
    }
}

// Action required add the YamlDotNet and Newtonsoft.Json packages to your C# project. You can do this by editing your Alaris.Lean.csproj file or by running the following commands in the src/csharp directory.