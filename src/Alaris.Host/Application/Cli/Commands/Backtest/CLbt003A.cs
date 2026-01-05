// CLbt003A.cs - Backtest list command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;
using Alaris.Host.Application.Model;
using Alaris.Host.Application.Service;

namespace Alaris.Host.Application.Cli.Commands.Backtest;

/// <summary>
/// Lists previous backtest sessions.
/// Component ID: CLbt003A
/// </summary>
public sealed class CLbt003A : AsyncCommand<BacktestListSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestListSettings settings)
    {
        APsv001A sessionService = new APsv001A();
        IReadOnlyList<APmd001A> sessions = await sessionService.ListAsync();

        if (sessions.Count == 0)
        {
            CLif003A.Info("No backtest sessions found.");
            AnsiConsole.WriteLine();
            CLif003A.Info("Create one with: alaris backtest create");
            return 0;
        }

        // Sort by creation date (newest first) and limit
        List<APmd001A> displaySessions = new List<APmd001A>(sessions);
        displaySessions.Sort((left, right) => right.CreatedAt.CompareTo(left.CreatedAt));

        int limit = settings.Limit < 0 ? 0 : settings.Limit;
        if (limit == 0 || limit > displaySessions.Count)
        {
            limit = displaySessions.Count;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]Backtest Sessions[/]");

        table.AddColumn(new TableColumn("[grey]Session ID[/]").LeftAligned());
        table.AddColumn(new TableColumn("[grey]Date Range[/]").Centered());
        table.AddColumn(new TableColumn("[grey]Symbols[/]").Centered());
        table.AddColumn(new TableColumn("[grey]Created[/]").Centered());
        table.AddColumn(new TableColumn("[grey]Status[/]").Centered());

        for (int i = 0; i < limit; i++)
        {
            APmd001A session = displaySessions[i];
            string dataPath = sessionService.GetDataPath(session.SessionId);
            string resultsPath = System.IO.Path.Combine(dataPath, "..", "results");

            bool hasData = Directory.Exists(dataPath) && 
                Directory.Exists(System.IO.Path.Combine(dataPath, "equity", "usa", "daily"));
            bool hasResults = Directory.Exists(resultsPath) && 
                Directory.GetFiles(resultsPath, "*.json").Length > 0;

            string status = hasResults ? "[green]Complete[/]" :
                            hasData ? "[yellow]Ready[/]" :
                            "[grey]No Data[/]";

            table.AddRow(
                $"[bold]{session.SessionId}[/]",
                $"{session.StartDate:yyyy-MM-dd} → {session.EndDate:yyyy-MM-dd}",
                session.Symbols.Count.ToString(),
                $"{session.CreatedAt:MMM dd HH:mm}",
                status);
        }

        AnsiConsole.Write(table);

        if (sessions.Count > limit)
        {
            AnsiConsole.WriteLine();
            CLif003A.Info($"Showing {limit} of {sessions.Count} sessions. Use --limit to see more.");
        }

        return 0;
    }
}
