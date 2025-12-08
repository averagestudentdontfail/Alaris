// =============================================================================
// APcm005A.cs - Backtest Command Group
// Component: APcm005A | Category: Commands | Variant: A (Primary)
// =============================================================================
// Implements 'alaris backtest create|run|list|delete|view' commands.
// Manages session-based backtest infrastructure.
// =============================================================================

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alaris.Application.Model;
using Alaris.Application.Service;
using Spectre.Console;
using Spectre.Console.Cli;

using Alaris.Data.Provider.Polygon;
using Alaris.Data.Provider.SEC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Alaris.Application.Command;

// =============================================================================
// Create Command
// =============================================================================

/// <summary>
/// Settings for backtest create command.
/// </summary>
public sealed class BacktestCreateSettings : CommandSettings
{
    [CommandOption("-s|--start <DATE>")]
    [Description("Backtest start date (YYYY-MM-DD)")]
    public required string StartDate { get; init; }

    [CommandOption("-e|--end <DATE>")]
    [Description("Backtest end date (YYYY-MM-DD)")]
    public required string EndDate { get; init; }

    [CommandOption("--symbols <SYMBOLS>")]
    [Description("Comma-separated list of symbols (optional)")]
    public string? Symbols { get; init; }

    [CommandOption("--skip-download")]
    [Description("Skip data download (useful if data already exists)")]
    [DefaultValue(false)]
    public bool SkipDownload { get; init; }
}

/// <summary>
/// Creates a new backtest session.
/// </summary>
public sealed class BacktestCreateCommand : AsyncCommand<BacktestCreateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestCreateSettings settings)
    {
        if (!DateTime.TryParse(settings.StartDate, out var startDate))
        {
            AnsiConsole.MarkupLine("[red]Invalid start date format. Use YYYY-MM-DD[/]");
            return 1;
        }

        if (!DateTime.TryParse(settings.EndDate, out var endDate))
        {
            AnsiConsole.MarkupLine("[red]Invalid end date format. Use YYYY-MM-DD[/]");
            return 1;
        }

        var symbols = settings.Symbols?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var service = new APsv001A();

        try
        {
            APmd001A session = null!;
            
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("[yellow]Creating backtest session...[/]", async ctx =>
                {
                    ctx.Status("[green]Generating session ID...[/]");
                    session = await service.CreateAsync(startDate, endDate, symbols);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Session created: [bold]{session.SessionId}[/]");
            AnsiConsole.MarkupLine($"  Path: {session.SessionPath}");
            AnsiConsole.MarkupLine($"  Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            if (symbols?.Length > 0)
            {
                AnsiConsole.MarkupLine($"  Symbols: {string.Join(", ", symbols)}");
            }

            if (!settings.SkipDownload)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Initiating data download...[/]");
                
                using var dataService = DependencyFactory.CreateAPsv002A();
                var targets = symbols?.Length > 0 ? symbols : new string[] { "SPY", "QQQ", "IWM", "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "TSLA", "META" }; // Default universe if none specified
                
                await dataService.DownloadEquityDataAsync(service.GetDataPath(session.SessionId), targets, startDate, endDate);
                
                // Update status to Ready
                await service.UpdateAsync(session with { Status = SessionStatus.Ready });
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Next steps:[/]");
            if (settings.SkipDownload)
            {
                AnsiConsole.MarkupLine($"  1. Download data: [cyan]alaris backtest prepare {session.SessionId}[/]");
            }
            AnsiConsole.MarkupLine($"  2. Run backtest:  [cyan]alaris backtest run {session.SessionId}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error creating session: {ex.Message}[/]");
            return 1;
        }
    }
}

// =============================================================================
// Prepare Command
// =============================================================================

public sealed class BacktestPrepareSettings : CommandSettings
{
    [CommandArgument(0, "<SESSION_ID>")]
    [Description("Session ID to prepare")]
    public required string SessionId { get; init; }
}

public sealed class BacktestPrepareCommand : AsyncCommand<BacktestPrepareSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestPrepareSettings settings)
    {
        var service = new APsv001A();
        var session = await service.GetAsync(settings.SessionId);

        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]Session not found: {settings.SessionId}[/]");
            return 1;
        }

        using var dataService = DependencyFactory.CreateAPsv002A();
        var targets = session.Symbols.Count > 0 
            ? session.Symbols 
            : new List<string> { "SPY", "QQQ", "IWM", "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "TSLA", "META" };

        AnsiConsole.MarkupLine($"[yellow]Downloading data for session {session.SessionId}...[/]");
        await dataService.DownloadEquityDataAsync(service.GetDataPath(session.SessionId), targets, session.StartDate, session.EndDate);
        
        await service.UpdateAsync(session with { Status = SessionStatus.Ready });
        AnsiConsole.MarkupLine($"[green]✓[/] Session {session.SessionId} is ready.");
        
        return 0;
    }
}

