// APcm005A.cs - Backtest command group (create/run/list/delete/view)

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Alaris.Host.Application.Model;
using Alaris.Host.Application.Service;
using Spectre.Console;
using Spectre.Console.Cli;

using Alaris.Infrastructure.Data.Provider.Polygon;
using Alaris.Infrastructure.Data.Provider.Nasdaq;
using Alaris.Infrastructure.Data.Provider.Treasury;
using Alaris.Infrastructure.Data.Http.Contracts;
using Alaris.Infrastructure.Protocol.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using Refit;

namespace Alaris.Host.Application.Command;

// Create Command

/// <summary>
/// Settings for backtest create command.
/// </summary>
public sealed class BacktestCreateSettings : CommandSettings
{
    [CommandOption("-s|--start <DATE>")]
    [Description("Backtest start date (YYYY-MM-DD). Default: 2 years ago")]
    public string? StartDate { get; init; }

    [CommandOption("-e|--end <DATE>")]
    [Description("Backtest end date (YYYY-MM-DD). Default: yesterday")]
    public string? EndDate { get; init; }

    [CommandOption("-y|--years <YEARS>")]
    [Description("Year(s) to backtest (e.g., 2024 or 2023,2024). Overrides start/end")]
    public string? Years { get; init; }

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
        // Resolve date range: --years > --start/--end > default (2 years)
        var (startDate, endDate) = ResolveDateRange(settings);
        
        if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
        {
            return 1; // Error already printed
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
                
                // Determine symbols: use provided symbols OR run screener
                string[] targets;
                if (symbols?.Length > 0)
                {
                    targets = symbols;
                    AnsiConsole.MarkupLine($"[yellow]Using specified symbols: {string.Join(", ", targets)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Running screener to discover tradeable symbols...[/]");
                    using var screener = DependencyFactory.CreateScreener();
                    var screenedSymbols = await screener.ScreenAsync(startDate, maxSymbols: 50);
                    targets = screenedSymbols.ToArray();
                    AnsiConsole.MarkupLine($"[green]✓[/] Screened {targets.Length} symbols");
                    
                    // Update session with screened symbols
                    session = session with { Symbols = screenedSymbols };
                    await service.UpdateAsync(session);
                }
                
                // Build requirements model for unified bootstrap
                var requirements = new Alaris.Core.Model.STDT010A
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Symbols = targets.ToList()
                };
                
                AnsiConsole.MarkupLine("[yellow]Starting unified data bootstrap...[/]");
                AnsiConsole.MarkupLine($"[grey]  Requirements: {requirements.GetSummary()}[/]");
                
                using var dataService = DependencyFactory.CreateAPsv002A();
                var bootstrapReport = await dataService.BootstrapSessionDataAsync(
                    requirements,
                    service.GetDataPath(session.SessionId),
                    CancellationToken.None);
                
                if (!bootstrapReport.Success)
                {
                    AnsiConsole.MarkupLine($"[red]Bootstrap failed: {bootstrapReport.ErrorMessage}[/]");
                    return 1;
                }
                
                AnsiConsole.MarkupLine($"[green]✓[/] {bootstrapReport.GetSummary()}");
                
