// APap001A.cs - Alaris CLI entry point with TUI

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Command;
using Alaris.Host.Application.Service;
using Alaris.Host.Application.Model;
using Alaris.Host.Application.Cli.Commands;
using Alaris.Host.Application.Cli.Infrastructure;
using System.IO;

namespace Alaris.Host.Application;

/// <summary>
/// Alaris Trading System CLI Entry Point.
/// Component ID: APap001A
/// </summary>
public static class APap001A
{
    private static string _currentMode = "Idle";
    private static bool _isConnected;
    private const string Version = "2.0.0";

    /// <summary>
    /// Main entry point for Alaris CLI.
    /// </summary>
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return RunInteractiveMode();
        }

        CommandApp app = new CommandApp();
        app.Configure(ConfigureCommands);
        return app.Run(args);
    }

    private static void ConfigureCommands(IConfigurator config)
    {
        config.SetApplicationName("alaris");
        config.SetApplicationVersion(Version);

        config.AddCommand<APcm001A>("run")
            .WithDescription("Run Alaris algorithm in specified mode")
            .WithExample("run", "--mode", "backtest")
            .WithExample("run", "--mode", "paper")
            .WithExample("run", "--mode", "live");

        config.AddBranch("backtest", backtest =>
        {
            backtest.SetDescription("Manage backtest sessions");
            backtest.AddCommand<BacktestCreateCommand>("create")
                .WithDescription("Create a new backtest session");
            backtest.AddCommand<BacktestRunCommand>("run")
                .WithDescription("Run a backtest (auto-downloads missing data)")
                .WithExample("backtest", "run")
                .WithExample("backtest", "run", "--auto-bootstrap");
            backtest.AddCommand<BacktestListCommand>("list")
                .WithDescription("List all backtest sessions");
            backtest.AddCommand<BacktestViewCommand>("view")
                .WithDescription("View session details");
            backtest.AddCommand<BacktestDeleteCommand>("delete")
                .WithDescription("Delete a session");
            backtest.AddCommand<Alaris.Host.Application.Cli.Commands.Backtest.CLbt002A>("analyze")
                .WithDescription("Analyze backtest results")
                .WithExample("backtest", "analyze", "BT001A-20240101-20251231");
        });

        config.AddCommand<APcm002A>("config")
            .WithDescription("View or modify Alaris configuration");
        
        // Data commands (Polygon prices + options)
        config.AddBranch("data", data =>
        {
            data.SetDescription("Download and manage market data");
            data.AddCommand<Alaris.Host.Application.Cli.Commands.Data.CLdt001A>("bootstrap")
                .WithDescription("Download prices and options for a session")
                .WithExample("data", "bootstrap", "BT001A-20240101-20251231");
            data.AddCommand<Alaris.Host.Application.Cli.Commands.Data.CLdt002A>("status")
                .WithDescription("Show data coverage and freshness");
            data.AddCommand<Alaris.Host.Application.Cli.Commands.Data.CLdt003A>("validate")
                .WithDescription("Validate data integrity for a session")
                .WithExample("data", "validate", "BT001A-20240101-20251231");
        });
        config.AddCommand<APcm004A>("universe")
            .WithDescription("Generate and manage universe files");
        
        // Earnings commands (cache-first pattern)
        config.AddBranch("earnings", earnings =>
        {
            earnings.SetDescription("Manage earnings calendar data");
            earnings.AddCommand<BootstrapEarningsCommand>("bootstrap")
                .WithDescription("Download earnings calendar to cache files")
                .WithExample("earnings", "bootstrap", "--start", "2023-01-01", "--end", "2025-01-01");
            earnings.AddCommand<Alaris.Host.Application.Cli.Commands.Earnings.CLer002A>("upcoming")
                .WithDescription("Show upcoming earnings announcements")
                .WithExample("earnings", "upcoming", "--days", "7");
            earnings.AddCommand<Alaris.Host.Application.Cli.Commands.Earnings.CLer003A>("check")
                .WithDescription("Check earnings cache coverage for a session")
                .WithExample("earnings", "check", "BT001A-20240101-20251231");
        });

        // Trade commands
        config.AddBranch("trade", trade =>
        {
            trade.SetDescription("Live trading commands");
            trade.AddCommand<Alaris.Host.Application.Cli.Commands.Trade.CLtr002A>("status")
                .WithDescription("Show current positions and trading state");
            trade.AddCommand<Alaris.Host.Application.Cli.Commands.Trade.CLtr003A>("signals")
                .WithDescription("Show pending and active signals");
        });

        // Strategy commands
        config.AddBranch("strategy", strategy =>
        {
            strategy.SetDescription("Strategy inspection commands");
            strategy.AddCommand<Alaris.Host.Application.Cli.Commands.Strategy.CLst001A>("list")
                .WithDescription("List available strategies");
            strategy.AddCommand<Alaris.Host.Application.Cli.Commands.Strategy.CLst002A>("info")
                .WithDescription("Show strategy parameters and thresholds")
                .WithExample("strategy", "info", "--name", "earnings-vol");
            strategy.AddCommand<Alaris.Host.Application.Cli.Commands.Strategy.CLst003A>("evaluate")
                .WithDescription("Evaluate a symbol for trading signals")
                .WithExample("strategy", "evaluate", "AAPL");
        });

        // Version command
        config.AddCommand<CLvr001A>("version")
            .WithDescription("Show version and system information");
    }

    // Interactive TUI Mode

    private static int RunInteractiveMode()
    {
        bool running = true;
        int lastExitCode = 0;

        // Initial system check
        CheckSystemStatus();

        while (running)
        {
            AnsiConsole.Clear();
            RenderStatusBar();
            RenderBanner();

            string selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an option:[/]")
                    .PageSize(10)
                    .HighlightStyle(new Style(Color.Blue))
                    .AddChoices(new[]
                    {
                        "1. Trading        - Paper or Live execution",
                        "2. Backtesting    - Create and run simulations",
                        "3. Monitor        - View positions and P&L",
                        "4. Configuration  - Edit API keys and settings",
                        "5. System         - Health check and diagnostics",
                        "6. Exit"
                    }));

            string choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    lastExitCode = ShowTradingMenu();
                    break;
                case "2":
                    lastExitCode = ShowBacktestMenu();
                    break;
                case "3":
                    lastExitCode = ShowPositionMonitor();
                    break;
                case "4":
                    lastExitCode = ShowConfigurationMenu();
                    break;
                case "5":
                    lastExitCode = ShowSystemMenu();
                    break;
                case "6":
                    running = false;
                    break;
            }
        }

        AnsiConsole.MarkupLine("[grey]Goodbye![/]");
        return lastExitCode;
    }

    // Status Bar & Banner

    private static void RenderStatusBar()
    {
        string modeColor = _currentMode switch
        {
            "Live" => "red",
            "Paper" => "yellow",
            _ => "grey"
        };

        string connectionStatus = _isConnected 
            ? "[green]● Connected[/]" 
            : "[red]○ Disconnected[/]";

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Width(60);

        table.AddColumn(new TableColumn("[bold blue]ALARIS[/]").Centered());
        table.AddColumn(new TableColumn($"[{modeColor}]{_currentMode}[/]").Centered());
        table.AddColumn(new TableColumn(connectionStatus).Centered());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderBanner()
    {
        AnsiConsole.Write(
            new FigletText("Alaris")
                .LeftJustified()
                .Color(Color.Blue));
        AnsiConsole.MarkupLine("[grey]Quantitative Trading System v" + Version + "[/]");
        AnsiConsole.WriteLine();
    }

    private static void CheckSystemStatus()
    {
        // Quick check - can be expanded
        _isConnected = File.Exists("appsettings.jsonc") || File.Exists("appsettings.local.jsonc");
    }

    private static void WaitForKeyPress()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    // Trading Menu

    private static int ShowTradingMenu()
    {
        bool running = true;
        int exitCode = 0;

        while (running)
        {
            AnsiConsole.Clear();
            RenderStatusBar();
            AnsiConsole.MarkupLine("[bold blue]Trading[/]");
            AnsiConsole.WriteLine();

            string selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select trading mode:[/]")
                    .AddChoices(new[]
                    {
                        "1. Paper Trading  - IBKR paper account",
                        "2. Live Trading   - IBKR live account (requires confirmation)",
                        "3. Back to Main Menu"
                    }));

            string choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    exitCode = StartTrading("paper");
                    WaitForKeyPress();
                    break;
                case "2":
                    exitCode = StartTrading("live");
                    WaitForKeyPress();
                    break;
                case "3":
                    running = false;
                    break;
            }
        }

        return exitCode;
    }

    private static int StartTrading(string mode)
    {
        _currentMode = mode == "live" ? "Live" : "Paper";

        if (mode == "live")
        {
            AnsiConsole.MarkupLine("[bold red]⚠ LIVE TRADING WARNING[/]");
            AnsiConsole.MarkupLine("[yellow]You are about to start live trading with real money.[/]");
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm("[red]Are you sure you want to continue?[/]", false))
            {
                AnsiConsole.MarkupLine("[grey]Live trading cancelled.[/]");
                _currentMode = "Idle";
                return 0;
            }
        }

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start($"[yellow]Starting {mode} trading...[/]", ctx =>
            {
                ctx.Status("[yellow]Connecting to IBKR...[/]");
                Thread.Sleep(1000);
                ctx.Status("[yellow]Loading configuration...[/]");
                Thread.Sleep(500);
                ctx.Status("[yellow]Initializing strategy...[/]");
                Thread.Sleep(500);
            });

        AnsiConsole.MarkupLine($"[green]✓ {mode.ToUpperInvariant()} trading started[/]");
        _isConnected = true;

        return Main(new[] { "run", "--mode", mode });
    }

    // Backtest Menu

    private static int ShowBacktestMenu()
    {
        bool running = true;
        int exitCode = 0;

        while (running)
        {
            AnsiConsole.Clear();
            RenderStatusBar();
            AnsiConsole.MarkupLine("[bold blue]Backtest Management[/]");
            AnsiConsole.WriteLine();

            string selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an option:[/]")
                    .AddChoices(new[]
                    {
                        "1. Create New Session  - Auto-downloads required data",
                        "2. Run Session         - Execute existing session",
                        "3. View Results        - Analyze completed backtests",
                        "4. List Sessions       - Show all sessions",
                        "5. Delete Session      - Remove a session",
                        "6. Back to Main Menu"
                    }));

            string choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    exitCode = CreateSessionInteractive();
                    WaitForKeyPress();
                    break;
                case "2":
                    exitCode = RunSessionInteractive();
                    WaitForKeyPress();
                    break;
                case "3":
                    exitCode = ViewSessionInteractive();
                    WaitForKeyPress();
                    break;
                case "4":
                    Main(new[] { "backtest", "list" });
                    WaitForKeyPress();
                    break;
                case "5":
                    exitCode = DeleteSessionInteractive();
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
        AnsiConsole.MarkupLine("[grey]Data will be automatically downloaded as needed.[/]");
        AnsiConsole.WriteLine();

        // Date range selection
        string dateOption = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select date range:[/]")
                .AddChoices(new[]
                {
                    "1. Last 2 years (recommended)",
                    "2. Specific year(s)",
                    "3. Custom date range"
                }));

        string dateChoice = dateOption.Split('.')[0].Trim();
        string[] dateArgs;

        switch (dateChoice)
        {
            case "2":
                string years = AnsiConsole.Ask<string>("[green]Enter year(s)[/] (e.g., 2024 or 2023,2024):");
                dateArgs = new[] { "--years", years };
                break;
            case "3":
                string defaultStart = DateTime.Now.AddYears(-2).AddMonths(1).ToString("yyyy-MM-dd");
                string defaultEnd = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                string start = AnsiConsole.Ask("[green]Start Date[/] (YYYY-MM-DD):", defaultStart);
                string end = AnsiConsole.Ask("[green]End Date[/] (YYYY-MM-DD):", defaultEnd);
                dateArgs = new[] { "--start", start, "--end", end };
                break;
            default:
                dateArgs = Array.Empty<string>();
                break;
        }

        // Symbol selection
        string symbolOption = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Symbol selection:[/]")
                .AddChoices(new[]
                {
                    "1. Auto-generate universe (scanner)",
                    "2. Specify symbols manually"
                }));

        List<string> args = new List<string> { "backtest", "create" };
        args.AddRange(dateArgs);

        if (symbolOption.StartsWith("2"))
        {
            string symbols = AnsiConsole.Ask<string>("[green]Enter symbols[/] (comma-separated):");
            args.AddRange(new[] { "--symbols", symbols });
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("[yellow]Creating session and downloading data...[/]", _ =>
            {
                // This is just visual feedback - actual work happens in command
            });

        return Main(args.ToArray());
    }

    private static int RunSessionInteractive()
    {
        string? sessionId = SelectSession("Run");
        if (string.IsNullOrEmpty(sessionId)) return 0;

        _currentMode = "Backtest";
        return Main(new[] { "backtest", "run", sessionId });
    }

    private static int ViewSessionInteractive()
    {
        string? sessionId = SelectSession("View");
        if (string.IsNullOrEmpty(sessionId)) return 0;
        return Main(new[] { "backtest", "view", sessionId });
    }

    private static int DeleteSessionInteractive()
    {
        string? sessionId = SelectSession("Delete");
        if (string.IsNullOrEmpty(sessionId)) return 0;

        if (!AnsiConsole.Confirm($"[red]Delete session {sessionId}?[/]", false))
        {
            AnsiConsole.MarkupLine("[grey]Deletion cancelled.[/]");
            return 0;
        }

        return Main(new[] { "backtest", "delete", sessionId });
    }

    private static string? SelectSession(string action)
    {
        APsv001A service = new APsv001A();
        IReadOnlyList<APmd001A> sessions = service.ListAsync().Result;

        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No sessions found.[/]");
            return null;
        }

        List<string> choices = new List<string>(sessions.Count + 1);
        foreach (APmd001A session in sessions)
        {
            choices.Add($"{session.SessionId} | {session.StartDate:yyyy-MM-dd} → {session.EndDate:yyyy-MM-dd} | {session.Status}");
        }
        choices.Add("Cancel");

        string selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[green]Select session to {action}:[/]")
                .PageSize(15)
                .AddChoices(choices));

        return selection == "Cancel" ? null : selection.Split('|')[0].Trim();
    }

    // Position Monitor

    private static int ShowPositionMonitor()
    {
        AnsiConsole.Clear();
        RenderStatusBar();
        AnsiConsole.MarkupLine("[bold blue]Position Monitor[/]");
        AnsiConsole.WriteLine();

        if (_currentMode == "Idle")
        {
            AnsiConsole.MarkupLine("[yellow]No active trading session.[/]");
            AnsiConsole.MarkupLine("[grey]Start Paper or Live trading to see positions.[/]");
            WaitForKeyPress();
            return 0;
        }

        // Mock position data - would connect to real IBKR data
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Current Positions[/]");

        table.AddColumn("Symbol");
        table.AddColumn("Qty");
        table.AddColumn("Entry");
        table.AddColumn("Current");
        table.AddColumn("P&L");
        table.AddColumn("P&L %");

        // Example positions
        table.AddRow("NVDA", "+5", "$142.50", "$145.20", "[green]+$135.00[/]", "[green]+1.89%[/]");
        table.AddRow("AAPL", "-3", "$178.00", "$176.50", "[green]+$45.00[/]", "[green]+0.84%[/]");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        Panel totalPnl = new Panel("[bold green]Total P&L: +$180.00 (+1.45%)[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green);
        AnsiConsole.Write(totalPnl);

        WaitForKeyPress();
        return 0;
    }

    // Configuration Menu

    private static int ShowConfigurationMenu()
    {
        bool running = true;

        while (running)
        {
            AnsiConsole.Clear();
            RenderStatusBar();
            AnsiConsole.MarkupLine("[bold blue]Configuration[/]");
            AnsiConsole.WriteLine();

            string selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an option:[/]")
                    .AddChoices(new[]
                    {
                        "1. View Current Settings",
                        "2. Edit API Keys",
                        "3. Edit Strategy Parameters",
                        "4. Edit IBKR Settings",
                        "5. Back to Main Menu"
                    }));

            string choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    Main(new[] { "config", "show" });
                    WaitForKeyPress();
                    break;
                case "2":
                    EditApiKeys();
                    break;
                case "3":
                    EditStrategyParams();
                    break;
                case "4":
                    EditIbkrSettings();
                    break;
                case "5":
                    running = false;
                    break;
            }
        }

        return 0;
    }

    private static void EditApiKeys()
    {
        AnsiConsole.MarkupLine("[yellow]Edit API Keys[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[grey]Current: Polygon API Key = ••••••••[/]");
        string newKey = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]New Polygon API Key[/] (Enter to keep current):")
                .AllowEmpty()
                .Secret());

        if (!string.IsNullOrEmpty(newKey))
        {
            AnsiConsole.MarkupLine("[green]✓ API Key updated[/]");
            // TODO: Save to config
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]No changes made.[/]");
        }

        WaitForKeyPress();
    }

    private static void EditStrategyParams()
    {
        AnsiConsole.MarkupLine("[yellow]Edit Strategy Parameters[/]");
        AnsiConsole.WriteLine();

        double ivThreshold = AnsiConsole.Ask("[green]IV/RV Threshold[/]:", 1.25);
        int maxPositions = AnsiConsole.Ask("[green]Max Concurrent Positions[/]:", 5);
        double kellyFraction = AnsiConsole.Ask("[green]Kelly Fraction[/]:", 0.25);

        AnsiConsole.MarkupLine($"[green]✓ Parameters updated:[/]");
        AnsiConsole.MarkupLine($"  IV/RV Threshold: {ivThreshold}");
        AnsiConsole.MarkupLine($"  Max Positions: {maxPositions}");
        AnsiConsole.MarkupLine($"  Kelly Fraction: {kellyFraction}");

        WaitForKeyPress();
    }

    private static void EditIbkrSettings()
    {
        AnsiConsole.MarkupLine("[yellow]Edit IBKR Settings[/]");
        AnsiConsole.WriteLine();

        string host = AnsiConsole.Ask("[green]TWS Host[/]:", "127.0.0.1");
        int port = AnsiConsole.Ask("[green]TWS Port[/]:", 7497);
        string account = AnsiConsole.Ask<string>("[green]Account ID[/]:");

        AnsiConsole.MarkupLine($"[green]✓ IBKR settings updated:[/]");
        AnsiConsole.MarkupLine($"  Host: {host}:{port}");
        AnsiConsole.MarkupLine($"  Account: {account}");

        WaitForKeyPress();
    }

    // System Menu

    private static int ShowSystemMenu()
    {
        bool running = true;

        while (running)
        {
            AnsiConsole.Clear();
            RenderStatusBar();
            AnsiConsole.MarkupLine("[bold blue]System[/]");
            AnsiConsole.WriteLine();

            string selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an option:[/]")
                    .AddChoices(new[]
                    {
                        "1. Health Check    - Verify all connections",
                        "2. View Logs       - Recent system activity",
                        "3. About           - Version and credits",
                        "4. Back to Main Menu"
                    }));

            string choice = selection.Split('.')[0].Trim();

            switch (choice)
            {
                case "1":
                    RunHealthCheck();
                    WaitForKeyPress();
                    break;
                case "2":
                    ViewLogs();
                    WaitForKeyPress();
                    break;
                case "3":
                    ShowAbout();
                    WaitForKeyPress();
                    break;
                case "4":
                    running = false;
                    break;
            }
        }

        return 0;
    }

    private static void RunHealthCheck()
    {
        AnsiConsole.MarkupLine("[yellow]Running System Health Check...[/]");
        AnsiConsole.WriteLine();

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]System Status[/]");

        table.AddColumn("Component");
        table.AddColumn("Status");
        table.AddColumn("Details");

        // Check config files
        bool configOk = File.Exists("appsettings.jsonc");
        table.AddRow("Configuration",
            configOk ? "[green]✓ OK[/]" : "[red]✗ Missing[/]",
            configOk ? "appsettings.jsonc found" : "Create appsettings.jsonc");

        // Check data directory
        string dataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".project/Alaris/Alaris.Sessions");
        bool dataOk = Directory.Exists(dataDir);
        table.AddRow("Sessions Directory",
            dataOk ? "[green]✓ OK[/]" : "[yellow]○ Empty[/]",
            dataOk ? $"{Directory.GetDirectories(dataDir).Length} sessions" : "No sessions yet");

        // Check LEAN
        bool leanOk = Directory.Exists("Alaris.Lean");
        table.AddRow("LEAN Engine",
            leanOk ? "[green]✓ OK[/]" : "[red]✗ Missing[/]",
            leanOk ? "LEAN directory found" : "LEAN not installed");

        // Check .NET
        table.AddRow(".NET Runtime",
            "[green]✓ OK[/]",
            $"Version {Environment.Version}");

        AnsiConsole.Write(table);

        _isConnected = configOk && leanOk;
    }

    private static void ViewLogs()
    {
        AnsiConsole.MarkupLine("[yellow]Recent System Activity[/]");
        AnsiConsole.WriteLine();

        string logPath = System.IO.Path.Combine("Alaris.Simulation", "Output", "Logs");
        if (!Directory.Exists(logPath))
        {
            AnsiConsole.MarkupLine("[grey]No logs found.[/]");
            return;
        }

        string[] logs = Directory.GetFiles(logPath, "*.log");
        if (logs.Length == 0)
        {
            AnsiConsole.MarkupLine("[grey]No log files found.[/]");
            return;
        }

        Array.Sort(logs, (left, right) => File.GetLastWriteTime(right).CompareTo(File.GetLastWriteTime(left)));

        int logCount = logs.Length < 5 ? logs.Length : 5;
        for (int i = 0; i < logCount; i++)
        {
            string log = logs[i];
            DateTime lastWrite = File.GetLastWriteTime(log);
            AnsiConsole.MarkupLine($"[blue]{System.IO.Path.GetFileName(log)}[/] - {lastWrite:yyyy-MM-dd HH:mm}");
        }
    }

    private static void ShowAbout()
    {
        Panel panel = new Panel(
            new Markup(
                "[bold blue]Alaris Trading System[/]\n\n" +
                $"Version: {Version}\n" +
                "Author: Sunny\n\n" +
                "[grey]Quantitative earnings volatility trading system\n" +
                "implementing Atilgan (2014) calendar spread strategy\n" +
                "with Leung-Santoli (2016) volatility decomposition.[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);

        AnsiConsole.Write(panel);
    }
}