internal static class DependencyFactory
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Services persist for command duration")]
    public static APsv002A CreateAPsv002A()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("config.json", optional: true)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddJsonFile("appsettings.local.jsonc", optional: true)
            .Build();

        var httpClient = new HttpClient();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        var polygonClient = new PolygonApiClient(
            httpClient, 
            config, 
            loggerFactory.CreateLogger<PolygonApiClient>());

        var secClient = new SecEdgarProvider(
            httpClient,
            loggerFactory.CreateLogger<SecEdgarProvider>());

        return new APsv002A(
            polygonClient, 
            secClient,
            loggerFactory.CreateLogger<APsv002A>());
    }
}

// =============================================================================
// Run Command
// =============================================================================

/// <summary>
/// Settings for backtest run command.
/// </summary>
public sealed class BacktestRunSettings : CommandSettings
{
    [CommandArgument(0, "[SESSION_ID]")]
    [Description("Session ID to run (e.g., BT001A-20230601-20230630)")]
    public string? SessionId { get; init; }

    [CommandOption("--latest")]
    [Description("Run the most recently created session")]
    [DefaultValue(false)]
    public bool Latest { get; init; }
}

/// <summary>
/// Runs a backtest session.
/// </summary>
public sealed class BacktestRunCommand : AsyncCommand<BacktestRunSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestRunSettings settings)
    {
        var service = new APsv001A();

        string? sessionId = settings.SessionId;

        if (settings.Latest || string.IsNullOrEmpty(sessionId))
        {
            var sessions = await service.ListAsync();
            var latest = sessions.Count > 0 ? sessions[0] : null;

            if (latest == null)
            {
                AnsiConsole.MarkupLine("[red]No sessions found. Create one with:[/] [cyan]alaris backtest create[/]");
                return 1;
            }

            sessionId = latest.SessionId;
            AnsiConsole.MarkupLine($"[grey]Using latest session: {sessionId}[/]");
        }

        var session = await service.GetAsync(sessionId);
        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]Session not found: {sessionId}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]Running session:[/] {session.SessionId}");
        AnsiConsole.MarkupLine($"[blue]Date Range:[/] {session.StartDate:yyyy-MM-dd} to {session.EndDate:yyyy-MM-dd}");
        AnsiConsole.WriteLine();

        // Update status to Running
        await service.UpdateAsync(session with { Status = SessionStatus.Running });

        // Execute LEAN with session-specific paths
        var exitCode = await ExecuteLeanForSession(session, service);

        // Update status based on result
        var finalStatus = exitCode == 0 ? SessionStatus.Completed : SessionStatus.Failed;
        await service.UpdateAsync(session with 
        { 
            Status = finalStatus, 
            ExitCode = exitCode 
        });

        if (exitCode == 0)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Session completed successfully");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Session failed with exit code {exitCode}");
        }

        return exitCode;
    }

    private static async Task<int> ExecuteLeanForSession(APmd001A session, APsv001A service)
    {
        // Find config and launcher
        var configPath = FindConfigPath();
        var launcherPath = FindLeanLauncher();

        if (configPath == null || launcherPath == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find config.json or LEAN launcher[/]");
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{launcherPath}\"",
            WorkingDirectory = System.IO.Path.GetDirectoryName(configPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Set session-specific environment variables
        psi.Environment["QC_ENVIRONMENT"] = "backtesting";
        // Inject data folder override for LEAN engine to pick up session data automatically
        psi.Environment["QC_DATA_FOLDER"] = service.GetDataPath(session.SessionId);
        // Inject static universe symbols for STUN001B to bypass universe file requirement
        psi.Environment["ALARIS_SESSION_SYMBOLS"] = string.Join(",", session.Symbols);

        psi.Environment["ALARIS_SESSION_ID"] = session.SessionId;
        psi.Environment["ALARIS_SESSION_PATH"] = session.SessionPath;
        psi.Environment["ALARIS_SESSION_DATA"] = service.GetDataPath(session.SessionId);
        psi.Environment["ALARIS_SESSION_RESULTS"] = service.GetResultsPath(session.SessionId);
        psi.Environment["ALARIS_BACKTEST_STARTDATE"] = session.StartDate.ToString("yyyy-MM-dd");
        psi.Environment["ALARIS_BACKTEST_ENDDATE"] = session.EndDate.ToString("yyyy-MM-dd");

        using var process = Process.Start(psi);
        if (process == null)
        {
            return 1;
        }

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) Console.WriteLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                var escaped = Markup.Escape(e.Data);
                AnsiConsole.MarkupLine($"[red]{escaped}[/]");
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static string? FindConfigPath()
    {
        var paths = new[] { "config.json", "../config.json", "../../config.json" };
        return paths.FirstOrDefault(File.Exists) is string p ? System.IO.Path.GetFullPath(p) : null;
    }

    private static string? FindLeanLauncher()
    {
        var paths = new[] 
        { 
            "Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj",
            "../Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj",
            "../../Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj"
        };
        return paths.FirstOrDefault(File.Exists) is string p ? System.IO.Path.GetFullPath(p) : null;
    }
}

// =============================================================================
// List Command
// =============================================================================

/// <summary>
/// Settings for backtest list command.
/// </summary>
public sealed class BacktestListSettings : CommandSettings
{
    [CommandOption("--all")]
    [Description("Show all sessions including deleted")]
    [DefaultValue(false)]
    public bool All { get; init; }
}

/// <summary>
/// Lists all backtest sessions.
/// </summary>
public sealed class BacktestListCommand : AsyncCommand<BacktestListSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestListSettings settings)
    {
        var service = new APsv001A();
        var sessions = await service.ListAsync();

        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No backtest sessions found.[/]");
            AnsiConsole.MarkupLine("Create one with: [cyan]alaris backtest create --start YYYY-MM-DD --end YYYY-MM-DD[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Session ID[/]")
            .AddColumn("[bold]Date Range[/]")
            .AddColumn("[bold]Status[/]")
            .AddColumn("[bold]Created[/]")
            .AddColumn("[bold]Symbols[/]");

        foreach (var session in sessions)
        {
            var statusColor = session.Status switch
            {
                SessionStatus.Completed => "green",
                SessionStatus.Failed => "red",
                SessionStatus.Running => "yellow",
                SessionStatus.Ready => "blue",
                _ => "grey"
            };

            table.AddRow(
                $"[bold]{session.SessionId}[/]",
                $"{session.StartDate:yyyy-MM-dd} to {session.EndDate:yyyy-MM-dd}",
                $"[{statusColor}]{session.Status}[/]",
                session.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                session.Symbols.Count > 0 ? session.Symbols.Count.ToString() : "-"
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }
}

// =============================================================================
// Delete Command
// =============================================================================

/// <summary>
/// Settings for backtest delete command.
/// </summary>
public sealed class BacktestDeleteSettings : CommandSettings
{
    [CommandArgument(0, "<SESSION_ID>")]
    [Description("Session ID to delete")]
    public required string SessionId { get; init; }

    [CommandOption("-f|--force")]
    [Description("Skip confirmation prompt")]
    [DefaultValue(false)]
    public bool Force { get; init; }
}

/// <summary>
/// Deletes a backtest session and all its data.
/// </summary>
public sealed class BacktestDeleteCommand : AsyncCommand<BacktestDeleteSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestDeleteSettings settings)
    {
        var service = new APsv001A();
        var session = await service.GetAsync(settings.SessionId);

        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]Session not found: {settings.SessionId}[/]");
            return 1;
        }

        if (!settings.Force)
        {
            var confirm = AnsiConsole.Confirm(
                $"[yellow]Delete session {session.SessionId} and all its data?[/]",
                defaultValue: false);

            if (!confirm)
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return 0;
            }
        }

        try
        {
            await service.DeleteAsync(settings.SessionId);
            AnsiConsole.MarkupLine($"[green]✓[/] Session {settings.SessionId} deleted.");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error deleting session: {ex.Message}[/]");
            return 1;
        }
    }
}