                // Update status to Ready
                await service.UpdateAsync(session with { Status = SessionStatus.Ready });
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Next step:[/]");
            AnsiConsole.MarkupLine($"  Run backtest: [cyan]alaris backtest run {session.SessionId}[/]");
            if (settings.SkipDownload)
            {
                AnsiConsole.MarkupLine("[grey]  (Data will be auto-downloaded when running)[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error creating session: {ex.Message}[/]");
            return 1;
        }
    }

    /// <summary>
    /// Resolves date range from command settings.
    /// Priority: --years > --start/--end > default (2 years ending yesterday)
    /// </summary>
    private static (DateTime start, DateTime end) ResolveDateRange(BacktestCreateSettings settings)
    {
        // Option 1: --years specified (e.g., "2024" or "2023,2024")
        if (!string.IsNullOrEmpty(settings.Years))
        {
            var yearStrings = settings.Years.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var years = new List<int>();
            
            foreach (var yearStr in yearStrings)
            {
                if (int.TryParse(yearStr, out var year) && year >= 2000 && year <= 2100)
                {
                    years.Add(year);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Invalid year: {yearStr}. Use format: 2024 or 2023,2024[/]");
                    return (DateTime.MinValue, DateTime.MinValue);
                }
            }

            if (years.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No valid years specified[/]");
                return (DateTime.MinValue, DateTime.MinValue);
            }

            var minYear = years.Min();
            var maxYear = years.Max();
            var startDate = new DateTime(minYear, 1, 1);
            var endDate = maxYear == DateTime.Now.Year 
                ? DateTime.Now.Date.AddDays(-1)  // Current year: end yesterday
                : new DateTime(maxYear, 12, 31); // Past years: end Dec 31
            
            AnsiConsole.MarkupLine($"[cyan]Using year range: {minYear}-{maxYear}[/]");
            return (startDate, endDate);
        }

        // Option 2: Explicit --start/--end
        if (!string.IsNullOrEmpty(settings.StartDate) || !string.IsNullOrEmpty(settings.EndDate))
        {
            if (string.IsNullOrEmpty(settings.StartDate) || !DateTime.TryParse(settings.StartDate, out var start))
            {
                AnsiConsole.MarkupLine("[red]Invalid or missing start date. Use YYYY-MM-DD format[/]");
                return (DateTime.MinValue, DateTime.MinValue);
            }

            if (string.IsNullOrEmpty(settings.EndDate) || !DateTime.TryParse(settings.EndDate, out var end))
            {
                AnsiConsole.MarkupLine("[red]Invalid or missing end date. Use YYYY-MM-DD format[/]");
                return (DateTime.MinValue, DateTime.MinValue);
            }

            return (start, end);
        }

        // Option 3: Default - 2 years ending yesterday (with 1-month buffer for options data)
        // Polygon's Options Starter plan has a 2-year limit, but options aggregate data
        // at the boundary often returns 403. Using 23 months ensures reliable data access.
        var yesterday = DateTime.Now.Date.AddDays(-1);
        var twoYearsAgo = yesterday.AddYears(-2).AddMonths(1); // 23 months instead of 24
        AnsiConsole.MarkupLine($"[cyan]Using default 2-year range: {twoYearsAgo:yyyy-MM-dd} to {yesterday:yyyy-MM-dd}[/]");
        return (twoYearsAgo, yesterday);
    }
}

