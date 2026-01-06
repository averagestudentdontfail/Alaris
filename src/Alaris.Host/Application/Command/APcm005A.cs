// APcm005A.cs - Backtest command group (create/run/list/delete/view)

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        (DateTime startDate, DateTime endDate) = ResolveDateRange(settings);
        
        if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
        {
            return 1; // Error already printed
        }

        string[]? symbols = settings.Symbols?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        APsv001A service = new APsv001A();

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
                List<string> targets;
                if (symbols?.Length > 0)
                {
                    targets = new List<string>(symbols);
                    AnsiConsole.MarkupLine($"[yellow]Using specified symbols: {string.Join(", ", targets)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Running screener to discover tradeable symbols...[/]");
                    using APsv002B screener = DependencyFactory.CreateScreener();
                    List<string> screenedSymbols = await screener.ScreenAsync(startDate, maxSymbols: 50);
                    targets = screenedSymbols;
                    AnsiConsole.MarkupLine($"[green]✓[/] Screened {targets.Count} symbols");
                    
                    // Update session with screened symbols
                    session = session with { Symbols = screenedSymbols };
                    await service.UpdateAsync(session);
                }
                
                // Build requirements model for unified bootstrap
                Alaris.Core.Model.STDT010A requirements = new Alaris.Core.Model.STDT010A
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Symbols = targets
                };
                
                AnsiConsole.MarkupLine("[yellow]Starting unified data bootstrap...[/]");
                AnsiConsole.MarkupLine($"[grey]  Requirements: {requirements.GetSummary()}[/]");
                
                using APsv002A dataService = DependencyFactory.CreateAPsv002A();
                BootstrapReport bootstrapReport = await dataService.BootstrapSessionDataAsync(
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
            string[] yearStrings = settings.Years.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            List<int> years = new List<int>();
            
            foreach (string yearStr in yearStrings)
            {
                if (int.TryParse(yearStr, out int year) && year >= 2000 && year <= 2100)
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

            int minYear = years[0];
            int maxYear = years[0];
            for (int i = 1; i < years.Count; i++)
            {
                int year = years[i];
                if (year < minYear)
                {
                    minYear = year;
                }
                if (year > maxYear)
                {
                    maxYear = year;
                }
            }

            DateTime startDate = new DateTime(minYear, 1, 1);
            DateTime nowDate = DateTime.Now.Date;
            DateTime endDate = maxYear == nowDate.Year 
                ? nowDate.AddDays(-1)  // Current year: end yesterday
                : new DateTime(maxYear, 12, 31); // Past years: end Dec 31
            
            AnsiConsole.MarkupLine($"[cyan]Using year range: {minYear}-{maxYear}[/]");
            return (startDate, endDate);
        }

        // Option 2: Explicit --start/--end
        if (!string.IsNullOrEmpty(settings.StartDate) || !string.IsNullOrEmpty(settings.EndDate))
        {
            if (string.IsNullOrEmpty(settings.StartDate) || !DateTime.TryParse(settings.StartDate, out DateTime start))
            {
                AnsiConsole.MarkupLine("[red]Invalid or missing start date. Use YYYY-MM-DD format[/]");
                return (DateTime.MinValue, DateTime.MinValue);
            }

            if (string.IsNullOrEmpty(settings.EndDate) || !DateTime.TryParse(settings.EndDate, out DateTime end))
            {
                AnsiConsole.MarkupLine("[red]Invalid or missing end date. Use YYYY-MM-DD format[/]");
                return (DateTime.MinValue, DateTime.MinValue);
            }

            if (end < start)
            {
                AnsiConsole.MarkupLine("[red]End date must be on or after start date.[/]");
                return (DateTime.MinValue, DateTime.MinValue);
            }

            return (start, end);
        }

        // Option 3: Default - 2 years ending yesterday (with 1-month buffer for options data)
        // Polygon's Options Starter plan has a 2-year limit, but options aggregate data
        // at the boundary often returns 403. Using 23 months ensures reliable data access.
        DateTime yesterday = DateTime.Now.Date.AddDays(-1);
        DateTime twoYearsAgo = yesterday.AddYears(-2).AddMonths(1); // 23 months instead of 24
        AnsiConsole.MarkupLine($"[cyan]Using default 2-year range: {twoYearsAgo:yyyy-MM-dd} to {yesterday:yyyy-MM-dd}[/]");
        return (twoYearsAgo, yesterday);
    }
}

