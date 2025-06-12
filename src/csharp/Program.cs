// src/csharp/Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;
using Newtonsoft.Json.Linq;
using QuantConnect;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Brokerages.InteractiveBrokers;

using QCSymbol = QuantConnect.Symbol; 

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
                RunEngine(mode, configDir.FullName);
            }, modeOption, configDirOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static void RunEngine(string mode, string configDir)
        {
            Console.WriteLine($"--- Alaris Lean Engine Initializing [Mode: {mode.ToUpper()}] ---");

            var leanConfigPath = Path.Combine(configDir, "lean_process.yaml");
            var algoConfigPath = Path.Combine(configDir, "algorithm.json");

            if (!LoadConfigurationFromFiles(leanConfigPath, algoConfigPath, mode))
            {
                Log.Error("Engine startup failed due to configuration errors.");
                return;
            }

            string? buildDirectory = FindBuildDirectory(Directory.GetCurrentDirectory());
            if (buildDirectory == null)
            {
                Log.Error("Could not find the 'build' directory. Please run from the project root or build directory.");
                return;
            }
            Config.Set("data-folder", Path.Combine(buildDirectory, "data"));
            Config.Set("cache-location", Path.Combine(buildDirectory, "cache"));
            Config.Set("results-destination-folder", Path.Combine(buildDirectory, "results"));

            if (mode == "download")
            {
                Log.Trace("Download mode is handled automatically by Lean when data is needed.");
                Log.Trace("Historical data will be downloaded on-demand during algorithm execution.");
                return;
            }

            LeanEngineSystemHandlers? systemHandlers = null;
            try
            {
                systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
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
                // Correctly dispose of system handlers to perform cleanup.
                systemHandlers?.Dispose(); 
            }
        }
        
        private static bool LoadConfigurationFromFiles(string yamlPath, string jsonPath, string mode)
        {
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var yamlText = File.ReadAllText(yamlPath);
                var yamlConfig = deserializer.Deserialize<dynamic>(yamlText);

                var algoConfig = yamlConfig["algorithm"];
                var brokerageConfig = yamlConfig["brokerage"];
                
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
                
                // Define which handlers to use for each mode.
                // This is the core of Lean's modularity.
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
                Log.Error(ex, $"Failed to load or parse configuration files ({yamlPath}).");
                return false;
            }
        }
        
        private static string FindProjectRoot()
        {
            string currentDir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(currentDir))
            {
                if (Directory.Exists(Path.Combine(currentDir, "config")) && Directory.Exists(Path.Combine(currentDir, "src")))
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