// Prepare Command

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

        // Determine symbols: use session symbols OR run screener
        IEnumerable<string> targets;
        if (session.Symbols.Count > 0)
        {
            targets = session.Symbols;
            AnsiConsole.MarkupLine($"[yellow]Using session symbols: {string.Join(", ", targets.Take(10))}...[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Running screener to discover tradeable symbols...[/]");
            using var screener = DependencyFactory.CreateScreener();
            var screenedSymbols = await screener.ScreenAsync(session.StartDate, maxSymbols: 50);
            targets = screenedSymbols;
            AnsiConsole.MarkupLine($"[green]✓[/] Screened {screenedSymbols.Count} symbols");
            
            // Update session with screened symbols
            session = session with { Symbols = screenedSymbols };
            await service.UpdateAsync(session);
        }

        // Build requirements model for unified bootstrap
        var requirements = new Alaris.Core.Model.STDT010A
        {
            StartDate = session.StartDate,
            EndDate = session.EndDate,
            Symbols = targets.ToList()
        };
        
        using var dataService = DependencyFactory.CreateAPsv002A();
        AnsiConsole.MarkupLine($"[yellow]Starting unified bootstrap for session {session.SessionId}...[/]");
        
        var bootstrapReport = await dataService.BootstrapSessionDataAsync(
            requirements,
            service.GetDataPath(session.SessionId),
            CancellationToken.None);
        
        if (!bootstrapReport.Success)
        {
            AnsiConsole.MarkupLine($"[red]Bootstrap failed: {bootstrapReport.ErrorMessage}[/]");
            return 1;
        }
        
        AnsiConsole.MarkupLine($"[green]✓[/] {bootstrapReport.GetSummary()}");
        
        await service.UpdateAsync(session with { Status = SessionStatus.Ready });
        AnsiConsole.MarkupLine($"[green]✓[/] Session {session.SessionId} is ready.");
        
        return 0;
    }
    
    /// <summary>
    /// Helper to get options-required dates from an existing session.
    /// </summary>
    private static IReadOnlyList<DateTime> GetOptionsRequiredDatesFromSession(string sessionDataPath, IEnumerable<string> symbols)
    {
        var dates = new HashSet<DateTime>();
        var nasdaqPath = System.IO.Path.Combine(sessionDataPath, "earnings", "nasdaq");
        
        if (!Directory.Exists(nasdaqPath))
        {
            return Array.Empty<DateTime>();
        }
        
        var symbolSet = new HashSet<string>(symbols, StringComparer.OrdinalIgnoreCase);
        
        foreach (var file in Directory.GetFiles(nasdaqPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                // Simple parsing - look for symbol matches
                foreach (var symbol in symbolSet)
                {
                    if (json.Contains($"\"{symbol}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract date from filename
                        var dateStr = System.IO.Path.GetFileNameWithoutExtension(file);
                        if (DateTime.TryParse(dateStr, out var earningsDate))
                        {
                            // Add evaluation dates (5-7 days before)
                            for (int d = 5; d <= 7; d++)
                            {
                                var evalDate = earningsDate.AddDays(-d);
                                if (evalDate.DayOfWeek != DayOfWeek.Saturday && 
                                    evalDate.DayOfWeek != DayOfWeek.Sunday)
                                {
                                    dates.Add(evalDate);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }
        
        return dates.OrderBy(d => d).ToList();
    }
}

internal static class DependencyFactory
{
    private static IConfiguration? _config;
    private static HttpClient? _httpClient;
    private static ILoggerFactory? _loggerFactory;
    
    private static IConfiguration GetConfig()
    {
        return _config ??= new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("config.json", optional: true)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddJsonFile("appsettings.local.jsonc", optional: true)
            .Build();
    }
    
    private static HttpClient GetHttpClient() => _httpClient ??= new HttpClient();
    
    private static ILoggerFactory GetLoggerFactory() => _loggerFactory ??= 
        LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Services persist for command duration")]
    public static APsv002A CreateAPsv002A()
    {
        var config = GetConfig();
        var loggerFactory = GetLoggerFactory();
        
        // Create Refit clients for each API
        var polygonApi = CreatePolygonApi();
        var nasdaqApi = CreateNasdaqApi();
        var treasuryApi = CreateTreasuryApi();
        
        var polygonClient = new PolygonApiClient(
            polygonApi, 
            config, 
            loggerFactory.CreateLogger<PolygonApiClient>());

        var earningsClient = new NasdaqEarningsProvider(
            nasdaqApi,
            loggerFactory.CreateLogger<NasdaqEarningsProvider>());

        var treasuryClient = new TreasuryDirectRateProvider(
            treasuryApi,
            loggerFactory.CreateLogger<TreasuryDirectRateProvider>());

        return new APsv002A(
            polygonClient, 
            earningsClient,
            treasuryClient,
            loggerFactory.CreateLogger<APsv002A>());
    }
    
    private static IPolygonApi CreatePolygonApi()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.polygon.io") };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alaris/1.0 (Quantitative Trading System)");
        return RestService.For<IPolygonApi>(httpClient);
    }
    
    private static INasdaqCalendarApi CreateNasdaqApi()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.nasdaq.com") };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return RestService.For<INasdaqCalendarApi>(httpClient);
    }
    
    private static ITreasuryDirectApi CreateTreasuryApi()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://www.treasurydirect.gov/TA_WS/securities") };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alaris/1.0 (Quantitative Trading System)");
        return RestService.For<ITreasuryDirectApi>(httpClient);
    }
    
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Services persist for command duration")]
    public static APsv002B CreateScreener()
    {
        return new APsv002B(
            GetHttpClient(),
            GetConfig(),
            GetLoggerFactory().CreateLogger<APsv002B>());
    }
}

// Run Command

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

    [CommandOption("--auto-bootstrap")]
    [Description("Automatically download missing data without prompting")]
    [DefaultValue(false)]
    public bool AutoBootstrap { get; init; }

    [CommandOption("--no-monitor")]
    [Description("Disable live monitoring dashboard (logs only)")]
    [DefaultValue(false)]
    public bool NoMonitor { get; init; }
}
/// <summary>
/// Runs a backtest session.
/// </summary>
public sealed class BacktestRunCommand : AsyncCommand<BacktestRunSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BacktestRunSettings settings)
    {
        // Initialize FSM for deterministic state tracking
        var fsm = PLWF002A.Create();
        fsm.OnTransition += record => 
        {
            if (record.Succeeded)
                AnsiConsole.MarkupLine($"[grey]FSM: {record.FromState} → {record.ToState}[/]");
        };
        
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

        // FSM: Idle → SessionSelected
        fsm.Fire(BacktestEvent.SelectSession);

        AnsiConsole.MarkupLine($"[blue]Running session:[/] {session.SessionId}");
        AnsiConsole.MarkupLine($"[blue]Date Range:[/] {session.StartDate:yyyy-MM-dd} to {session.EndDate:yyyy-MM-dd}");
        AnsiConsole.MarkupLine($"[blue]Symbols:[/] {string.Join(", ", session.Symbols.Take(10))}{(session.Symbols.Count > 10 ? "..." : "")}");
        AnsiConsole.WriteLine();

        // FSM: SessionSelected → DataChecking
        fsm.Fire(BacktestEvent.CheckData);

        var dataPath = service.GetDataPath(session.SessionId);
        var (pricesMissing, earningsMissing, optionsMissing) = CheckDataAvailability(dataPath, session);

        if (pricesMissing || earningsMissing || optionsMissing)
        {
            // FSM: DataChecking → DataBootstrapping
            fsm.Fire(BacktestEvent.DataMissing);
            
            AnsiConsole.MarkupLine("[yellow]⚠ Missing data detected:[/]");
            if (pricesMissing) AnsiConsole.MarkupLine("  [grey]• Price data not found[/]");
            if (earningsMissing) AnsiConsole.MarkupLine("  [grey]• Earnings cache empty[/]");
            if (optionsMissing) AnsiConsole.MarkupLine("  [grey]• Options data empty (mandatory)[/]");
            AnsiConsole.WriteLine();

            if (settings.AutoBootstrap)
            {
                try
                {
                    await BootstrapDataAsync(session, service, dataPath);
                    // FSM: DataBootstrapping → DataChecking (re-check after bootstrap)
                    fsm.Fire(BacktestEvent.BootstrapComplete);
                    // FSM: DataChecking → ExecutingLean (data should be ready now)
                    fsm.Fire(BacktestEvent.DataReady);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Bootstrap failed: {ex.Message}[/]");
                    fsm.Fire(BacktestEvent.BootstrapFailed);
                    return 1;
                }
            }
            else
            {
                var download = AnsiConsole.Confirm("Download missing data now?", defaultValue: true);
                if (download)
                {
                    try
                    {
                        await BootstrapDataAsync(session, service, dataPath);
                        fsm.Fire(BacktestEvent.BootstrapComplete);
                        fsm.Fire(BacktestEvent.DataReady);
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Bootstrap failed: {ex.Message}[/]");
                        fsm.Fire(BacktestEvent.BootstrapFailed);
                        return 1;
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Cannot proceed without mandatory market data.[/]");
                    AnsiConsole.MarkupLine("[grey]Strategy requires historical options data with IV for backtesting.[/]");
                    fsm.Fire(BacktestEvent.BootstrapFailed);
                    return 1;
                }
            }
            AnsiConsole.WriteLine();
        }
        else
        {
            // FSM: DataChecking → ExecutingLean
            fsm.Fire(BacktestEvent.DataReady);
            AnsiConsole.MarkupLine("[green]✓[/] Data available for session");
            AnsiConsole.WriteLine();
        }

        // Update status to Running
        await service.UpdateAsync(session with { Status = SessionStatus.Running });

        // Execute LEAN with session-specific paths
        int exitCode;
        if (!settings.NoMonitor)
        {
            exitCode = await ExecuteLeanWithMonitoringAsync(session, service);
        }
        else
        {
            exitCode = await ExecuteLeanForSession(session, service);
        }

        // FSM: ExecutingLean → Completed/Failed
        if (exitCode == 0)
        {
            fsm.Fire(BacktestEvent.LeanCompleted);
        }
        else
        {
            fsm.Fire(BacktestEvent.LeanFailed);
        }

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

        // Log FSM audit trail
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]FSM transitions: {fsm.History.Count}, Final state: {fsm.CurrentState}[/]");

        return exitCode;
    }

    private static (bool pricesMissing, bool earningsMissing, bool optionsMissing) CheckDataAvailability(string dataPath, APmd001A session)
    {
        // Check for price data (LEAN equity folder structure)
        var equityPath = System.IO.Path.Combine(dataPath, "equity", "usa", "daily");
        var hasPrices = Directory.Exists(equityPath) && Directory.GetFiles(equityPath, "*.zip").Length > 0;

        // Check for earnings cache
        var earningsPath = System.IO.Path.Combine(dataPath, "earnings", "nasdaq");
        var hasEarnings = Directory.Exists(earningsPath) && Directory.GetFiles(earningsPath, "*.json").Length > 0;

        // Check for options data (mandatory for strategy)
        var optionsPath = System.IO.Path.Combine(dataPath, "options");
        var hasOptions = Directory.Exists(optionsPath) && Directory.GetFiles(optionsPath, "*.json").Length > 0;

        return (!hasPrices, !hasEarnings, !hasOptions);
    }

    private static async Task BootstrapDataAsync(APmd001A session, APsv001A service, string dataPath)
    {
        AnsiConsole.MarkupLine("[blue]Downloading price data from Polygon...[/]");
        AnsiConsole.WriteLine();
        
        // Create configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.jsonc", optional: true)
            .AddJsonFile("appsettings.local.jsonc", optional: true)
            .AddEnvironmentVariables("ALARIS_")
            .Build();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        // Create Polygon client for price data
        var polygonHttpClient = new HttpClient { BaseAddress = new Uri("https://api.polygon.io") };
        polygonHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alaris/1.0");
        var polygonApi = RestService.For<IPolygonApi>(polygonHttpClient);
        var polygonClient = new PolygonApiClient(polygonApi, config, loggerFactory.CreateLogger<PolygonApiClient>());

        // Create NASDAQ client for earnings calendar
        var nasdaqHttpClient = new HttpClient { BaseAddress = new Uri("https://api.nasdaq.com") };
        nasdaqHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        nasdaqHttpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        var nasdaqApi = RestService.For<INasdaqCalendarApi>(nasdaqHttpClient);
        var earningsProvider = new NasdaqEarningsProvider(nasdaqApi, loggerFactory.CreateLogger<NasdaqEarningsProvider>());

        // Create data service with both providers
        using var dataService = new APsv002A(
            polygonClient, 
            earningsProvider, 
            null, 
            loggerFactory.CreateLogger<APsv002A>());
        
        // Download price data
        await dataService.DownloadEquityDataAsync(
            dataPath,
            session.Symbols,
            session.StartDate,
            session.EndDate);

        AnsiConsole.MarkupLine("[green]✓[/] Price data download completed");
        AnsiConsole.WriteLine();

        // Bootstrap earnings calendar for the session date range
        AnsiConsole.MarkupLine("[blue]Downloading earnings calendar from NASDAQ...[/]");
        AnsiConsole.MarkupLine("[grey]  (Rate-limited: ~1 day/second to avoid blocking)[/]");
        AnsiConsole.WriteLine();
        
        // Use data path so algorithm can find cached earnings at {data}/earnings/nasdaq/
        var daysDownloaded = await dataService.BootstrapEarningsCalendarAsync(
            session.StartDate,
            session.EndDate.AddDays(120), // Fetch 120 days of future earnings for lookahead logic
            dataPath,
            CancellationToken.None);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] Earnings calendar bootstrap completed ({daysDownloaded} days downloaded)");
        AnsiConsole.WriteLine();
        
        // Bootstrap options data for evaluation dates around earnings
        // Parse earnings dates from cached calendar to determine which dates need options
        AnsiConsole.MarkupLine("[blue]Downloading historical options data with IV...[/]");
        AnsiConsole.MarkupLine("[grey]  (Uses market prices to calculate Black-Scholes IV)[/]");
        AnsiConsole.WriteLine();
        
        var earningsDates = GetEarningsDatesFromCache(dataPath, session.StartDate, session.EndDate);
        if (earningsDates.Count > 0)
        {
            // For each earnings date, get options for evaluation dates 7-21 days before (strategy entry window)
            var evaluationDates = new HashSet<DateTime>();
            foreach (var earningsDate in earningsDates)
            {
                // Strategy evaluates 7-21 days before earnings
                for (int daysBeforeEarnings = 7; daysBeforeEarnings <= 21; daysBeforeEarnings += 7)
                {
                    var evalDate = earningsDate.AddDays(-daysBeforeEarnings).Date;
                    if (evalDate >= session.StartDate && evalDate <= session.EndDate)
                    {
                        evaluationDates.Add(evalDate);
                    }
                }
            }
            
            if (evaluationDates.Count > 0)
            {
                var optionsDownloaded = await dataService.BootstrapOptionsDataAsync(
                    session.Symbols,
                    evaluationDates.OrderBy(d => d),
                    dataPath,
                    CancellationToken.None);
                    
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]✓[/] Options data bootstrap completed ({optionsDownloaded} chains downloaded for {evaluationDates.Count} dates)");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]No evaluation dates in range for options bootstrap[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]No earnings dates found in cache for options bootstrap[/]");
        }
        AnsiConsole.WriteLine();
    }
    
    /// <summary>
    /// Parses earnings dates from cached calendar files.
    /// </summary>
    private static List<DateTime> GetEarningsDatesFromCache(string dataPath, DateTime startDate, DateTime endDate)
    {
        var earningsDates = new List<DateTime>();
        var earningsPath = System.IO.Path.Combine(dataPath, "earnings", "nasdaq");
        
        if (!Directory.Exists(earningsPath))
        {
            return earningsDates;
        }
        
        foreach (var file in Directory.GetFiles(earningsPath, "*.json"))
        {
            var filename = System.IO.Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(filename, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                if (date >= startDate && date <= endDate)
                {
                    // Check if file has any earnings events
                    try
                    {
                        var content = File.ReadAllText(file);
                        if (content.Length > 10 && !content.Contains("[]")) // Not empty
                        {
                            earningsDates.Add(date);
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }
        }
        
        return earningsDates;
    }

    private static async Task<int> ExecuteLeanWithMonitoringAsync(APmd001A session, APsv001A service)
    {
        // Create monitoring panel
        var rule = new Rule($"[blue]BACKTEST: {session.SessionId}[/]") { Justification = Justify.Left };
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine($"[grey]Press Ctrl+C to cancel[/]");
        AnsiConsole.WriteLine();

        // Run LEAN with live output parsing
        return await ExecuteLeanForSession(session, service);
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

        // CRITICAL: Inject session data path into LEAN config before engine starts
        // Environment variables (QC_DATA_FOLDER) do NOT work - LEAN reads from config.json only
        // Config.Set() also doesn't work because it only affects THIS process, not the subprocess
        // Solution: Pass --data-folder as a command-line argument to the LEAN subprocess
        var sessionDataPath = service.GetDataPath(session.SessionId);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{launcherPath}\" -- --data-folder \"{sessionDataPath}\" --close-automatically true",
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

// List Command

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

// Delete Command

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

// View Command

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
