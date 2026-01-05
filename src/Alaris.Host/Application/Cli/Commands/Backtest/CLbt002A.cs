// CLbt002A.cs - Backtest analyze command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;
using Alaris.Host.Application.Model;
using Alaris.Host.Application.Service;

namespace Alaris.Host.Application.Cli.Commands.Backtest;

/// <summary>
/// Analyzes backtest results for a session.
/// Component ID: CLbt002A
/// </summary>
public sealed class CLbt002A : AsyncCommand<BacktestAnalyzeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestAnalyzeSettings settings)
    {
        CLif003A.Info($"Analyzing backtest results: {settings.SessionId}");
        AnsiConsole.WriteLine();

        APsv001A sessionService = new APsv001A();
        APmd001A? session = await sessionService.GetAsync(settings.SessionId);

        if (session == null)
        {
            CLif003A.Error($"Session not found: {settings.SessionId}");
            return 1;
        }

        string dataPath = sessionService.GetDataPath(session.SessionId);
        string resultsPath = System.IO.Path.Combine(dataPath, "..", "results");

        // Look for LEAN output files
        string[] jsonFiles = Directory.Exists(resultsPath) 
            ? Directory.GetFiles(resultsPath, "*.json") 
            : Array.Empty<string>();

        if (jsonFiles.Length == 0)
        {
            CLif003A.Warning("No backtest results found. Run 'alaris backtest run' first.");
            return 1;
        }

        // Parse statistics from latest result
        string latestResult = jsonFiles[0];
        DateTime latestWrite = File.GetLastWriteTime(latestResult);
        for (int i = 1; i < jsonFiles.Length; i++)
        {
            string candidate = jsonFiles[i];
            DateTime candidateWrite = File.GetLastWriteTime(candidate);
            if (candidateWrite > latestWrite)
            {
                latestWrite = candidateWrite;
                latestResult = candidate;
            }
        }
        
        try
        {
            string content = await File.ReadAllTextAsync(latestResult);
            System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(content);

            // Extract key metrics
            List<(string Key, string Value)> metrics = new List<(string Key, string Value)>();

            if (doc.RootElement.TryGetProperty("Statistics", out System.Text.Json.JsonElement stats))
            {
                string[] keyMetrics = {
                    "Total Trades", "Average Win", "Average Loss", "Win Rate",
                    "Sharpe Ratio", "Sortino Ratio", "Compounding Annual Return",
                    "Drawdown", "Net Profit"
                };

                foreach (string metric in keyMetrics)
                {
                    if (stats.TryGetProperty(metric, out System.Text.Json.JsonElement value))
                    {
                        string formattedValue = value.GetString() ?? value.ToString();
                        metrics.Add((metric, formattedValue));
                    }
                }
            }

            if (metrics.Count > 0)
            {
                CLif003A.WriteKeyValueTable("Backtest Statistics", metrics);
            }
            else
            {
                CLif003A.Warning("Could not parse statistics from result file.");
            }

            // Show equity summary if available
            if (doc.RootElement.TryGetProperty("Charts", out System.Text.Json.JsonElement charts) &&
                charts.TryGetProperty("Strategy Equity", out System.Text.Json.JsonElement equityChart))
            {
                AnsiConsole.WriteLine();
                CLif003A.Info("Equity data available. Use '--format json' for full data.");
            }

            AnsiConsole.WriteLine();
            CLif003A.Success($"Results from: {System.IO.Path.GetFileName(latestResult)}");

            return 0;
        }
        catch (Exception ex)
        {
            CLif003A.Error($"Failed to parse results: {ex.Message}");
            return 1;
        }
    }
}
