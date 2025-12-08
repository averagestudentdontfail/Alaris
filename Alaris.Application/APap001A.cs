// =============================================================================
// APap001A.cs - Alaris Application Entry Point
// Component: APap001A | Category: Application Entry | Variant: A (Primary)
// =============================================================================
// Provides CLI entry point with Spectre.Console interactive menus.
// Commands: run, config, data
// =============================================================================

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Application.Command;
using Alaris.Application.Service;
using Alaris.Application.Model;

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

            config.AddBranch("backtest", backtest =>
            {
                backtest.SetDescription("Manage backtest sessions");
                backtest.AddCommand<BacktestCreateCommand>("create")
                    .WithDescription("Create a new backtest session")
                    .WithExample("backtest", "create", "--start", "2023-01-01", "--end", "2023-01-31");

                backtest.AddCommand<BacktestPrepareCommand>("prepare")
                    .WithDescription("Download data for an existing session")
                    .WithExample("backtest", "prepare", "BT001A-20230101-20230131");
                
                backtest.AddCommand<BacktestRunCommand>("run")
                    .WithDescription("Run a backtest session")
                    .WithExample("backtest", "run", "--latest");
                
                backtest.AddCommand<BacktestListCommand>("list")
                    .WithDescription("List all backtest sessions");
                
                backtest.AddCommand<BacktestViewCommand>("view")
                    .WithDescription("View session details");
                
                backtest.AddCommand<BacktestDeleteCommand>("delete")
                    .WithDescription("Delete a session");
            });

            config.AddCommand<APcm002A>("config")
                .WithDescription("View or modify Alaris configuration")
                .WithExample("config", "show")
                .WithExample("config", "set", "ib-account", "DU12345");

            config.AddCommand<APcm003A>("data")
                .WithDescription("Download and manage market data")
                .WithExample("data", "download", "--ticker", "AAPL");

            config.AddCommand<APcm004A>("universe")
                .WithDescription("Generate and manage universe files from Polygon")
                .WithExample("universe", "generate", "--from", "20240101", "--to", "20241201")
                .WithExample("universe", "list");
        });

        return app.Run(args);
    }

    /// <summary>
    /// Launch interactive terminal UI for mode selection.
    /// Runs in a loop until user selects Exit.
    /// </summary>
    private static int RunInteractiveMode()
    {
        var running = true;
        var lastExitCode = 0;

        while (running)
        {
            AnsiConsole.Clear();
            ShowBanner();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an option:[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "1. Backtest Management - Sessions & Simulation",
                        "2. Paper Trading - IBKR paper trading",
                        "3. Live Trading - IBKR live trading",
                        "4. Configuration - View/edit settings",
                        "5. Data Management - Download market data",
                        "6. Exit"
                    }));

            var choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    lastExitCode = ShowBacktestMenu();
                    break;
                case "2":
                    lastExitCode = ExecuteMode("paper");
                    WaitForKeyPress();
                    break;
                case "3":
                    lastExitCode = ExecuteMode("live");
                    WaitForKeyPress();
                    break;
                case "4":
                    lastExitCode = ShowConfigurationMenu();
                    break;
                case "5":
                    lastExitCode = ShowDataManagementMenu();
                    break;
                case "6":
                    running = false;
                    break;
            }
        }

        AnsiConsole.MarkupLine("[grey]Goodbye![/]");
        return lastExitCode;
    }

    private static void ShowBanner()
    {
        AnsiConsole.Write(
            new FigletText("Alaris")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[grey]Quantitative Trading System[/]");
        AnsiConsole.WriteLine();
    }

    private static void WaitForKeyPress()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    private static int ExecuteMode(string mode)
    {
        AnsiConsole.MarkupLine($"[yellow]Starting {Markup.Escape(mode)} mode...[/]");
        return Main(new[] { "run", "--mode", mode });
    }

    private static int ShowConfigurationMenu()
    {
        var running = true;

        while (running)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold blue]Configuration[/]");
            AnsiConsole.WriteLine();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an option:[/]")
                    .AddChoices(new[]
                    {
                        "1. View current configuration",
                        "2. Back to main menu"
                    }));

            var choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    Main(new[] { "config", "show" });
                    WaitForKeyPress();
                    break;
                case "2":
                    running = false;
                    break;
            }
        }

        return 0;
    }

    private static int ShowDataManagementMenu()
    {
        var running = true;

        while (running)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold blue]Data Management[/]");
            AnsiConsole.WriteLine();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an option:[/]")
                    .AddChoices(new[]
                    {
                        "1. Download equity data from Polygon",
                        "2. Download option data from Polygon",
                        "3. List existing data",
                        "4. Check data status",
                        "5. Back to main menu"
                    }));

            var choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    DownloadDataInteractive("equity");
                    WaitForKeyPress();
                    break;
                case "2":
                    DownloadDataInteractive("option");
                    WaitForKeyPress();
                    break;
                case "3":
                    Main(new[] { "data", "list" });
                    WaitForKeyPress();
                    break;
                case "4":
                    Main(new[] { "data", "status" });
                    WaitForKeyPress();
                    break;
                case "5":
                    running = false;
                    break;
            }
        }

        return 0;
    }

    private static int DownloadDataInteractive(string dataType)
    {
        AnsiConsole.MarkupLine($"[yellow]Download {Markup.Escape(dataType)} data[/]");
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
                .AddChoices(new[] { "daily", "hour", "minute" }));

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

    private static int ShowBacktestMenu()
    {
        var running = true;
        var exitCode = 0;

        while (running)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold blue]Backtest Management[/]");
            AnsiConsole.WriteLine();

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an option:[/]")
                    .AddChoices(new[]
                    {
                        "1. Create New Session",
                        "2. List All Sessions",
                        "3. Run Backtest Session",
                        "4. Delete Session",
                        "5. View Session Details",
                        "6. Back to Main Menu"
                    }));

            var choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    exitCode = CreateSessionInteractive();
                    WaitForKeyPress();
                    break;
                case "2":
                    // Use CLI list command
                    Main(new[] { "backtest", "list" });
                    WaitForKeyPress();
                    break;
                case "3":
                    exitCode = RunSessionInteractive();
                    WaitForKeyPress();
                    break;
                case "4":
                    exitCode = DeleteSessionInteractive();
                    WaitForKeyPress();
                    break;
                case "5":
                    exitCode = ViewSessionInteractive();
                    WaitForKeyPress();
                    break;
                case "6":
                    running = false;
                    break;
            }
        }
        return exitCode;
    }

    private static int CreateSessionInteractive()
    {
        AnsiConsole.MarkupLine("[yellow]Create New Backtest Session[/]");
        
        var start = AnsiConsole.Ask<string>("[green]Start Date[/] (YYYY-MM-DD):", "2023-01-01");
        var end = AnsiConsole.Ask<string>("[green]End Date[/] (YYYY-MM-DD):", "2023-06-30");
        var symbols = AnsiConsole.Ask<string>("[green]Symbols[/] (comma-separated):", "SPY,QQQ");

        var args = new[] { "backtest", "create", "--start", start, "--end", end, "--symbols", symbols };
        return Main(args);
    }

    private static int RunSessionInteractive()
    {
        var sessionId = SelectSession("Run Session");
        if (string.IsNullOrEmpty(sessionId)) return 0;
        
        return Main(new[] { "backtest", "run", sessionId });
    }

    private static int DeleteSessionInteractive()
    {
        var sessionId = SelectSession("Delete Session");
        if (string.IsNullOrEmpty(sessionId)) return 0;

        return Main(new[] { "backtest", "delete", sessionId });
    }

    private static int ViewSessionInteractive()
    {
        var sessionId = SelectSession("View Session");
        if (string.IsNullOrEmpty(sessionId)) return 0;

        return Main(new[] { "backtest", "view", sessionId });
    }

    private static string? SelectSession(string actionName)
    {
        AnsiConsole.MarkupLine($"[yellow]Select Session to {actionName}[/]");
        
        // Use Service directly to fetch sessions
        var service = new APsv001A(); // Default paths
        var sessions = service.ListAsync().Result; // Sync wait for TUI

        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No sessions found.[/]");
            return null;
        }

        var choices = sessions.Select(s => $"{s.SessionId} ({s.StartDate:yyyy-MM-dd} to {s.EndDate:yyyy-MM-dd}) [[{Markup.Escape(s.Status.ToString())}]]").ToList();
        choices.Add("Cancel");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Choose a session:[/]")
                .PageSize(15)
                .AddChoices(choices));

        if (selection == "Cancel") return null;

        return selection.Split(' ')[0]; // Return SessionId
    }
}
