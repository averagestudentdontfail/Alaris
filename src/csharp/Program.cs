// src/csharp/Program.cs
using System;
using System.Threading.Tasks;
using QuantConnect.Lean.Engine;
using QuantConnect.Configuration;
using QuantConnect.Util;
using QuantConnect.Interfaces;
using Alaris.Algorithm;

namespace Alaris
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting Alaris Lean Process...");

                // Configure Lean
                ConfigureLean();

                // Create and configure the algorithm
                var algorithm = new DeterministicVolArbitrageAlgorithm();

                // Create Lean engine
                using var engine = new Engine.Results.LiveTradingResultHandler();
                
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

                Console.WriteLine("Shutting down Alaris Lean Process...");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in Alaris Lean Process: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return 1;
            }
        }

        private static void ConfigureLean()
        {
            // Set up basic configuration
            Config.Set("data-directory", "./Data");
            Config.Set("cache-location", "./Cache");
            Config.Set("results-destination-folder", "./Results");
            
            // Configure for live trading with Interactive Brokers
            Config.Set("live-mode", "true");
            Config.Set("live-mode-brokerage", "InteractiveBrokersBrokerage");
            Config.Set("data-feed-handler", "InteractiveBrokersBrokerage");
            
            // Interactive Brokers configuration
            Config.Set("ib-host", "127.0.0.1");
            Config.Set("ib-port", "4001"); // TWS/IB Gateway port
            Config.Set("ib-account", "DU123456"); // Demo account - update with real account
            Config.Set("ib-user-name", "");
            Config.Set("ib-password", "");
            Config.Set("ib-agent-description", "Individual");
            
            // Risk management
            Config.Set("maximum-data-points-per-chart-series", "1000000");
            Config.Set("force-exchange-always-open", "false");
            
            // Performance optimization
            Config.Set("show-missing-data-logs", "false");
            Config.Set("maximum-chart-series", "30");
            
            Console.WriteLine("Lean configuration completed");
        }
    }
}