// =============================================================================
// View Command
// =============================================================================

/// <summary>
/// Settings for backtest view command.
/// </summary>
public sealed class BacktestViewSettings : CommandSettings
{
    [CommandArgument(0, "<SESSION_ID>")]
    [Description("Session ID to view")]
    public required string SessionId { get; init; }

    [CommandOption("--logs")]
    [Description("Show logs")]
    [DefaultValue(false)]
    public bool ShowLogs { get; init; }
}

/// <summary>
/// Views details of a backtest session.
/// </summary>
public sealed class BacktestViewCommand : AsyncCommand<BacktestViewSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestViewSettings settings)
    {
        var service = new APsv001A();
        var session = await service.GetAsync(settings.SessionId);

        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]Session not found: {settings.SessionId}[/]");
            return 1;
        }

        // Session details panel
        var panel = new Panel(new Markup(
            $"[bold]Session ID:[/] {session.SessionId}\n" +
            $"[bold]Date Range:[/] {session.StartDate:yyyy-MM-dd} to {session.EndDate:yyyy-MM-dd}\n" +
            $"[bold]Status:[/] {session.Status}\n" +
            $"[bold]Created:[/] {session.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
            $"[bold]Updated:[/] {session.UpdatedAt:yyyy-MM-dd HH:mm:ss}\n" +
            $"[bold]Path:[/] {session.SessionPath}\n" +
            $"[bold]Symbols:[/] {(session.Symbols.Count > 0 ? string.Join(", ", session.Symbols.Take(10)) + (session.Symbols.Count > 10 ? "..." : "") : "(none)")}"
        ))
        {
            Header = new PanelHeader($"[bold blue]{session.SessionId}[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);

        // Statistics if completed
        if (session.Statistics != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Statistics:[/]");
            
            var statsTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn("Metric")
                .AddColumn("Value");

            statsTable.AddRow("Total Orders", session.Statistics.TotalOrders.ToString());
            statsTable.AddRow("Net Profit", $"{session.Statistics.NetProfit:P2}");
            statsTable.AddRow("Sharpe Ratio", session.Statistics.SharpeRatio.ToString("F2"));
            statsTable.AddRow("Max Drawdown", $"{session.Statistics.MaxDrawdown:P2}");
            statsTable.AddRow("Win Rate", $"{session.Statistics.WinRate:P2}");
            statsTable.AddRow("Duration", $"{session.Statistics.DurationSeconds:F1}s");

            AnsiConsole.Write(statsTable);
        }

        // Show logs if requested
        if (settings.ShowLogs)
        {
            var logPath = System.IO.Path.Combine(service.GetResultsPath(session.SessionId), "log.txt");
            if (File.Exists(logPath))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Logs (last 50 lines):[/]");
                var lines = await File.ReadAllLinesAsync(logPath);
                foreach (var line in lines.TakeLast(50))
                {
                    AnsiConsole.WriteLine(line);
                }
            }
        }

        return 0;
    }
}
