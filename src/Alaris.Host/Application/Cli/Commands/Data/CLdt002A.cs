// CLdt002A.cs - Data status command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;
using Alaris.Host.Application.Service;

namespace Alaris.Host.Application.Cli.Commands.Data;

/// <summary>
/// Shows data coverage and freshness for sessions.
/// Component ID: CLdt002A
/// </summary>
public sealed class CLdt002A : AsyncCommand<DataStatusSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DataStatusSettings settings)
    {
        APsv001A sessionService = new APsv001A();
        IReadOnlyList<APmd001A> allSessions = await sessionService.ListAsync();
        List<APmd001A> sessions = new List<APmd001A>(allSessions);

        if (sessions.Count == 0)
        {
            CLif003A.Warning("No sessions found.");
            return 0;
        }

        // Filter if session specified
        if (!string.IsNullOrEmpty(settings.SessionId))
        {
            List<APmd001A> filtered = new List<APmd001A>();
            foreach (APmd001A session in sessions)
            {
                if (session.SessionId == settings.SessionId)
                {
                    filtered.Add(session);
                }
            }
            sessions = filtered;
            if (sessions.Count == 0)
            {
                CLif003A.Error($"Session not found: {settings.SessionId}");
                return 1;
            }
        }

        // Build status table
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]Data Status[/]");

        table.AddColumn("[grey]Session[/]");
        table.AddColumn("[grey]Date Range[/]");
        table.AddColumn("[grey]Symbols[/]");
        table.AddColumn("[grey]Prices[/]");
        table.AddColumn("[grey]Options[/]");
        table.AddColumn("[grey]Earnings[/]");

        foreach (APmd001A session in sessions)
        {
            string dataPath = sessionService.GetDataPath(session.SessionId);
            
            // Check data coverage
            string pricesPath = System.IO.Path.Combine(dataPath, "equity", "usa", "daily");
            string optionsPath = System.IO.Path.Combine(dataPath, "options");
            string earningsPath = System.IO.Path.Combine(dataPath, "earnings", "nasdaq");

            int priceFiles = Directory.Exists(pricesPath) ? Directory.GetFiles(pricesPath, "*.zip").Length : 0;
            int optionFiles = Directory.Exists(optionsPath) ? Directory.GetFiles(optionsPath, "*.json").Length : 0;
            int earningFiles = Directory.Exists(earningsPath) ? Directory.GetFiles(earningsPath, "*.json").Length : 0;

            string priceStatus = priceFiles > 0 ? $"[green]{priceFiles}[/]" : "[red]0[/]";
            string optionStatus = optionFiles > 0 ? $"[green]{optionFiles}[/]" : "[yellow]0[/]";
            string earningStatus = earningFiles > 0 ? $"[green]{earningFiles}[/]" : "[yellow]0[/]";

            table.AddRow(
                session.SessionId,
                $"{session.StartDate:yyyy-MM-dd} → {session.EndDate:yyyy-MM-dd}",
                session.Symbols.Count.ToString(),
                priceStatus,
                optionStatus,
                earningStatus);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        CLif003A.Info("Prices: ZIP files in equity/usa/daily/");
        CLif003A.Info("Options: JSON files in options/");
        CLif003A.Info("Earnings: Cached dates in earnings/nasdaq/");

        return 0;
    }
}
