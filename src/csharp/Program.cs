// src/csharp/Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using YamlDotNet.Serialization;
using QuantConnect;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Interfaces;

namespace Alaris
{
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

            rootCommand.SetHandler((mode, configDir) =>
            {
                // --- SOLUTION ---
                // Route to a dedicated download method to avoid dependency conflicts.
                if (mode.Equals("download", StringComparison.OrdinalIgnoreCase))
                {
                    RunDownload();
                }
                else
                {
                    RunTrading(mode, configDir.FullName);
                }
            }, modeOption, configDirOption);

            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// Runs the Lean engine for live or paper trading, loading full configuration.
        /// </summary>
        private static void RunTrading(string mode, string configDir)
        {
            Console.WriteLine($"--- Alaris Lean Engine Initializing [Mode: {mode.ToUpper()}] ---");

            var leanConfigPath = Path.Combine(configDir, "lean_process.yaml");

            if (!LoadConfigurationFromFiles(leanConfigPath, mode))
            {
                Log.Error("Engine startup failed due to configuration errors.");
                return;
            }

            RunEngine(mode);
        }

        /// <summary>
        /// Runs the Lean engine in a minimal configuration specifically for downloading data.
        /// This bypasses YAML parsing to avoid any potential dependency conflicts.
        /// </summary>
        private static void RunDownload()
        {
            Console.WriteLine($"--- Alaris Lean Engine Initializing [Mode: DOWNLOAD] ---");

            // Manually set the minimal required configuration for data downloading.
            Config.Set("environment", "lean-cli");
            Config.Set("algorithm-type-name", "Alaris.Algorithm.DataDownload");
            Config.Set("algorithm-location", "Alaris.Lean.dll");
            Config.Set("data-provider", "QuantConnect.Lean.Engine.DataFeeds.ApiDataProvider");
            Config.Set("job-user-id", Environment.GetEnvironmentVariable("QC_USER_ID") ?? "0");
            Config.Set("api-access-token", Environment.GetEnvironmentVariable("QC_API_TOKEN") ?? "");
            Config.Set("data-provider-agree-to-terms", "true");
            
            // Set required handlers for a backtesting/download environment
            Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.BacktestingSetupHandler");
            Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");
            Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed");
            Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler");
            Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler");
            Config.Set("map-file-provider", "QuantConnect.Data.Auxiliary.LocalZipMapFileProvider");
            Config.Set("factor-file-provider", "QuantConnect.Data.Auxiliary.LocalZipFactorFileProvider");

            RunEngine("download");
        }