file static class BacktestFormatting
{
    public static string BuildSymbolPreview(IReadOnlyList<string> symbols, int maxSymbols)
    {
        if (symbols.Count == 0)
        {
            return string.Empty;
        }

        int count = symbols.Count < maxSymbols ? symbols.Count : maxSymbols;
        string[] preview = new string[count];
        for (int i = 0; i < count; i++)
        {
            preview[i] = symbols[i];
        }

        string result = string.Join(", ", preview);
        if (symbols.Count > maxSymbols)
        {
            result += "...";
        }

        return result;
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
        APsv001A service = new APsv001A();
        APmd001A? session = await service.GetAsync(settings.SessionId);

        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]Session not found: {settings.SessionId}[/]");
            return 1;
        }

        // Determine symbols: use session symbols OR run screener
        List<string> targets;
        if (session.Symbols.Count > 0)
        {
            targets = new List<string>(session.Symbols);
            string symbolPreview = BacktestFormatting.BuildSymbolPreview(session.Symbols, 10);
            AnsiConsole.MarkupLine($"[yellow]Using session symbols: {symbolPreview}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Running screener to discover tradeable symbols...[/]");
            using APsv002B screener = DependencyFactory.CreateScreener();
            List<string> screenedSymbols = await screener.ScreenAsync(session.StartDate, maxSymbols: 50);
            targets = screenedSymbols;
            AnsiConsole.MarkupLine($"[green]✓[/] Screened {screenedSymbols.Count} symbols");
            
            // Update session with screened symbols
            session = session with { Symbols = screenedSymbols };
            await service.UpdateAsync(session);
        }

        // Build requirements model for unified bootstrap
        Alaris.Core.Model.STDT010A requirements = new Alaris.Core.Model.STDT010A
        {
            StartDate = session.StartDate,
            EndDate = session.EndDate,
            Symbols = targets
        };
        
        using APsv002A dataService = DependencyFactory.CreateAPsv002A();
        AnsiConsole.MarkupLine($"[yellow]Starting unified bootstrap for session {session.SessionId}...[/]");
        
        BootstrapReport bootstrapReport = await dataService.BootstrapSessionDataAsync(
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
        HashSet<DateTime> dates = new HashSet<DateTime>();
        string nasdaqPath = System.IO.Path.Combine(sessionDataPath, "earnings", "nasdaq");
        
        if (!Directory.Exists(nasdaqPath))
        {
            return Array.Empty<DateTime>();
        }
        
        HashSet<string> symbolSet = new HashSet<string>(symbols, StringComparer.OrdinalIgnoreCase);
        
        string[] files = Directory.GetFiles(nasdaqPath, "*.json");
        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                // Simple parsing - look for symbol matches
                foreach (string symbol in symbolSet)
                {
                    if (json.Contains($"\"{symbol}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract date from filename
                        string dateStr = System.IO.Path.GetFileNameWithoutExtension(file);
                        if (DateTime.TryParse(dateStr, out DateTime earningsDate))
                        {
                            // Add evaluation dates (5-7 days before)
                            for (int d = 5; d <= 7; d++)
                            {
                                DateTime evalDate = earningsDate.AddDays(-d);
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
        
        List<DateTime> orderedDates = new List<DateTime>(dates);
        orderedDates.Sort();
        return orderedDates;
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
            .AddUserSecrets<BacktestCreateCommand>(optional: true)
            .AddEnvironmentVariables("ALARIS_")
            .Build();
    }
    
    private static HttpClient GetHttpClient() => _httpClient ??= new HttpClient();
    
    private static ILoggerFactory GetLoggerFactory() => _loggerFactory ??= 
        LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Services persist for command duration")]
    public static APsv002A CreateAPsv002A()
    {
        IConfiguration config = GetConfig();
        ILoggerFactory loggerFactory = GetLoggerFactory();
        
        // Create Refit clients for each API
        IPolygonApi polygonApi = CreatePolygonApi();
        INasdaqCalendarApi nasdaqApi = CreateNasdaqApi();
        ITreasuryDirectApi treasuryApi = CreateTreasuryApi();
        
        PolygonApiClient polygonClient = new PolygonApiClient(
            polygonApi, 
            config, 
            loggerFactory.CreateLogger<PolygonApiClient>());

        NasdaqEarningsProvider earningsClient = new NasdaqEarningsProvider(
            nasdaqApi,
            loggerFactory.CreateLogger<NasdaqEarningsProvider>());

        TreasuryDirectRateProvider treasuryClient = new TreasuryDirectRateProvider(
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
        HttpClient httpClient = new HttpClient { BaseAddress = new Uri("https://api.polygon.io") };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alaris/1.0 (Quantitative Trading System)");
        return RestService.For<IPolygonApi>(httpClient);
    }
    
    private static INasdaqCalendarApi CreateNasdaqApi()
    {
        HttpClient httpClient = new HttpClient { BaseAddress = new Uri("https://api.nasdaq.com") };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return RestService.For<INasdaqCalendarApi>(httpClient);
    }
    
    private static ITreasuryDirectApi CreateTreasuryApi()
    {
        HttpClient httpClient = new HttpClient { BaseAddress = new Uri("https://www.treasurydirect.gov/TA_WS/securities") };
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
        PLWF001A<BacktestState, BacktestEvent> fsm = PLWF002A.Create();
        fsm.OnTransition += record => 
        {
            if (record.Succeeded)
                AnsiConsole.MarkupLine($"[grey]FSM: {record.FromState} → {record.ToState}[/]");
        };
        
        APsv001A service = new APsv001A();

        string? sessionId = settings.SessionId;

        if (settings.Latest || string.IsNullOrEmpty(sessionId))
        {
            IReadOnlyList<APmd001A> sessions = await service.ListAsync();
            APmd001A? latest = sessions.Count > 0 ? sessions[0] : null;

            if (latest == null)
            {
                AnsiConsole.MarkupLine("[red]No sessions found. Create one with:[/] [cyan]alaris backtest create[/]");
                return 1;
            }

            sessionId = latest.SessionId;
            AnsiConsole.MarkupLine($"[grey]Using latest session: {sessionId}[/]");
        }

        APmd001A? session = await service.GetAsync(sessionId);
        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]Session not found: {sessionId}[/]");
            return 1;
        }

        // FSM: Idle → SessionSelected
        fsm.Fire(BacktestEvent.SelectSession);

        AnsiConsole.MarkupLine($"[blue]Running session:[/] {session.SessionId}");
        AnsiConsole.MarkupLine($"[blue]Date Range:[/] {session.StartDate:yyyy-MM-dd} to {session.EndDate:yyyy-MM-dd}");
        string symbolPreview = BacktestFormatting.BuildSymbolPreview(session.Symbols, 10);
        string symbolDisplay = symbolPreview.Length == 0 ? "(none)" : symbolPreview;
        AnsiConsole.MarkupLine($"[blue]Symbols:[/] {symbolDisplay}");
        AnsiConsole.WriteLine();

        // FSM: SessionSelected → DataChecking
        fsm.Fire(BacktestEvent.CheckData);

        string dataPath = service.GetDataPath(session.SessionId);
        (bool pricesMissing, bool earningsMissing, bool optionsMissing) = CheckDataAvailability(dataPath, session);

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
                bool download = AnsiConsole.Confirm("Download missing data now?", defaultValue: true);
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
        SessionStatus finalStatus = exitCode == 0 ? SessionStatus.Completed : SessionStatus.Failed;
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
        string equityPath = System.IO.Path.Combine(dataPath, "equity", "usa", "daily");
        bool hasPrices = Directory.Exists(equityPath) && Directory.GetFiles(equityPath, "*.zip").Length > 0;

        // Check for earnings cache
        string earningsPath = System.IO.Path.Combine(dataPath, "earnings", "nasdaq");
        bool hasEarnings = Directory.Exists(earningsPath) && Directory.GetFiles(earningsPath, "*.json").Length > 0;

        // Check for options data (mandatory for strategy)
        string optionsPath = System.IO.Path.Combine(dataPath, "options");
        bool hasOptions = Directory.Exists(optionsPath) && Directory.GetFiles(optionsPath, "*.json").Length > 0;

        return (!hasPrices, !hasEarnings, !hasOptions);
    }

    private static async Task BootstrapDataAsync(APmd001A session, APsv001A service, string dataPath)
    {
        AnsiConsole.MarkupLine("[blue]Downloading price data from Polygon...[/]");
        AnsiConsole.WriteLine();
        
        // Create configuration
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.jsonc", optional: true)
            .AddJsonFile("appsettings.local.jsonc", optional: true)
            .AddUserSecrets<BacktestCreateCommand>(optional: true)
            .AddEnvironmentVariables("ALARIS_")
            .Build();

        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        // Create Polygon client for price data
        HttpClient polygonHttpClient = new HttpClient { BaseAddress = new Uri("https://api.polygon.io") };
        polygonHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alaris/1.0");
        IPolygonApi polygonApi = RestService.For<IPolygonApi>(polygonHttpClient);
        PolygonApiClient polygonClient = new PolygonApiClient(polygonApi, config, loggerFactory.CreateLogger<PolygonApiClient>());

        // Create NASDAQ client for earnings calendar
        HttpClient nasdaqHttpClient = new HttpClient { BaseAddress = new Uri("https://api.nasdaq.com") };
        nasdaqHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        nasdaqHttpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        INasdaqCalendarApi nasdaqApi = RestService.For<INasdaqCalendarApi>(nasdaqHttpClient);
        NasdaqEarningsProvider earningsProvider = new NasdaqEarningsProvider(nasdaqApi, loggerFactory.CreateLogger<NasdaqEarningsProvider>());

        // Create data service with both providers
        using APsv002A dataService = new APsv002A(
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
        int daysDownloaded = await dataService.BootstrapEarningsCalendarAsync(
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
        
        List<DateTime> earningsDates = GetEarningsDatesFromCache(dataPath, session.StartDate, session.EndDate);
        if (earningsDates.Count > 0)
        {
            // For each earnings date, get options for evaluation dates 7-21 days before (strategy entry window)
            HashSet<DateTime> evaluationDates = new HashSet<DateTime>();
            foreach (DateTime earningsDate in earningsDates)
            {
                // Strategy evaluates 7-21 days before earnings
                for (int daysBeforeEarnings = 7; daysBeforeEarnings <= 21; daysBeforeEarnings += 7)
                {
                    DateTime evalDate = earningsDate.AddDays(-daysBeforeEarnings).Date;
                    if (evalDate >= session.StartDate && evalDate <= session.EndDate)
                    {
                        evaluationDates.Add(evalDate);
                    }
                }
            }
            
            if (evaluationDates.Count > 0)
            {
                List<DateTime> orderedDates = new List<DateTime>(evaluationDates);
                orderedDates.Sort();
                int optionsDownloaded = await dataService.BootstrapOptionsDataAsync(
                    session.Symbols,
                    orderedDates,
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
        List<DateTime> earningsDates = new List<DateTime>();
        string earningsPath = System.IO.Path.Combine(dataPath, "earnings", "nasdaq");
        
        if (!Directory.Exists(earningsPath))
        {
            return earningsDates;
        }
        
        string[] files = Directory.GetFiles(earningsPath, "*.json");
        foreach (string file in files)
        {
            string filename = System.IO.Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(filename, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
            {
                if (date >= startDate && date <= endDate)
                {
                    // Check if file has any earnings events
                    try
                    {
                        string content = File.ReadAllText(file);
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
        Rule rule = new Rule($"[blue]BACKTEST: {session.SessionId}[/]") { Justification = Justify.Left };
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine($"[grey]Press Ctrl+C to cancel[/]");
        AnsiConsole.WriteLine();

        // Run LEAN with live output parsing
        return await ExecuteLeanForSession(session, service);
    }

    private static async Task<int> ExecuteLeanForSession(APmd001A session, APsv001A service)
    {
        // Find config and launcher
        string? configPath = FindConfigPath();
        string? launcherPath = FindLeanLauncher();

        if (configPath == null || launcherPath == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find config.json or LEAN launcher[/]");
            return 1;
        }

        // CRITICAL: Inject session data path into LEAN config before engine starts
        // Environment variables (QC_DATA_FOLDER) do NOT work - LEAN reads from config.json only
        // Config.Set() also doesn't work because it only affects THIS process, not the subprocess
        // Solution: Pass --data-folder as a command-line argument to the LEAN subprocess
        string sessionDataPath = service.GetDataPath(session.SessionId);

        ProcessStartInfo psi = new ProcessStartInfo
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

        using Process? process = Process.Start(psi);
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
                string escaped = Markup.Escape(e.Data);
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
        string[] paths = new[] { "config.json", "../config.json", "../../config.json" };
        foreach (string path in paths)
        {
            if (File.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }
        return null;
    }

    private static string? FindLeanLauncher()
    {
        string[] paths = new[] 
        { 
            "Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj",
            "../Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj",
            "../../Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj"
        };
        foreach (string path in paths)
        {
            if (File.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }
        return null;
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
        APsv001A service = new APsv001A();
        IReadOnlyList<APmd001A> sessions = await service.ListAsync();

        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No backtest sessions found.[/]");
            AnsiConsole.MarkupLine("Create one with: [cyan]alaris backtest create --start YYYY-MM-DD --end YYYY-MM-DD[/]");
            return 0;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Session ID[/]")
            .AddColumn("[bold]Date Range[/]")
            .AddColumn("[bold]Status[/]")
            .AddColumn("[bold]Created[/]")
            .AddColumn("[bold]Symbols[/]");

        foreach (APmd001A session in sessions)
        {
            string statusColor = session.Status switch
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
        APsv001A service = new APsv001A();
        APmd001A? session = await service.GetAsync(settings.SessionId);

        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]Session not found: {settings.SessionId}[/]");
            return 1;
        }

        if (!settings.Force)
        {
            bool confirm = AnsiConsole.Confirm(
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
        APsv001A service = new APsv001A();
        APmd001A? session = await service.GetAsync(settings.SessionId);

        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]Session not found: {settings.SessionId}[/]");
            return 1;
        }

        // Session details panel
        string symbolPreview = BacktestFormatting.BuildSymbolPreview(session.Symbols, 10);
        string symbolDisplay = symbolPreview.Length == 0 ? "(none)" : symbolPreview;

        Panel panel = new Panel(new Markup(
            $"[bold]Session ID:[/] {session.SessionId}\n" +
            $"[bold]Date Range:[/] {session.StartDate:yyyy-MM-dd} to {session.EndDate:yyyy-MM-dd}\n" +
            $"[bold]Status:[/] {session.Status}\n" +
            $"[bold]Created:[/] {session.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
            $"[bold]Updated:[/] {session.UpdatedAt:yyyy-MM-dd HH:mm:ss}\n" +
            $"[bold]Path:[/] {session.SessionPath}\n" +
            $"[bold]Symbols:[/] {symbolDisplay}"
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
            
            Table statsTable = new Table()
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
            string logPath = System.IO.Path.Combine(service.GetResultsPath(session.SessionId), "log.txt");
            if (File.Exists(logPath))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Logs (last 50 lines):[/]");
                string[] lines = await File.ReadAllLinesAsync(logPath);
                int start = lines.Length > 50 ? lines.Length - 50 : 0;
                for (int i = start; i < lines.Length; i++)
                {
                    AnsiConsole.WriteLine(lines[i]);
                }
            }
        }

        return 0;
    }
}
