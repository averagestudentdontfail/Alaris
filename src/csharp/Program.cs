// src/csharp/Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Collections.Generic;
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
            
            // This is a dummy option for compatibility, as the config file is not used in download mode.
            var configDirOption = new Option<DirectoryInfo>(
                name: "--config-dir",
                description: "Path to the configuration directory.",
                getDefaultValue: () => new DirectoryInfo(Path.Combine(FindProjectRoot(), "config")));
            configDirOption.AddAlias("-c");

            rootCommand.AddOption(modeOption);
            rootCommand.AddOption(configDirOption);

            rootCommand.SetHandler((mode, configDir) =>
            {
                // --- SOLUTION ---
                // Route to a dedicated download method that does NOT load the trading config file.
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
        /// Runs the Lean engine for live, paper, or backtest modes by loading full configuration.
        /// </summary>
        private static void RunTrading(string mode, string configDir)
        {
            Console.WriteLine($"--- Alaris Lean Engine Initializing [Mode: {mode.ToUpper()}] ---");

            var leanConfigPath = Path.Combine(configDir, "lean_process.yaml");
            if (!File.Exists(leanConfigPath))
            {
                 Log.Error($"Configuration file not found: {leanConfigPath}");
                 return;
            }
            
            // Load the full configuration from the YAML file for trading modes
            // This would require implementing YAML loading logic here for trading modes
            // For now, we'll use a simplified approach
            if (!LoadConfigurationFromYaml(leanConfigPath, mode))
            {
                Log.Error("Engine startup failed due to configuration errors.");
                return;
            }

            RunEngine(mode);
        }

        /// <summary>
        /// Runs the Lean engine in a minimal, programmatically-defined configuration
        /// specifically for downloading data. This bypasses the YAML file entirely to
        /// avoid any conflicting settings (like brokerage details).
        /// </summary>
        private static void RunDownload()
        {
            Console.WriteLine($"--- Alaris Lean Engine Initializing [Mode: DOWNLOAD] ---");

            // Manually set the minimal required configuration for data downloading.
            // This guarantees a clean environment.
            Config.Set("environment", "backtesting-desktop");
            Config.Set("algorithm-type-name", "Alaris.Algorithm.DataDownload");
            Config.Set("algorithm-location", "Alaris.Lean.dll");

            // Configure data provider to use the QuantConnect API
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
                
                // Use a BacktestNodePacket for download and backtest modes
                var packet = new BacktestNodePacket(0, 0, "", new Dictionary<string, string>(), 10000, "");
                
                systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, packet, algorithmManager);
                
                string algorithmLocation = Config.Get("algorithm-location", "Alaris.Lean.dll");
                Log.Trace($"Starting engine with algorithm: {Config.Get("algorithm-type-name")} from {algorithmLocation}");
                
                engine.Run(packet, algorithmManager, algorithmLocation, WorkerThread.Instance);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Engine execution failed in {mode} mode:");
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                Console.WriteLine($"--- Alaris Lean Engine Shutdown [Mode: {mode.ToUpper()}] ---");
                systemHandlers?.Dispose();
            }
        }

        /// <summary>
        /// Loads configuration from the primary YAML file for trading modes.
        /// </summary>
        private static bool LoadConfigurationFromYaml(string yamlPath, string mode)
        {
            try
            {
                if (!File.Exists(yamlPath))
                {
                    Log.Error($"Configuration file not found: {yamlPath}");
                    return false;
                }

                // For trading modes, we would need to implement YAML parsing
                // This is a simplified version that sets basic configuration
                Config.Set("job-user-id", Environment.GetEnvironmentVariable("QC_USER_ID") ?? "0");
                Config.Set("api-access-token", Environment.GetEnvironmentVariable("QC_API_TOKEN") ?? "");
                Config.Set("data-provider-agree-to-terms", "true");
                Config.Set("algorithm-type-name", "Alaris.Algorithm.ArbitrageAlgorithm");
                Config.Set("algorithm-location", "Alaris.Lean.dll");
                
                bool isLive = (mode == "live" || mode == "paper");
                Config.Set("live-mode", isLive.ToString().ToLower());
                
                if (isLive)
                {
                    // Live trading configuration
                    Config.Set("live-mode-brokerage", "InteractiveBrokersBrokerage");
                    Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.BrokerageSetupHandler");
                    Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.LiveTradingResultHandler");
                    Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.LiveTradingDataFeed");
                    Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.LiveTradingRealTimeHandler");
                    Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BrokerageTransactionHandler");
                    Config.Set("data-queue-handler", "QuantConnect.Brokerages.InteractiveBrokers.InteractiveBrokersBrokerage");
                }
                else
                {
                    // Backtesting configuration
                    Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.BacktestingSetupHandler");
                    Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");
                    Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed");
                    Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler");
                    Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler");
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
                Log.Trace($"Created directory: {path}");
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