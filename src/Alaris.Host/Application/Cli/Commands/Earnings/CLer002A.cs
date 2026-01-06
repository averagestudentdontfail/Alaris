// CLer002A.cs - Earnings upcoming command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;

namespace Alaris.Host.Application.Cli.Commands.Earnings;

/// <summary>
/// Shows upcoming earnings announcements from cached data.
/// Component ID: CLer002A
/// </summary>
public sealed class CLer002A : AsyncCommand<EarningsUpcomingSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, EarningsUpcomingSettings settings)
    {
        if (settings.Days <= 0)
        {
            CLif003A.Error("Days must be greater than zero.");
            return 1;
        }

        CLif003A.Info($"Checking upcoming earnings ({settings.Days} days)...");
        AnsiConsole.WriteLine();

        // Find cache directory
        string cachePath = Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA") 
            ?? System.IO.Path.Combine(Environment.CurrentDirectory, "ses");
        
        string nasdaqPath = System.IO.Path.Combine(cachePath, "earnings", "nasdaq");
        
        if (!Directory.Exists(nasdaqPath))
        {
            CLif003A.Warning("No earnings cache found. Run 'alaris earnings bootstrap' first.");
            return 1;
        }

        DateTime startDate = DateTime.UtcNow.Date;
        DateTime endDate = startDate.AddDays(settings.Days);
        List<(DateTime Date, string Symbol, string Time)> upcomingEarnings = new List<(DateTime Date, string Symbol, string Time)>();

        // Scan cached files
        await Task.Run(() =>
        {
            string[] files = Directory.GetFiles(nasdaqPath, "*.json");
            foreach (string file in files)
            {
                string filename = System.IO.Path.GetFileNameWithoutExtension(file);
                if (DateTime.TryParse(filename, out DateTime date) && date >= startDate && date <= endDate)
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(content);
                        
                        if (doc.RootElement.TryGetProperty("Earnings", out System.Text.Json.JsonElement earnings))
                        {
                            foreach (System.Text.Json.JsonElement earning in earnings.EnumerateArray())
                            {
                                string symbol = earning.TryGetProperty("symbol", out System.Text.Json.JsonElement s) ? s.GetString() ?? "" : "";
                                string time = earning.TryGetProperty("time", out System.Text.Json.JsonElement t) ? t.GetString() ?? "--" : "--";
                                
                                if (!string.IsNullOrEmpty(symbol))
                                {
                                    if (string.IsNullOrEmpty(settings.Symbol) || 
                                        symbol.Equals(settings.Symbol, StringComparison.OrdinalIgnoreCase))
                                    {
                                        upcomingEarnings.Add((date, symbol.ToUpperInvariant(), time));
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip corrupt files
                    }
                }
            }
        });

        if (upcomingEarnings.Count == 0)
        {
            CLif003A.Info($"No earnings announcements found in next {settings.Days} days.");
            return 0;
        }

        // Sort and display
        upcomingEarnings.Sort((left, right) =>
        {
            int dateCompare = left.Date.CompareTo(right.Date);
            if (dateCompare != 0)
            {
                return dateCompare;
            }

            return string.Compare(left.Symbol, right.Symbol, StringComparison.Ordinal);
        });

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title($"[bold]Upcoming Earnings ({settings.Days} days)[/]");

        table.AddColumn(new TableColumn("[grey]Date[/]").Centered());
        table.AddColumn(new TableColumn("[grey]Symbol[/]").LeftAligned());
        table.AddColumn(new TableColumn("[grey]Time[/]").Centered());

        int displayCount = upcomingEarnings.Count > 50 ? 50 : upcomingEarnings.Count;
        for (int i = 0; i < displayCount; i++)
        {
            (DateTime date, string symbol, string time) = upcomingEarnings[i];
            string timingDisplay = time switch
            {
                "time-pre-market" => "[yellow]BMO[/]",
                "time-after-hours" => "[blue]AMC[/]",
                _ => "[grey]--[/]"
            };

            table.AddRow($"{date:MMM dd}", $"[bold]{symbol}[/]", timingDisplay);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        
        if (upcomingEarnings.Count > 50)
        {
            CLif003A.Info($"Showing 50 of {upcomingEarnings.Count} earnings.");
        }

        return 0;
    }
}
