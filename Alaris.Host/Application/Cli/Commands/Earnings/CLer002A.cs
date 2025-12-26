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
        CLif003A.Info($"Checking upcoming earnings ({settings.Days} days)...");
        AnsiConsole.WriteLine();

        // Find cache directory
        string cachePath = Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA") 
            ?? System.IO.Path.Combine(Environment.CurrentDirectory, "Alaris.Sessions");
        
        string nasdaqPath = System.IO.Path.Combine(cachePath, "earnings", "nasdaq");
        
        if (!Directory.Exists(nasdaqPath))
        {
            CLif003A.Warning("No earnings cache found. Run 'alaris earnings bootstrap' first.");
            return 1;
        }

        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(settings.Days);
        var upcomingEarnings = new List<(DateTime Date, string Symbol, string Time)>();

        // Scan cached files
        await Task.Run(() =>
        {
            foreach (var file in Directory.GetFiles(nasdaqPath, "*.json"))
            {
                string filename = System.IO.Path.GetFileNameWithoutExtension(file);
                if (DateTime.TryParse(filename, out DateTime date) && date >= startDate && date <= endDate)
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        var doc = System.Text.Json.JsonDocument.Parse(content);
                        
                        if (doc.RootElement.TryGetProperty("Earnings", out var earnings))
                        {
                            foreach (var earning in earnings.EnumerateArray())
                            {
                                string symbol = earning.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                                string time = earning.TryGetProperty("time", out var t) ? t.GetString() ?? "--" : "--";
                                
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
        upcomingEarnings = upcomingEarnings.OrderBy(e => e.Date).ThenBy(e => e.Symbol).ToList();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title($"[bold]Upcoming Earnings ({settings.Days} days)[/]");

        table.AddColumn(new TableColumn("[grey]Date[/]").Centered());
        table.AddColumn(new TableColumn("[grey]Symbol[/]").LeftAligned());
        table.AddColumn(new TableColumn("[grey]Time[/]").Centered());

        foreach (var (date, symbol, time) in upcomingEarnings.Take(50))
        {
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
