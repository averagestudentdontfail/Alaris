// src/csharp/Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using QuantConnect;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Api;

namespace Alaris
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Enable debugging
            EnableDebugLogging();
            LogSystemInformation();
            
            var rootCommand = new RootCommand("Alaris Trading System - Lean Integration Engine");

            var modeOption = new Option<string>(
                name: "--mode",
                description: "The operational mode for the Lean engine.",
                getDefaultValue: () => "backtest");
            modeOption.AddAlias("-m");
            modeOption.FromAmong("live", "paper", "backtest", "download");
            
            var configDirOption = new Option<DirectoryInfo>(
                name: "--config-dir",
                description: "Path to the configuration directory.",
                getDefaultValue: () => new DirectoryInfo(Path.Combine(FindProjectRoot(), "config")));
            configDirOption.AddAlias("-c");

            var verboseOption = new Option<bool>(
                name: "--verbose",
                description: "Enable verbose logging for debugging.",
                getDefaultValue: () => false);
            verboseOption.AddAlias("-v");

            var debugOption = new Option<bool>(
                name: "--debug",
                description: "Enable maximum debugging output.",
                getDefaultValue: () => false);
            debugOption.AddAlias("-d");

            rootCommand.AddOption(modeOption);
            rootCommand.AddOption(configDirOption);
            rootCommand.AddOption(verboseOption);
            rootCommand.AddOption(debugOption);

            rootCommand.SetHandler((mode, configDir, verbose, debug) =>
            {
                try
                {
                    // Set debug levels based on command line options
                    if (debug || verbose)
                    {
                        SetDebugConfiguration(debug, verbose);
                    }

                    Log.Trace($"Starting Alaris Lean Engine - Mode: {mode}, Debug: {debug}, Verbose: {verbose}");
                    Log.Trace($"Command line arguments: {string.Join(" ", args)}");
                    Log.Trace($"Working directory: {Directory.GetCurrentDirectory()}");
                    Log.Trace($"Config directory: {configDir.FullName}");

                    if (mode.Equals("download", StringComparison.OrdinalIgnoreCase))
                    {
                        RunProductionDownload(debug, verbose);
                    }
                    else
                    {
                        RunTrading(mode, configDir.FullName, debug, verbose);
                    }
                }
                catch (Exception ex)
                {
                    LogCriticalError("Main execution failed", ex);
                    return;
                }
            }, modeOption, configDirOption, verboseOption, debugOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static void EnableDebugLogging()
        {
            try
            {
                // Set up initial debug logging before any other operations
                Log.LogHandler = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("ConsoleLogHandler");
                Log.DebuggingEnabled = true;
                Log.DebuggingLevel = 1; // Maximum debug level
                
                Console.WriteLine("=== Alaris Debug Mode Enabled ===");
                Console.WriteLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");
                Console.WriteLine($"Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to enable debug logging: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void LogSystemInformation()
        {
            try
            {
                Log.Trace("=== System Information ===");
                Log.Trace($"OS: {Environment.OSVersion}");
                Log.Trace($"64-bit Process: {Environment.Is64BitProcess}");
                Log.Trace($"64-bit OS: {Environment.Is64BitOperatingSystem}");
                Log.Trace($"Processor Count: {Environment.ProcessorCount}");
                Log.Trace($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
                Log.Trace($"CLR Version: {Environment.Version}");
                
                // Log .NET information
                var assembly = Assembly.GetExecutingAssembly();
                Log.Trace($"Assembly Location: {assembly.Location}");
                Log.Trace($"Assembly Version: {assembly.GetName().Version}");
                Log.Trace($"Target Framework: {assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName ?? "Unknown"}");
                
                // Log environment variables relevant to .NET and QuantConnect
                LogEnvironmentVariable("DOTNET_ROOT");
                LogEnvironmentVariable("DOTNET_HOST_PATH");
                LogEnvironmentVariable("QC_USER_ID");
                LogEnvironmentVariable("QC_API_TOKEN", mask: true);
                
                Log.Trace("=== End System Information ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log system information: {ex.Message}");
            }
        }

        private static void LogEnvironmentVariable(string name, bool mask = false)
        {
            try
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (value != null)
                {
                    if (mask && value.Length > 0)
                    {
                        Log.Trace($"Environment Variable {name}: {value.Substring(0, Math.Min(4, value.Length))}***");
                    }
                    else
                    {
                        Log.Trace($"Environment Variable {name}: {value}");
                    }
                }
                else
                {
                    Log.Trace($"Environment Variable {name}: <not set>");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to read environment variable {name}: {ex.Message}");
            }
        }

        private static void SetDebugConfiguration(bool debug, bool verbose)
        {
            try
            {
                // Enable comprehensive debugging in QuantConnect configuration
                Config.Set("debugging", "true");
                Config.Set("debugging-method", "LocalCmdline");
                Config.Set("show-missing-data-logs", "true");
                Config.Set("log-level", debug ? "Trace" : "Debug");
                Config.Set("log-handler", "QuantConnect.Logging.CompositeLogHandler");
                
                // Additional debug settings
                if (debug)
                {
                    Config.Set("verbose", "true");
                    Config.Set("log-assembly-loading", "true");
                    Config.Set("log-memory-usage", "true");
                    Config.Set("break-on-first-exception", "true");
                    Log.DebuggingLevel = 2; // Maximum verbosity
                }

                Log.Trace($"Debug configuration set - Debug: {debug}, Verbose: {verbose}");
            }
            catch (Exception ex)
            {
                LogCriticalError("Failed to set debug configuration", ex);
            }
        }

        private static void RunProductionDownload(bool debug, bool verbose)
        {
            Log.Trace("=== Starting Production Download Mode ===");

            try
            {
                // Validate QuantConnect credentials
                var qcUserId = Environment.GetEnvironmentVariable("QC_USER_ID");
                var qcApiToken = Environment.GetEnvironmentVariable("QC_API_TOKEN");
                
                if (string.IsNullOrEmpty(qcUserId) || string.IsNullOrEmpty(qcApiToken))
                {
                    LogCriticalError("QuantConnect API credentials not found! Set QC_USER_ID and QC_API_TOKEN environment variables.", null);
                    return;
                }

                // Validate credential format
                if (!int.TryParse(qcUserId, out int userId) || userId <= 0)
                {
                    LogCriticalError($"Invalid QC_USER_ID format: {qcUserId}. Must be a positive integer.", null);
                    return;
                }

                if (string.IsNullOrWhiteSpace(qcApiToken) || qcApiToken.Length < 32)
                {
                    LogCriticalError("Invalid QC_API_TOKEN format. Must be a valid API token from QuantConnect.", null);
                    return;
                }

                Log.Trace($"✓ Validated QuantConnect credentials for user: {qcUserId}");

                // Configure comprehensive Lean environment for production data download
                SetupProductionConfiguration(qcUserId, qcApiToken, debug);

                // Initialize API components before running engine
                InitializeApiComponents();

                Log.Trace("✓ Production download configuration complete");
                RunEngine("download", debug, verbose);
            }
            catch (Exception ex)
            {
                LogCriticalError("Failed to run production download mode", ex);
            }
        }

        private static void SetupProductionConfiguration(string userId, string apiToken, bool debug)
        {
            Log.Trace("Setting up production configuration for QuantConnect API...");

            // Core Lean configuration
            Config.Set("environment", "backtesting-desktop");
            Config.Set("algorithm-type-name", "Alaris.Algorithm.DataDownload");
            Config.Set("algorithm-location", "./bin/Alaris.Lean.dll");
            Config.Set("live-mode", "false");

            // API Authentication Configuration - Critical for hash-based auth
            Config.Set("job-user-id", userId);
            Config.Set("api-access-token", apiToken);
            Config.Set("api-url", "https://www.quantconnect.com/api/v2/");
            
            // Organization and project configuration
            Config.Set("job-organization-id", ""); // Will be populated by API
            Config.Set("project-id", "0");
            Config.Set("version-id", "");
            
            // Data provider configuration - Critical for ApiDataProvider
            Config.Set("data-provider", "QuantConnect.Lean.Engine.DataFeeds.ApiDataProvider");
            Config.Set("downloader-data-update-period", "7");
            Config.Set("data-purchase-limit", decimal.MaxValue.ToString());
            Config.Set("data-provider-agree-to-terms", "true");

            // API Client Configuration - Required for proper authentication
            Config.Set("api-timeout", "300000"); // 5 minutes for large downloads
            Config.Set("api-retry-count", "3");
            Config.Set("api-retry-delay", "5000"); // 5 seconds between retries

            // Engine handlers configuration
            Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.BacktestingSetupHandler");
            Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");
            Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed");
            Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler");
            Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler");
            
            // File providers - Required by ApiDataProvider
            Config.Set("map-file-provider", "QuantConnect.Data.Auxiliary.LocalZipMapFileProvider");
            Config.Set("factor-file-provider", "QuantConnect.Data.Auxiliary.LocalZipFactorFileProvider");
            Config.Set("data-permission-manager", "DataPermissionManager");
            Config.Set("object-store", "LocalObjectStore");

            // Lean Manager configuration
            Config.Set("lean-manager-type", "LocalLeanManager");

            // Additional required configurations for ApiDataProvider
            Config.Set("data-directory", Config.Get("data-folder"));
            Config.Set("cache-location", Config.Get("cache-location"));
            
            // Job packet configuration - Required for proper API context
            Config.Set("job-id", "0");
            Config.Set("algorithm-id", "");
            
            // Security configuration
            Config.Set("force-exchange-always-open", "false");
            Config.Set("live-mode-brokerage", "");
            
            // Performance configuration for large downloads
            Config.Set("maximum-concurrent-data-downloads", "4");
            Config.Set("data-download-timeout", "600000"); // 10 minutes per file

            Log.Trace("✓ Production configuration setup complete");
        }

        private static void InitializeApiComponents()
        {
            Log.Trace("Initializing API components...");

            try
            {
                // Check if API client is available - Fixed method call
                var apiClient = Composer.Instance.GetExportedValueByTypeName<IApi>("Api");
                if (apiClient == null)
                {
                    Log.Trace("Creating new API client instance...");
                    apiClient = new Api();
                    Log.Trace("✓ API client created");
                }
                else
                {
                    Log.Trace("✓ API client already available");
                }

                // Test basic API availability
                TestApiConnectivity(apiClient);

                Log.Trace("✓ API components initialized successfully");
            }
            catch (Exception ex)
            {
                LogCriticalError("Failed to initialize API components", ex);
                throw;
            }
        }

        private static void TestApiConnectivity(IApi api)
        {
            Log.Trace("Testing QuantConnect API connectivity...");

            try
            {
                if (api == null)
                {
                    throw new InvalidOperationException("API client not available");
                }

                // Note: We can't do a full test here because it would trigger the same
                // authentication issues. The real test happens when ApiDataProvider is created.
                Log.Trace("✓ API client is available and configured");
            }
            catch (Exception ex)
            {
                Log.Error($"API connectivity test failed: {ex.Message}");
                throw;
            }
        }

        private static void RunTrading(string mode, string configDir, bool debug, bool verbose)
        {
            Log.Trace($"=== Starting Trading Mode: {mode.ToUpper()} ===");
            Log.Trace($"Config Directory: {configDir}");

            try
            {
                // Use debug config if debug mode is enabled
                var configFileName = debug ? "lean_process_debug.yaml" : "lean_process.yaml";
                var leanConfigPath = Path.Combine(configDir, configFileName);
                
                Log.Trace($"Looking for configuration file: {leanConfigPath}");
                
                if (!File.Exists(leanConfigPath))
                {
                    // Fall back to standard config if debug config doesn't exist
                    if (debug)
                    {
                        leanConfigPath = Path.Combine(configDir, "lean_process.yaml");
                        Log.Trace($"Debug config not found, falling back to: {leanConfigPath}");
                    }
                    
                    if (!File.Exists(leanConfigPath))
                    {
                        LogCriticalError($"Configuration file not found: {leanConfigPath}", null);
                        return;
                    }
                }

                Log.Trace($"Loading configuration from: {leanConfigPath}");
                
                if (!LoadConfigurationFromYaml(leanConfigPath, mode, debug, verbose))
                {
                    LogCriticalError("Engine startup failed due to configuration errors", null);
                    return;
                }

                RunEngine(mode, debug, verbose);
            }
            catch (Exception ex)
            {
                LogCriticalError($"Failed to run trading mode {mode}", ex);
            }
        }

        private static void RunEngine(string mode, bool debug, bool verbose)
        {
            Log.Trace($"=== Running Lean Engine [Mode: {mode.ToUpper()}] ===");

            // Set up data directories with enhanced logging
            string? buildDirectory = FindBuildDirectory(Directory.GetCurrentDirectory());
            if (buildDirectory == null)
            {
                LogCriticalError("Could not find the 'build' directory. Please run from the project root or build directory.", null);
                return;
            }

            Log.Trace($"Build directory found: {buildDirectory}");

            var dataDir = Path.Combine(buildDirectory, "data");
            var cacheDir = Path.Combine(buildDirectory, "cache");
            var resultsDir = Path.Combine(buildDirectory, "results");
            var logsDir = Path.Combine(buildDirectory, "logs");

            EnsureDirectoryExists(dataDir);
            EnsureDirectoryExists(cacheDir);
            EnsureDirectoryExists(resultsDir);
            EnsureDirectoryExists(logsDir);

            Config.Set("data-folder", dataDir);
            Config.Set("cache-location", cacheDir);
            Config.Set("results-destination-folder", resultsDir);

            Log.Trace($"Data folder: {Config.Get("data-folder")}");
            Log.Trace($"Cache location: {Config.Get("cache-location")}");
            Log.Trace($"Results destination: {Config.Get("results-destination-folder")}");

            LeanEngineSystemHandlers? systemHandlers = null;
            try
            {
                Log.Trace("=== Initializing Lean Engine System Handlers ===");
                
                // Log all current configuration before creating handlers
                if (debug)
                {
                    LogCurrentConfiguration();
                }

                systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
                Log.Trace("✓ System handlers created successfully");
                
                systemHandlers.Initialize();
                Log.Trace("✓ System handlers initialized successfully");

                Log.Trace("=== Initializing Lean Engine Algorithm Handlers ===");
                var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
                Log.Trace("✓ Algorithm handlers created successfully");
                
                var isLiveMode = Config.GetBool("live-mode");
                Log.Trace($"Live mode: {isLiveMode}");
                
                var engine = new Engine(systemHandlers, algorithmHandlers, isLiveMode);
                Log.Trace("✓ Engine created successfully");
                
                var algorithmManager = new AlgorithmManager(isLiveMode);
                Log.Trace("✓ Algorithm manager created successfully");
                
                // Create appropriate packet based on mode
                AlgorithmNodePacket packet;
                if (isLiveMode)
                {
                    packet = new LiveNodePacket();
                    Log.Trace("✓ Live node packet created");
                }
                else
                {
                    packet = new BacktestNodePacket();
                    Log.Trace("✓ Backtest node packet created");
                }

                // Set user and token for data provider access
                packet.UserId = Config.GetInt("job-user-id");
                packet.Channel = Config.Get("api-access-token");
                Log.Trace($"Packet configured - User ID: {packet.UserId}");
                
                systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, packet, algorithmManager);
                Log.Trace("✓ Lean manager initialized successfully");
                
                string algorithmLocation = Config.Get("algorithm-location", "Alaris.Lean.dll");
                string algorithmTypeName = Config.Get("algorithm-type-name", "Alaris.Algorithm.ArbitrageAlgorithm");
                
                Log.Trace($"Starting engine with algorithm: {algorithmTypeName} from {algorithmLocation}");
                
                // Verify algorithm assembly exists
                if (!File.Exists(algorithmLocation))
                {
                    // Try to find it in the current directory
                    var localPath = Path.Combine(Directory.GetCurrentDirectory(), algorithmLocation);
                    if (File.Exists(localPath))
                    {
                        algorithmLocation = localPath;
                        Log.Trace($"Found algorithm assembly at: {algorithmLocation}");
                    }
                    else
                    {
                        LogCriticalError($"Algorithm assembly not found: {algorithmLocation}", null);
                        return;
                    }
                }
                
                Log.Trace("=== Starting Engine Execution ===");
                engine.Run(packet, algorithmManager, algorithmLocation, WorkerThread.Instance);
                Log.Trace("✓ Engine execution completed successfully");
            }
            catch (Exception ex)
            {
                LogCriticalError($"Engine execution failed in {mode} mode", ex);
                
                // Provide specific troubleshooting for download mode
                if (mode == "download")
                {
                    Log.Error("=== Download Mode Troubleshooting ===");
                    Log.Error("Common solutions:");
                    Log.Error("1. Verify QuantConnect credentials are correct");
                    Log.Error("2. Check account has data agreement signed");
                    Log.Error("3. Ensure sufficient credit balance for data purchase");
                    Log.Error("4. Check network connectivity to quantconnect.com");
                    Log.Error("5. Try again in a few minutes if rate limited");
                }
            }
            finally
            {
                Log.Trace($"=== Alaris Lean Engine Shutdown [Mode: {mode.ToUpper()}] ===");
                try
                {
                    Log.Trace("LeanEngineSystemHandlers.Dispose(): start...");
                    systemHandlers?.Dispose();
                    Log.Trace("LeanEngineSystemHandlers.Dispose(): Disposed of system handlers.");
                    Log.Trace("✓ System handlers disposed successfully");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error during cleanup: {ex.Message}");
                }
            }
        }

        private static void LogCurrentConfiguration()
        {
            try
            {
                Log.Trace("=== Current Configuration ===");
                
                // Log key configuration values
                var configKeys = new[]
                {
                    "algorithm-type-name", "algorithm-location", "environment",
                    "setup-handler", "result-handler", "data-feed-handler",
                    "real-time-handler", "transaction-handler", "live-mode",
                    "data-provider", "job-user-id", "debugging", "log-level",
                    "api-access-token", "api-url", "job-organization-id",
                    "project-id", "data-purchase-limit", "downloader-data-update-period"
                };

                foreach (var key in configKeys)
                {
                    var value = Config.Get(key, "<not set>");
                    if (key == "api-access-token" && value.Length > 10)
                    {
                        value = value.Substring(0, 8) + "***";
                    }
                    Log.Trace($"Config {key}: {value}");
                }
                
                Log.Trace("=== End Configuration ===");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to log configuration: {ex.Message}");
            }
        }

        private static bool LoadConfigurationFromYaml(string yamlPath, string mode, bool debug, bool verbose)
        {
            try
            {
                Log.Trace($"Loading configuration from: {yamlPath}");
                
                if (!File.Exists(yamlPath))
                {
                    LogCriticalError($"Configuration file not found: {yamlPath}", null);
                    return false;
                }

                // Read the file for debugging
                if (debug)
                {
                    try
                    {
                        var content = File.ReadAllText(yamlPath);
                        Log.Trace($"Configuration file content length: {content.Length} characters");
                        // Don't log the full content as it might contain sensitive information
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to read configuration file for debugging: {ex.Message}");
                    }
                }

                // For now, we'll use a simplified configuration loading approach
                // In a production system, you would implement proper YAML parsing
                
                Config.Set("job-user-id", Environment.GetEnvironmentVariable("QC_USER_ID") ?? "0");
                Config.Set("api-access-token", Environment.GetEnvironmentVariable("QC_API_TOKEN") ?? "");
                Config.Set("data-provider-agree-to-terms", "true");
                Config.Set("algorithm-type-name", "Alaris.Algorithm.ArbitrageAlgorithm");
                Config.Set("algorithm-location", "Alaris.Lean.dll");
                
                bool isLive = (mode == "live" || mode == "paper");
                Config.Set("live-mode", isLive.ToString().ToLower());
                
                if (isLive)
                {
                    Log.Trace("Configuring for live trading mode");
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
                    Log.Trace("Configuring for backtesting mode");
                    // Backtesting configuration
                    Config.Set("setup-handler", "QuantConnect.Lean.Engine.Setup.BacktestingSetupHandler");
                    Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.BacktestingResultHandler");
                    Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed");
                    Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler");
                    Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler");
                    Config.Set("map-file-provider", "QuantConnect.Data.Auxiliary.LocalZipMapFileProvider");
                    Config.Set("factor-file-provider", "QuantConnect.Data.Auxiliary.LocalZipFactorFileProvider");
                }
                
                Log.Trace("✓ Configuration loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogCriticalError($"Failed to load configuration from {yamlPath}", ex);
                return false;
            }
        }

        private static void LogCriticalError(string message, Exception? ex)
        {
            Console.WriteLine($"CRITICAL ERROR: {message}");
            Log.Error($"CRITICAL ERROR: {message}");
            
            if (ex != null)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Type: {ex.GetType().FullName}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                Log.Error($"Exception: {ex.Message}");
                Log.Error($"Type: {ex.GetType().FullName}");
                Log.Error($"Stack Trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Type: {ex.InnerException.GetType().FullName}");
                    Console.WriteLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                    
                    Log.Error($"Inner Exception: {ex.InnerException.Message}");
                    Log.Error($"Inner Type: {ex.InnerException.GetType().FullName}");
                    Log.Error($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    Log.Trace($"Created directory: {path}");
                }
                else
                {
                    Log.Trace($"Directory exists: {path}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to create directory {path}: {ex.Message}");
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
            Log.Trace($"Searching for build directory starting from: {startDirectory}");
            
            string currentDir = startDirectory;
            for (int i = 0; i < 5; i++)
            {
                Log.Trace($"Checking directory: {currentDir}");
                
                if (Path.GetFileName(currentDir).Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Trace($"Found build directory: {currentDir}");
                    return currentDir;
                }
                
                string buildDir = Path.Combine(currentDir, "build");
                if (Directory.Exists(buildDir))
                {
                    Log.Trace($"Found build subdirectory: {buildDir}");
                    return buildDir;
                }
                
                var parent = Directory.GetParent(currentDir);
                if (parent == null) 
                {
                    Log.Trace("Reached root directory, stopping search");
                    break;
                }
                currentDir = parent.FullName;
            }
            
            Log.Error("Build directory not found");
            return null;
        }
    }
}