        /// <summary>
        /// The core method to set up and run the Lean engine.
        /// </summary>
        private static void RunEngine(string mode)
        {
            // Set up data directories
            string? buildDirectory = FindBuildDirectory(Directory.GetCurrentDirectory());
            if (buildDirectory == null)
            {
                Log.Error("Could not find the 'build' directory. Please run from the project root or build directory.");
                return;
            }

            EnsureDirectoryExists(Path.Combine(buildDirectory, "data"));
            EnsureDirectoryExists(Path.Combine(buildDirectory, "cache"));
            EnsureDirectoryExists(Path.Combine(buildDirectory, "results"));

            Config.Set("data-folder", Path.Combine(buildDirectory, "data"));
            Config.Set("cache-location", Path.Combine(buildDirectory, "cache"));
            Config.Set("results-destination-folder", Path.Combine(buildDirectory, "results"));

            Log.Trace($"Data folder: {Config.Get("data-folder")}");

            LeanEngineSystemHandlers? systemHandlers = null;
            try
            {
                Log.Trace("Initializing Lean Engine system handlers...");
                systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
                systemHandlers.Initialize();

                Log.Trace("Initializing Lean Engine algorithm handlers...");
                var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
                
                var isLiveMode = Config.GetBool("live-mode");
                var engine = new Engine(systemHandlers, algorithmHandlers, isLiveMode);
                
                var algorithmManager = new AlgorithmManager(isLiveMode);
                
                var packet = isLiveMode 
                    ? new LiveNodePacket() as AlgorithmNodePacket
                    : new BacktestNodePacket() as AlgorithmNodePacket;

                systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, packet, algorithmManager);
                
                string algorithmLocation = Config.Get("algorithm-location");
                Log.Trace($"Starting engine with algorithm: {Config.Get("algorithm-type-name")} from {algorithmLocation}");
                
                engine.Run(packet, algorithmManager, algorithmLocation, WorkerThread.Instance);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Engine execution failed in {mode} mode:");
            }
            finally
            {
                Console.WriteLine($"--- Alaris Lean Engine Shutdown [Mode: {mode.ToUpper()}] ---");
                systemHandlers?.Dispose();
            }
        }
        
        private static bool LoadConfigurationFromFiles(string yamlPath, string mode)
        {
            try
            {
                if (!File.Exists(yamlPath))
                {
                    Log.Error($"Configuration file not found: {yamlPath}");
                    return false;
                }

                var deserializer = new DeserializerBuilder().Build();
                var yamlText = File.ReadAllText(yamlPath);
                var yamlConfig = deserializer.Deserialize<dynamic>(yamlText);

                var algoConfig = yamlConfig["algorithm"];
                var brokerageConfig = yamlConfig["brokerage"];
                
                var userId = Environment.GetEnvironmentVariable("QC_USER_ID") ?? "0";
                var apiToken = Environment.GetEnvironmentVariable("QC_API_TOKEN") ?? "";

                Config.Set("job-user-id", userId);
                Config.Set("api-access-token", apiToken);
                Config.Set("data-provider-agree-to-terms", "true");
                
                Config.Set("algorithm-type-name", (string)algoConfig["name"]);
                Config.Set("algorithm-location", "Alaris.Lean.dll");
                Config.Set("start-date", (string)algoConfig["start_date"]);
                Config.Set("end-date", (string)algoConfig["end_date"]);
                Config.Set("cash", algoConfig["cash"].ToString());

                Config.Set("ib-account", (string)brokerageConfig["account"]);
                Config.Set("ib-host", (string)brokerageConfig["host"]);
                Config.Set("ib-client-id", brokerageConfig["client_id"].ToString());
                
                string port = mode == "live" 
                    ? brokerageConfig["live_port"].ToString()
                    : brokerageConfig["paper_port"].ToString();
                Config.Set("ib-port", port);

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
                    Config.Set("data-provider", "InteractiveBrokersBrokerage");
                    Config.Set("map-file-provider", "LocalDiskMapFileProvider");
                    Config.Set("factor-file-provider", "LocalDiskFactorFileProvider");
                }
                else
                {
                    Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.BacktestingSetupHandler");
                    Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");
                    Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed");
                    Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler");
                    Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler");
                    Config.Set("data-provider", "QuantConnect.Lean.Engine.DataFeeds.ApiDataProvider");
                    Config.Set("map-file-provider", "QuantConnect.Data.Auxiliary.LocalZipMapFileProvider");
                    Config.Set("factor-file-provider", "QuantConnect.Data.Auxiliary.LocalZipFactorFileProvider");
                }
                
                Log.Trace("✓ Configuration loaded successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to load configuration from {yamlPath}:");
                return false;
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        
        private static string FindProjectRoot()
        {
            string currentDir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(currentDir))
            {
                if (Directory.Exists(Path.Combine(currentDir, "config")) && 
                    Directory.Exists(Path.Combine(currentDir, "src")))
                {
                    return currentDir;
                }
                var parent = Directory.GetParent(currentDir);
                if (parent == null) break;
                currentDir = parent.FullName;
            }
            return Directory.GetCurrentDirectory();
        }

        private static string? FindBuildDirectory(string startDirectory)
        {
            string currentDir = startDirectory;
            for (int i = 0; i < 5; i++)
            {
                if (Path.GetFileName(currentDir).Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                    return currentDir;
                }
                
                string buildDir = Path.Combine(currentDir, "build");
                if (Directory.Exists(buildDir))
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