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
using QuantConnect.ToolBox;
using QuantConnect.Brokerages.InteractiveBrokers;
using System.Collections.Generic;
using System.Linq;
using QuantConnect;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Lean.Engine.Results;

namespace Alaris
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // --- Command Line Configuration ---
            var rootCommand = new RootCommand("Alaris Trading System - Lean Integration Engine");

            var modeOption = new Option<string>(
                name: "--mode",
                description: "The operational mode for the Lean engine.",
                getDefaultValue: () => "paper");
            modeOption.AddAlias("-m");
            modeOption.FromAmong("live", "paper", "backtest", "download");

            var configOption = new Option<FileInfo>(
                name: "--config",
                description: "Path to the Lean configuration file.",
                getDefaultValue: () => new FileInfo("config.json"));
            configOption.AddAlias("-c");

            rootCommand.AddOption(modeOption);
            rootCommand.AddOption(configOption);

            rootCommand.SetHandler(async (mode, configFile) =>
            {
                await RunEngine(mode, configFile.FullName);
            }, modeOption, configOption);

            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// Main entry point for running the Lean engine in different modes.
        /// </summary>
        private static async Task RunEngine(string mode, string configPath)
        {
            Console.WriteLine($"--- Alaris Lean Engine Initializing [Mode: {mode.ToUpper()}] ---");
            
            // --- Load Configuration ---
            if (!File.Exists(configPath))
            {
                Log.Error($"Configuration file not found: {configPath}");
                return;
            }
            Config.SetConfigurationFile(configPath);
            // Sets the environment from the config file, which determines which handlers to load.
            Config.Set("environment", mode);

            // --- Find and Set Essential Paths ---
            string? buildDirectory = FindBuildDirectory(Directory.GetCurrentDirectory());
            if (buildDirectory == null)
            {
                Log.Error("Could not find the 'build' directory. Please run from the project root.");
                return;
            }
            string dataDirectory = Path.Combine(buildDirectory, "data");
            Config.Set("data-folder", dataDirectory);
            Config.Set("cache-location", Path.Combine(buildDirectory, "cache"));
            Config.Set("results-destination-folder", Path.Combine(buildDirectory, "results"));
            Log.Trace($"Data folder set to: {dataDirectory}");

            if (mode == "download")
            {
                DownloadHistoricalData();
                return;
            }

            // --- Run Lean Engine with dynamically composed handlers ---
            try
            {
                var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
                systemHandlers.Initialize();

                var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
                string assemblyPath = Config.Get("algorithm-location", "Alaris.Algorithm.dll");

                var liveMode = mode.Contains("live") || mode.Contains("paper");
                
                // The AlgorithmManager will use the ISetupHandler defined in the config
                var algorithmManager = new AlgorithmManager(liveMode);
                
                systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, new BacktestNodePacket(), algorithmManager);
                var engine = new Engine(systemHandlers, algorithmHandlers, liveMode);
                
                // Run the engine with the configured handlers
                engine.Run(new BacktestNodePacket(), algorithmManager, assemblyPath, WorkerThread.Instance);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Engine run failed in {mode} mode:");
            }
            finally
            {
                 Console.WriteLine($"--- Alaris Lean Engine Shutdown [Mode: {mode.ToUpper()}] ---");
                 // Ensure proper shutdown of the LeanManager
                 Engine.Main(new string[]{});
            }
        }

        /// <summary>
        /// Handles downloading of historical data using the configured downloader.
        /// </summary>
        private static void DownloadHistoricalData()
        {
            var symbols = Config.Get("symbols").Split(',').ToList();
            var resolution = Enum.Parse<Resolution>(Config.Get("resolution", "Daily"), true);
            var fromDate = DateTime.Parse(Config.Get("start-date", "2023-01-01"));
            var toDate = DateTime.Parse(Config.Get("end-date", "2024-12-31"));

            // This uses the IDataDownloader configured in config.json
            var dataDownloader = Composer.Instance.GetExportedValue<IDataDownloader>();

            Console.WriteLine($"Starting download for {symbols.Count} symbols from {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd} at {resolution} resolution...");

            foreach (var symbolStr in symbols)
            {
                var symbol = Symbol.Create(symbolStr, SecurityType.Equity, Market.USA);
                dataDownloader.Download(symbol, resolution, fromDate, toDate);
            }

            Console.WriteLine("--- Data Download Process Completed ---");
        }

        /// <summary>
        /// Finds the build directory by searching parent directories.
        /// </summary>
        private static string? FindBuildDirectory(string startDirectory)
        {
            string currentDir = startDirectory;
            for (int i = 0; i < 5; i++)
            {
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
