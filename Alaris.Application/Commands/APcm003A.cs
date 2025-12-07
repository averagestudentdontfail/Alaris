// =============================================================================
// APcm003A.cs - Data Command
// Component: APcm003A | Category: Commands | Variant: A (Primary)
// =============================================================================
// Implements 'alaris data download' command for market data management.
// =============================================================================

using System.ComponentModel;
using System.IO;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Alaris.Application.Commands;

/// <summary>
/// Data Command settings.
/// </summary>
public sealed class DataSettings : CommandSettings
{
    [CommandArgument(0, "[action]")]
    [Description("Action: download, list")]
    [DefaultValue("list")]
    public string Action { get; init; } = "list";

    [CommandOption("-t|--ticker <TICKER>")]
    [Description("Ticker symbol (e.g., AAPL)")]
    public string? Ticker { get; init; }

    [CommandOption("--source <SOURCE>")]
    [Description("Data source: polygon")]
    [DefaultValue("polygon")]
    public string Source { get; init; } = "polygon";

    [CommandOption("--type <TYPE>")]
    [Description("Data type: option, equity")]
    [DefaultValue("option")]
    public string DataType { get; init; } = "option";

    [CommandOption("--from <DATE>")]
    [Description("Start date (YYYY-MM-DD)")]
    public string? FromDate { get; init; }
}

/// <summary>
/// Alaris Data Command - download and manage market data.
/// Component ID: APcm003A
/// </summary>
public sealed class APcm003A : Command<DataSettings>
{
    public override int Execute(CommandContext context, DataSettings settings)
    {
        return settings.Action.ToLowerInvariant() switch
        {
            "download" => DownloadData(settings),
            "list" => ListData(),
            _ => InvalidAction(settings.Action)
        };
    }

    private static int DownloadData(DataSettings settings)
    {
        if (settings.Ticker is null)
        {
            AnsiConsole.MarkupLine("[red]Ticker is required. Use --ticker AAPL[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[blue]Data Download[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("[grey]Parameter[/]");
        table.AddColumn("[white]Value[/]");
        table.AddRow("Source", settings.Source);
        table.AddRow("Type", settings.DataType);
        table.AddRow("Ticker", settings.Ticker);
        table.AddRow("From Date", settings.FromDate ?? "(default)");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]Data download not yet implemented.[/]");
        AnsiConsole.MarkupLine("[grey]This feature will use LEAN ToolBox for data download.[/]");

        // Future implementation would use:
        // dotnet run --project Alaris.Lean/ToolBox -- \
        //   --app=PolygonDataDownloader \
        //   --from-date=20240101 \
        //   --tickers=AAPL

        return 0;
    }

    private static int ListData()
    {
        var dataPath = FindDataPath();
        if (dataPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find data folder[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Data folder:[/] {dataPath}");
        AnsiConsole.WriteLine();

        // List data directories
        if (Directory.Exists(dataPath))
        {
            var dirs = Directory.GetDirectories(dataPath).Take(10);
            foreach (var dir in dirs)
            {
                AnsiConsole.MarkupLine($"  [blue]{System.IO.Path.GetFileName(dir)}/[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]No data found[/]");
        }

        return 0;
    }

    private static int InvalidAction(string action)
    {
        AnsiConsole.MarkupLine($"[red]Unknown action: {action}. Use 'download' or 'list'.[/]");
        return 1;
    }

    private static string? FindDataPath()
    {
        var paths = new[]
        {
            "Alaris.Lean/Data",
            "../Alaris.Lean/Data",
            "../../Alaris.Lean/Data"
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }

        return null;
    }
}
