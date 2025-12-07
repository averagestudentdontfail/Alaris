// =============================================================================
// APap001A.cs - Alaris Application Entry Point
// Component: APap001A | Category: Application Entry | Variant: A (Primary)
// =============================================================================
// Provides CLI entry point with Spectre.Console interactive menus.
// Commands: run, config, data
// =============================================================================

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Application.Commands;

namespace Alaris.Application;

/// <summary>
/// Alaris Trading System CLI Entry Point.
/// Component ID: APap001A
/// </summary>
public static class APap001A
{
    /// <summary>
    /// Main entry point for Alaris CLI.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    public static int Main(string[] args)
    {
        // If no arguments, launch interactive mode
        if (args.Length == 0)
        {
            return RunInteractiveMode();
        }

        // Configure CLI application
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("alaris");
            config.SetApplicationVersion("1.0.0");

            config.AddCommand<APcm001A>("run")
                .WithDescription("Run Alaris algorithm in specified mode")
                .WithExample("run", "--mode", "backtest")
                .WithExample("run", "--mode", "paper")
                .WithExample("run", "--mode", "live");

            config.AddCommand<APcm002A>("config")
                .WithDescription("View or modify Alaris configuration")
                .WithExample("config", "show")
                .WithExample("config", "set", "ib-account", "DU12345");

            config.AddCommand<APcm003A>("data")
                .WithDescription("Download and manage market data")
                .WithExample("data", "download", "--ticker", "AAPL");
        });

        return app.Run(args);
    }

    /// <summary>
    /// Launch interactive terminal UI for mode selection.
    /// </summary>
    private static int RunInteractiveMode()
    {
        AnsiConsole.Write(
            new FigletText("Alaris")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[grey]Quantitative Trading System[/]");
        AnsiConsole.WriteLine();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select execution mode:[/]")
                .PageSize(10)
                .AddChoices(new[]
                {
                    "Backtest - Run historical simulation",
                    "Paper - Live paper trading (IBKR)",
                    "Live - Live trading (IBKR)",
                    "Configuration - View/edit settings",
                    "Data Management - Download market data",
                    "Exit"
                }));

        return selection switch
        {
            "Backtest - Run historical simulation" => ExecuteMode("backtest"),
            "Paper - Live paper trading (IBKR)" => ExecuteMode("paper"),
            "Live - Live trading (IBKR)" => ExecuteMode("live"),
            "Configuration - View/edit settings" => ShowConfiguration(),
            "Data Management - Download market data" => ManageData(),
            _ => 0
        };
    }

    private static int ExecuteMode(string mode)
    {
        AnsiConsole.MarkupLine($"[yellow]Starting {mode} mode...[/]");
        return Main(new[] { "run", "--mode", mode });
    }

    private static int ShowConfiguration()
    {
        return Main(new[] { "config", "show" });
    }

    private static int ManageData()
    {
        AnsiConsole.MarkupLine("[bold blue]Data Management[/]");
        AnsiConsole.WriteLine();

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select data action:[/]")
                .AddChoices(new[]
                {
                    "Download equity data from Polygon",
                    "Download option data from Polygon",
                    "List existing data",
                    "Check data status",
                    "Back to main menu"
                }));

        return action switch
        {
            "Download equity data from Polygon" => DownloadData("equity"),
            "Download option data from Polygon" => DownloadData("option"),
            "List existing data" => Main(new[] { "data", "list" }),
            "Check data status" => Main(new[] { "data", "status" }),
            _ => RunInteractiveMode()
        };
    }

    private static int DownloadData(string dataType)
    {
        AnsiConsole.MarkupLine($"[yellow]Download {dataType} data[/]");
        AnsiConsole.WriteLine();

        // Prompt for ticker(s)
        var tickers = AnsiConsole.Ask<string>(
            "[green]Enter ticker(s)[/] [grey](comma-separated, e.g., AAPL,MSFT,GOOGL)[/]:");

        // Prompt for date range
        var defaultFrom = DateTime.Now.AddYears(-1).ToString("yyyyMMdd");
        var fromDate = AnsiConsole.Ask(
            $"[green]Start date[/] [grey](YYYYMMDD)[/]:", 
            defaultFrom);

        var defaultTo = DateTime.Now.ToString("yyyyMMdd");
        var toDate = AnsiConsole.Ask(
            $"[green]End date[/] [grey](YYYYMMDD)[/]:", 
            defaultTo);

        // Prompt for resolution
        var resolution = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select resolution:[/]")
                .AddChoices(new[] { "minute", "hour", "daily" }));

        AnsiConsole.WriteLine();

        // Execute the download command
        var args = new List<string>
        {
            "data", "download",
            "--tickers", tickers,
            "--from", fromDate,
            "--to", toDate,
            "--resolution", resolution,
            "--type", dataType
        };

        return Main(args.ToArray());
    }
}
