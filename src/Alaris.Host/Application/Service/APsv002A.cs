// APsv002A.cs - Session data download service (Polygon → LEAN format)

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alaris.Infrastructure.Data.Provider.Nasdaq; // For NasdaqEarningsProvider
using Alaris.Infrastructure.Data.Provider.Polygon; // For PolygonApiClient
using Alaris.Infrastructure.Data.Provider.Treasury; // For TreasuryDirectRateProvider
using Alaris.Infrastructure.Data.Model; // For PriceBar
using System.Text.Json; // For JSON serialization
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Alaris.Host.Application.Service;

/// <summary>
/// Service for downloading and preparing session data.
/// Component ID: APsv002A
/// </summary>
public sealed class APsv002A : IDisposable
{
    private readonly PolygonApiClient _polygonClient;
    private readonly NasdaqEarningsProvider? _earningsClient;
    private readonly TreasuryDirectRateProvider? _treasuryClient;
    private readonly ILogger<APsv002A>? _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true,
        PropertyNameCaseInsensitive = true 
    };

    public APsv002A(
        PolygonApiClient polygonClient, 
        NasdaqEarningsProvider? earningsClient, 
        TreasuryDirectRateProvider? treasuryClient,
        ILogger<APsv002A>? logger = null)
    {
        _polygonClient = polygonClient ?? throw new ArgumentNullException(nameof(polygonClient));
        _earningsClient = earningsClient;
        _treasuryClient = treasuryClient;
        _logger = logger;
    }

    /// <summary>
    /// Downloads equity data for a session and saves in LEAN format.
    /// </summary>
    public async Task DownloadEquityDataAsync(string sessionDataPath, IEnumerable<string> symbols, DateTime start, DateTime end)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDataPath);
        ArgumentNullException.ThrowIfNull(symbols);
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End date must be on or after start date.");
        }

        List<string> symbolList = new List<string>(symbols);
        if (symbolList.Count == 0)
        {
            return;
        }

        int total = symbolList.Count;
        int current = 0;

        // Path: session/data/equity/usa/daily
        string dailyPath = Path.Combine(sessionDataPath, "equity", "usa", "daily");
        Directory.CreateDirectory(dailyPath);

        // NOTE: Earnings data comes from 'alaris earnings bootstrap' command
        // using cache-first pattern in DTea001C. Not downloaded here.

        // Path: session/data/options
        string optionsPath = Path.Combine(sessionDataPath, "options");
        Directory.CreateDirectory(optionsPath);

        // Copy system files first (so we can overwrite/add to them)
        CopySystemFiles(sessionDataPath);

        // Path: session/data/equity/usa/map_files
        string mapFilesPath = Path.Combine(sessionDataPath, "equity", "usa", "map_files");
        Directory.CreateDirectory(mapFilesPath);

        // Path: session/data/equity/usa/factor_files
        string factorFilesPath = Path.Combine(sessionDataPath, "equity", "usa", "factor_files");
        Directory.CreateDirectory(factorFilesPath);

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                ProgressTask taskData = ctx.AddTask($"[green]Downloading Price Data ({total} symbols)...[/]");
                ProgressTask taskOptions = ctx.AddTask($"[green]Downloading Options Data...[/]");
                ProgressTask? taskRates = _treasuryClient != null ? ctx.AddTask($"[green]Downloading Interest Rates...[/]") : null;
                
                foreach (string symbol in symbolList)
                {
                    current++;
                    taskData.Description = $"[green]Downloading prices for {symbol} ({current}/{total})...[/]";
                    taskData.Value = (double)current / total * 100;

                    // Generate Map File (Critical for LEAN to find the data)
                    await GenerateMapFileAsync(symbol, mapFilesPath);
                    
                    // Generate Factor File (Critical for LEAN data reading, assumes adjusted data)
                    await GenerateFactorFileAsync(symbol, factorFilesPath);

                    // Price Data
                    try
                    {
                        // Add buffer for warmup (120 days) to ensure sufficient history
                        DateTime minAllowedDate = DateTime.UtcNow.AddYears(-2).Date;
                        DateTime lookbackStart = start.AddDays(-120);
                        DateTime requestStart = lookbackStart < minAllowedDate ? minAllowedDate : lookbackStart;
                        
                        IReadOnlyList<PriceBar> bars = await _polygonClient.GetHistoricalBarsAsync(symbol, requestStart, end);
                        if (bars.Count > 0)
                        {
                            await SaveAsLeanZipAsync(symbol, bars, dailyPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to download prices for {Symbol}", symbol);
                    }

                    // Options Data - Download historical chain for session start date
                    // Apply 1-month buffer from 2-year boundary to ensure options aggregates are available
                    try
                    {
                        taskOptions.Description = $"[green]Downloading options for {symbol} ({current}/{total})...[/]";
                        taskOptions.Value = (double)current / total * 100;
                        
                        // Polygon's Options Starter plan has a 2-year limit for options aggregates.
                        // Apply a 1-month buffer to avoid hitting the boundary.
                        DateTime optionsMinDate = DateTime.UtcNow.AddYears(-2).AddMonths(1).Date;
                        DateTime effectiveOptionsDate = start < optionsMinDate ? optionsMinDate : start;
                        
                        OptionChainSnapshot optionChain = await _polygonClient.GetHistoricalOptionChainAsync(symbol, effectiveOptionsDate);
                        if (optionChain.Contracts.Count > 0)
                        {
                            string jsonPath = Path.Combine(optionsPath, $"{symbol.ToLowerInvariant()}.json");
                            string json = JsonSerializer.Serialize(optionChain, JsonOptions);
                            await File.WriteAllTextAsync(jsonPath, json);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to download options for {Symbol} (may not have options)", symbol);
                    }
                }


                // Interest Rate Data (One-time fetch)
                if (_treasuryClient != null && taskRates != null)
                {
                    try
                    {
                        taskRates.IsIndeterminate = true;
                        string ratePath = Path.Combine(sessionDataPath, "alternative", "interest-rate", "usa");
                        Directory.CreateDirectory(ratePath);
                        string csvPath = Path.Combine(ratePath, "interest-rate.csv");

                        // Look back 2 years + buffer from Start Date, or just fetch large history
                        // Rates are global, not per-symbol.
                        IReadOnlyDictionary<DateTime, decimal> rates = await _treasuryClient.GetHistoricalRatesAsync(start.AddYears(-2), end);
                        
                        if (rates.Count > 0)
                        {
                            // Write CSV: Date(yyyyMMdd),Rate(decimal)
                            // Sort by date
                            StringBuilder sb = new StringBuilder();
                            List<DateTime> rateDates = new List<DateTime>(rates.Keys);
                            rateDates.Sort();
                            foreach (DateTime rateDate in rateDates)
                            {
                                sb.AppendLine($"{rateDate:yyyyMMdd},{rates[rateDate]}");
                            }
                            await File.WriteAllTextAsync(csvPath, sb.ToString());
                            _logger?.LogInformation("Saved {Count} interest rate observations to {Path}", rates.Count, csvPath);
                        }
                        else
                        {
                            _logger?.LogWarning("Treasury API returned 0 interest rate observations for date range");
                        }
                        taskRates.Description = $"[green]Interest Rates: {rates.Count} observations[/]";
                        taskRates.Value = 100;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to download interest rates");
                        taskRates.Description = "[red]Interest Rates Failed[/]";
                    }
                }
            });
            
        // CopySystemFiles moved to start
    }

    /// <summary>
    /// Generates a valid LEAN map file (csv) for the symbol.
    /// Format: Date,Ticker,Exchange
    /// </summary>
    private async Task GenerateMapFileAsync(string symbol, string mapFilesPath)
    {
        string ticker = symbol.ToLowerInvariant();
        string path = Path.Combine(mapFilesPath, $"{ticker}.csv");
        
        // Simple map file: valid provided ticker for all history, default to NASDAQ (Q)
        // 19980101,ticker,Q
        // 20501231,ticker,Q
        string content = $"19980101,{ticker},Q\n20501231,{ticker},Q";
        
        await File.WriteAllTextAsync(path, content);
    }

    /// <summary>
    /// Generates a valid LEAN factor file (csv) for the symbol.
    /// Format: Date,PriceFactor,SplitFactor,ReferencePrice
    /// Since we use adjusted data from Polygon (post-split prices), we set factors to 1 (no adjustment).
    /// </summary>
    private async Task GenerateFactorFileAsync(string symbol, string factorFilesPath)
    {
        string ticker = symbol.ToLowerInvariant();
        string path = Path.Combine(factorFilesPath, $"{ticker}.csv");
        
        // Simple factor file: no adjustments (1,1) for all history
        // 19980101,1,1,1
        // 20501231,1,1,1
        string content = "19980101,1,1,1\n20501231,1,1,1";
        
        await File.WriteAllTextAsync(path, content);
    }

    /// <summary>
    /// Bootstrap earnings calendar data from NASDAQ API to local cache files.
    /// Rate-limited to 1 request per second to avoid anti-bot blocking.
    /// </summary>
    /// <param name="startDate">Start date for calendar data.</param>
    /// <param name="endDate">End date for calendar data.</param>
    /// <param name="outputPath">Output directory for cached files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of days downloaded.</returns>
    public async Task<int> BootstrapEarningsCalendarAsync(
        DateTime startDate,
        DateTime endDate,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (endDate < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "End date must be on or after start date.");
        }

        if (_earningsClient == null)
        {
            throw new InvalidOperationException("Earnings client not configured");
        }

        string nasdaqPath = Path.Combine(outputPath, "earnings", "nasdaq");
        Directory.CreateDirectory(nasdaqPath);

        int totalWeekdays = 0;
        int downloadedDays = 0;
        int skippedDays = 0;

        // Count total weekdays
        for (DateTime d = startDate; d <= endDate; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            {
                totalWeekdays++;
            }
        }

        _logger?.LogInformation(
            "Bootstrap earnings calendar: {Start:yyyy-MM-dd} to {End:yyyy-MM-dd} ({Total} weekdays)",
            startDate, endDate, totalWeekdays);

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                ProgressTask task = ctx.AddTask("Downloading Earnings Calendar", maxValue: totalWeekdays);

                for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Skip weekends
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        continue;
                    }

                    string cachePath = Path.Combine(nasdaqPath, $"{date:yyyy-MM-dd}.json");

                    // Skip if already cached
                    if (File.Exists(cachePath))
                    {
                        skippedDays++;
                        task.Increment(1);
                        continue;
                    }

                    try
                    {
                        // Rate limit: 1 request per second
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                        IReadOnlyList<EarningsEvent> earnings = await _earningsClient.FetchAndCacheAsync(
                            date, outputPath, cancellationToken);

                        downloadedDays++;
                        task.Description = $"Downloaded {date:yyyy-MM-dd} ({earnings.Count} events)";
                        task.Increment(1);
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger?.LogWarning(
                            ex,
                            "Failed to fetch {Date:yyyy-MM-dd}, will retry on next run",
                            date);
                        task.Increment(1);
                    }
                }
            });

        _logger?.LogInformation(
            "Earnings bootstrap complete: {Downloaded} downloaded, {Skipped} skipped (already cached)",
            downloadedDays, skippedDays);

        return downloadedDays;
    }

    /// <summary>
    /// Bootstrap options data for specific dates (typically around earnings events).
    /// Downloads historical option chain with IV calculated from market prices via Black-Scholes.
    /// Concurrency and throttling are controlled by Polygon client settings.
    /// </summary>
    /// <param name="symbols">Symbols to download options for.</param>
    /// <param name="dates">Dates to download options for (evaluation dates around earnings).</param>
    /// <param name="sessionDataPath">Session data path for caching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of option chains downloaded.</returns>
    public async Task<int> BootstrapOptionsDataAsync(
        IEnumerable<string> symbols,
        IEnumerable<DateTime> dates,
        string sessionDataPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDataPath);

        List<string> symbolList = new List<string>(symbols);
        HashSet<DateTime> uniqueDates = new HashSet<DateTime>();
        foreach (DateTime date in dates)
        {
            uniqueDates.Add(date);
        }

        List<DateTime> dateList = new List<DateTime>(uniqueDates);
        dateList.Sort();
        
        if (symbolList.Count == 0 || dateList.Count == 0)
        {
            return 0;
        }

        int strideDays = Math.Max(1, _polygonClient.OptionsBootstrapStrideDays);
        if (strideDays > 1)
        {
            int before = dateList.Count;
            dateList = ApplyDateStride(dateList, strideDays);
            _logger?.LogInformation("Applied options bootstrap stride: {Stride} days ({Before} → {After} dates)", 
                strideDays, before, dateList.Count);
        }

        string optionsPath = Path.Combine(sessionDataPath, "options");
        Directory.CreateDirectory(optionsPath);

        Dictionary<string, IReadOnlyDictionary<int, decimal>> spotCache =
            new Dictionary<string, IReadOnlyDictionary<int, decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (string symbol in symbolList)
        {
            spotCache[symbol] = LoadDailyCloseCache(sessionDataPath, symbol);
        }

        int totalDownloaded = 0;
        int totalSkipped = 0;
        int totalFailed = 0;

        // Apply 2-year limit buffer (Polygon Options Starter plan)
        DateTime optionsMinDate = DateTime.UtcNow.AddYears(-2).AddMonths(1).Date;

        _logger?.LogInformation(
            "Bootstrap options: {SymbolCount} symbols × {DateCount} dates",
            symbolList.Count, dateList.Count);

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                ProgressTask task = ctx.AddTask("Downloading Options Data", maxValue: symbolList.Count * dateList.Count);
                int maxParallel = Math.Max(1, _polygonClient.OptionsChainParallelism);
                int delayMs = _polygonClient.OptionsChainDelayMs;
                object progressLock = new object();

                List<(string Symbol, DateTime Date)> workItems =
                    new List<(string Symbol, DateTime Date)>(symbolList.Count * dateList.Count);
                foreach (string symbol in symbolList)
                {
                    foreach (DateTime date in dateList)
                    {
                        workItems.Add((symbol, date));
                    }
                }

                ParallelOptions options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxParallel,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(workItems, options, async (item, ct) =>
                {
                    string symbol = item.Symbol;
                    DateTime date = item.Date;
                    string symbolLower = symbol.ToLowerInvariant();
                    string dateSuffix = date.ToString("yyyyMMdd");
                    string cachePath = Path.Combine(optionsPath, $"{symbolLower}_{dateSuffix}.json");
                    int dateKey = (date.Year * 10000) + (date.Month * 100) + date.Day;

                    try
                    {
                        // Skip if already cached
                        if (File.Exists(cachePath))
                        {
                            Interlocked.Increment(ref totalSkipped);
                            return;
                        }

                        // Skip dates outside Polygon's 2-year limit
                        if (date < optionsMinDate)
                        {
                            _logger?.LogDebug("Skipping {Symbol} @ {Date}: outside 2-year limit", symbol, date);
                            return;
                        }

                        lock (progressLock)
                        {
                            task.Description = $"Options: {symbol} @ {date:yyyy-MM-dd}";
                        }

                        decimal? spotOverride = null;
                        if (spotCache.TryGetValue(symbol, out IReadOnlyDictionary<int, decimal>? spotByDate) &&
                            spotByDate.TryGetValue(dateKey, out decimal cachedSpot) &&
                            cachedSpot > 0m)
                        {
                            spotOverride = cachedSpot;
                        }

                        OptionChainSnapshot optionChain = await _polygonClient.GetHistoricalOptionChainAsync(
                            symbol,
                            date,
                            spotOverride,
                            ct);

                        if (optionChain.Contracts.Count > 0)
                        {
                            string json = JsonSerializer.Serialize(optionChain, JsonOptions);
                            await File.WriteAllTextAsync(cachePath, json, ct);
                            Interlocked.Increment(ref totalDownloaded);
                            _logger?.LogDebug("Cached {Count} options for {Symbol} @ {Date}",
                                optionChain.Contracts.Count, symbol, date);
                        }
                        else
                        {
                            _logger?.LogDebug("No options found for {Symbol} @ {Date}", symbol, date);
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref totalFailed);
                        _logger?.LogWarning(ex, "Failed to download options for {Symbol} @ {Date}", symbol, date);
                    }
                    finally
                    {
                        lock (progressLock)
                        {
                            task.Increment(1);
                        }

                        if (delayMs > 0)
                        {
                            await Task.Delay(delayMs, ct);
                        }
                    }
                });
            });

        _logger?.LogInformation(
            "Options bootstrap complete: {Downloaded} downloaded, {Skipped} cached, {Failed} failed",
            totalDownloaded, totalSkipped, totalFailed);

        return totalDownloaded;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Unified Bootstrap (Single Entry Point)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bootstraps ALL data required for a backtest session.
    /// This is the SINGLE entry point for data preparation.
    /// </summary>
    /// <param name="requirements">Session data requirements model.</param>
    /// <param name="sessionDataPath">Path to session data directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed bootstrap report.</returns>
    public async Task<BootstrapReport> BootstrapSessionDataAsync(
        Alaris.Core.Model.STDT010A requirements,
        string sessionDataPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDataPath);
        
        (bool IsValid, string? Error) validation = requirements.Validate();
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error ?? "Invalid bootstrap requirements.", nameof(requirements));
        }
        
        _logger?.LogInformation("Starting unified bootstrap: {Summary}", requirements.GetSummary());
        
        BootstrapReport report = new BootstrapReport
        {
            StartedAt = DateTime.UtcNow,
            SessionDataPath = sessionDataPath
        };
        
        try
        {
            // Phase 1: System files (market-hours, symbol-properties, default maps)
            AnsiConsole.MarkupLine("[blue]Phase 1:[/] Copying system files...");
            CopySystemFiles(sessionDataPath);
            
            // Phase 2: Price data with warmup buffer
            AnsiConsole.MarkupLine("[blue]Phase 2:[/] Downloading price data...");
            report.PricesDownloaded = await DownloadPriceDataWithBenchmarkAsync(
                requirements, sessionDataPath, cancellationToken);
            
            // Phase 3: Earnings calendar with lookahead (and warmup lookback)
            AnsiConsole.MarkupLine("[blue]Phase 3:[/] Downloading earnings calendar...");
            AnsiConsole.MarkupLine($"[grey]  Range: {requirements.PriceDataStart:yyyy-MM-dd} to {requirements.EarningsLookaheadEnd:yyyy-MM-dd}[/]");
            
            if (_earningsClient != null)
            {
                report.EarningsDaysDownloaded = await BootstrapEarningsCalendarAsync(
                    requirements.PriceDataStart,  // Include warmup period
                    requirements.EarningsLookaheadEnd, // Critical: includes lookahead
                    sessionDataPath,
                    cancellationToken);
            }
            
            // Phase 4: Compute options-required dates from earnings
            // CRITICAL: Use PriceDataStart to include warmup period, not just StartDate
            AnsiConsole.MarkupLine("[blue]Phase 4:[/] Computing options-required dates (including warmup)...");
            IReadOnlyList<DateTime> optionsDates = ComputeOptionsRequiredDates(
                sessionDataPath,
                requirements.Symbols,
                requirements.PriceDataStart,  // Use warmup start, not session start
                requirements.EndDate,
                requirements.SignalWindowMinDays,
                requirements.SignalWindowMaxDays);
            
            report.OptionsRequiredDatesComputed = optionsDates.Count;
            AnsiConsole.MarkupLine($"[grey]  Found {optionsDates.Count} dates requiring options data (from {requirements.PriceDataStart:yyyy-MM-dd})[/]");
            
            // Phase 5: Bootstrap options for each required date
            if (optionsDates.Count > 0)
            {
                AnsiConsole.MarkupLine("[blue]Phase 5:[/] Downloading options data for signal dates...");
                report.OptionsDownloaded = await BootstrapOptionsDataAsync(
                    requirements.Symbols,
                    optionsDates,
                    sessionDataPath,
                    cancellationToken);
            }
            
            // Phase 6: Interest rates (optional)
            if (_treasuryClient != null)
            {
                AnsiConsole.MarkupLine("[blue]Phase 6:[/] Downloading interest rates...");
                report.InterestRatesDownloaded = await DownloadInterestRatesAsync(
                    requirements, sessionDataPath, cancellationToken);
            }
            
            report.Success = true;
        }
        catch (Exception ex)
        {
            report.Success = false;
            report.ErrorMessage = ex.Message;
            _logger?.LogError(ex, "Bootstrap failed");
        }
        finally
        {
            report.CompletedAt = DateTime.UtcNow;
        }
        
        _logger?.LogInformation(
            "Bootstrap complete: Prices={Prices}, Earnings={Earnings}, Options={Options}",
            report.PricesDownloaded, report.EarningsDaysDownloaded, report.OptionsDownloaded);
        
        return report;
    }

    /// <summary>
    /// Downloads price data for all symbols including benchmark.
    /// </summary>
    private async Task<int> DownloadPriceDataWithBenchmarkAsync(
        Alaris.Core.Model.STDT010A requirements,
        string sessionDataPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDataPath);

        string dailyPath = Path.Combine(sessionDataPath, "equity", "usa", "daily");
        Directory.CreateDirectory(dailyPath);
        
        string mapFilesPath = Path.Combine(sessionDataPath, "equity", "usa", "map_files");
        Directory.CreateDirectory(mapFilesPath);
        
        string factorFilesPath = Path.Combine(sessionDataPath, "equity", "usa", "factor_files");
        Directory.CreateDirectory(factorFilesPath);
        
        IReadOnlyList<string> allSymbols = requirements.AllSymbols;
        int downloaded = 0;
        
        // Apply 2-year limit for Polygon
        DateTime minAllowedDate = DateTime.UtcNow.AddYears(-2).Date;
        DateTime requestStart = requirements.PriceDataStart < minAllowedDate 
            ? minAllowedDate 
            : requirements.PriceDataStart;
        
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                ProgressTask task = ctx.AddTask($"[green]Downloading prices ({allSymbols.Count} symbols)...[/]");
                
                for (int i = 0; i < allSymbols.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    string symbol = allSymbols[i];
                    task.Description = $"[green]Price data: {symbol} ({i + 1}/{allSymbols.Count})[/]";
                    task.Value = (double)(i + 1) / allSymbols.Count * 100;
                    
                    try
                    {
                        // Generate map and factor files
                        await GenerateMapFileAsync(symbol, mapFilesPath);
                        await GenerateFactorFileAsync(symbol, factorFilesPath);
                        
                        // Download price data
                        IReadOnlyList<PriceBar> bars = await _polygonClient.GetHistoricalBarsAsync(
                            symbol, requestStart, requirements.EndDate);
                        
                        if (bars.Count > 0)
                        {
                            await SaveAsLeanZipAsync(symbol, bars, dailyPath);
                            downloaded++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to download prices for {Symbol}", symbol);
                    }
                }
            });
        
        return downloaded;
    }

    /// <summary>
    /// Downloads interest rate data.
    /// </summary>
    private async Task<bool> DownloadInterestRatesAsync(
        Alaris.Core.Model.STDT010A requirements,
        string sessionDataPath,
        CancellationToken cancellationToken)
    {
        try
        {
            string ratePath = Path.Combine(sessionDataPath, "alternative", "interest-rate", "usa");
            Directory.CreateDirectory(ratePath);
            string csvPath = Path.Combine(ratePath, "interest-rate.csv");
            
            IReadOnlyDictionary<DateTime, decimal> rates = await _treasuryClient!.GetHistoricalRatesAsync(
                requirements.PriceDataStart, requirements.EndDate, cancellationToken);
            
            if (rates.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                List<DateTime> rateDates = new List<DateTime>(rates.Keys);
                rateDates.Sort();
                foreach (DateTime rateDate in rateDates)
                {
                    sb.AppendLine($"{rateDate:yyyyMMdd},{rates[rateDate]}");
                }
                await File.WriteAllTextAsync(csvPath, sb.ToString(), cancellationToken);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to download interest rates");
        }
        
        return false;
    }

    /// <summary>
    /// Computes all dates where options data is required for signal generation.
    /// For each earnings event within the session range, we need options on dates
    /// [earnings - maxDays, earnings - minDays].
    /// </summary>
    private IReadOnlyList<DateTime> ComputeOptionsRequiredDates(
        string sessionDataPath,
        IReadOnlyList<string> symbols,
        DateTime startDate,
        DateTime endDate,
        int minDays,
        int maxDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDataPath);
        ArgumentNullException.ThrowIfNull(symbols);
        if (endDate < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "End date must be on or after start date.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(minDays);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDays);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDays, minDays, nameof(maxDays));

        HashSet<DateTime> dates = new HashSet<DateTime>();
        HashSet<string> symbolSet = new HashSet<string>(symbols, StringComparer.OrdinalIgnoreCase);
        string nasdaqPath = Path.Combine(sessionDataPath, "earnings", "nasdaq");
        
        if (!Directory.Exists(nasdaqPath))
        {
            _logger?.LogWarning("Earnings directory not found: {Path}", nasdaqPath);
            return Array.Empty<DateTime>();
        }
        
        // Read all earnings files and find dates for our symbols
        string[] files = Directory.GetFiles(nasdaqPath, "*.json");
        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                CachedEarningsDay? cached = JsonSerializer.Deserialize<CachedEarningsDay>(json, JsonOptions);
                
                if (cached?.Earnings == null)
                    continue;
                
                foreach (CachedEarningsEvent earning in cached.Earnings)
                {
                    // Check if this symbol is in our universe
                    if (!symbolSet.Contains(earning.Symbol))
                        continue;
                    
                    // For this earnings date, compute evaluation dates
                    for (int d = minDays; d <= maxDays; d++)
                    {
                        DateTime evalDate = earning.Date.AddDays(-d);
                        
                        // Only include if within session range
                        if (evalDate >= startDate && evalDate <= endDate)
                        {
                            // Skip weekends
                            if (evalDate.DayOfWeek != DayOfWeek.Saturday && 
                                evalDate.DayOfWeek != DayOfWeek.Sunday)
                            {
                                dates.Add(evalDate);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error reading earnings file: {File}", file);
            }
        }
        
        _logger?.LogInformation("Computed {Count} options-required dates from earnings calendar", dates.Count);
        List<DateTime> orderedDates = new List<DateTime>(dates);
        orderedDates.Sort();
        return orderedDates;
    }

    /// <summary>
    /// Cached earnings data structure for deserialization.
    /// </summary>
    private sealed class CachedEarningsDay
    {
        public DateTime Date { get; init; }
        public DateTime FetchedAt { get; init; }
        public IReadOnlyList<CachedEarningsEvent> Earnings { get; init; } = Array.Empty<CachedEarningsEvent>();
    }
    
    private sealed class CachedEarningsEvent
    {
        public string Symbol { get; init; } = string.Empty;
        public DateTime Date { get; init; }
    }

    private static List<DateTime> ApplyDateStride(IReadOnlyList<DateTime> dates, int strideDays)
    {
        if (dates.Count == 0 || strideDays <= 1)
        {
            return dates is List<DateTime> list ? list : new List<DateTime>(dates);
        }

        List<DateTime> filtered = new List<DateTime>();
        DateTime? lastIncluded = null;
        for (int i = 0; i < dates.Count; i++)
        {
            DateTime date = dates[i];
            if (lastIncluded == null || (date - lastIncluded.Value).TotalDays >= strideDays)
            {
                filtered.Add(date);
                lastIncluded = date;
            }
        }

        return filtered;
    }

    private static IReadOnlyDictionary<int, decimal> LoadDailyCloseCache(string sessionDataPath, string symbol)
    {
        string symbolLower = symbol.ToLowerInvariant();
        string zipPath = Path.Combine(sessionDataPath, "equity", "usa", "daily", $"{symbolLower}.zip");
        if (!File.Exists(zipPath))
        {
            return new Dictionary<int, decimal>();
        }

        Dictionary<int, decimal> closes = new Dictionary<int, decimal>();
        try
        {
            using FileStream stream = File.OpenRead(zipPath);
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            ZipArchiveEntry? entry = archive.GetEntry($"{symbolLower}.csv");
            if (entry == null)
            {
                return closes;
            }

            using StreamReader reader = new StreamReader(entry.Open());
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(',');
                if (parts.Length < 5)
                {
                    continue;
                }

                string datePart = parts[0];
                if (datePart.Length < 8)
                {
                    continue;
                }

                if (!int.TryParse(datePart.AsSpan(0, 8), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dateKey))
                {
                    continue;
                }

                if (!long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out long closeScaled))
                {
                    continue;
                }

                closes[dateKey] = closeScaled / 10000m;
            }
        }
        catch
        {
            return closes;
        }

        return closes;
    }

    public void Dispose()
    {
        // NasdaqEarningsProvider does not require explicit disposal
    }

    /// <summary>
    /// Saves bars to a LEAN-compatible ZIP file.
    /// </summary>
    private static async Task SaveAsLeanZipAsync(string symbol, IEnumerable<PriceBar> bars, string outputDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);

        // LEAN format: ticker.zip containing ticker.csv
        string ticker = symbol.ToLowerInvariant();
        string zipPath = Path.Combine(outputDir, $"{ticker}.zip");

        using MemoryStream memoryStream = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            ZipArchiveEntry entry = archive.CreateEntry($"{ticker}.csv");
            using Stream entryStream = entry.Open();
            using StreamWriter writer = new StreamWriter(entryStream);

            foreach (PriceBar bar in bars)
            {
                // Format: Date,Open,High,Low,Close,Volume
                // Date format: yyyyMMdd HH:mm
                // Prices: Scaled by 10000 ensures compatibility with older LEAN data readers,
                // but strictly speaking typical daily CSV is 
                // yyyyMMdd 00:00,open*10000,high*10000,low*10000,close*10000,volume
                
                // LEAN Daily resolution expects 'TwelveCharacter' format: "yyyyMMdd HH:mm"
                // TradeBar.cs ParseEquity uses default scaling (x10000)
                // CRITICAL: Daily bars must use 00:00 (exchange timezone midnight), not actual UTC timestamp
                string dateStr = bar.Timestamp.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + " 00:00";
                
                // Scale by 10000 to match LEAN default scale factor (1/10000)
                // When LEAN reads this, it divides by 10000 to get the original price.
                long open = (long)(bar.Open * 10000);
                long high = (long)(bar.High * 10000);
                long low = (long)(bar.Low * 10000);
                long close = (long)(bar.Close * 10000);
                long volume = (long)bar.Volume;

                await writer.WriteLineAsync(string.Format(CultureInfo.InvariantCulture, 
                    "{0},{1},{2},{3},{4},{5}", 
                    dateStr, open, high, low, close, volume));
            }
        }

        memoryStream.Position = 0;
        await File.WriteAllBytesAsync(zipPath, memoryStream.ToArray());
    }

    /// <summary>
    /// Copies system data folders (market-hours, symbol-properties) to the session data folder.
    /// LEAN requires these to function correctly.
    /// </summary>
    private void CopySystemFiles(string sessionDataPath)
    {
        // Find source Alaris.Lean/Data
        string? sourcePath = FindLeanDataPath();
        if (string.IsNullOrEmpty(sourcePath))
        {
            _logger?.LogWarning("Could not find Alaris.Lean/Data to copy system files. Backtest may fail.");
            return;
        }

        string[] foldersToCopy = new[] 
        { 
            "market-hours", 
            "symbol-properties",
            "equity/usa/map_files",
            "equity/usa/factor_files"
        };

        foreach (string folder in foldersToCopy)
        {
            string sourceDir = Path.Combine(sourcePath, folder);
            string destDir = Path.Combine(sessionDataPath, folder);

            if (Directory.Exists(sourceDir))
            {
                CopyDirectory(sourceDir, destDir);
                _logger?.LogInformation("Copied {Folder} to session data", folder);
            }
            else
            {
                _logger?.LogWarning("Source folder {Folder} not found at {Path}", folder, sourceDir);
            }
        }
    }

    private static string? FindLeanDataPath()
    {
        // Try common locations
        string[] candidates = new[]
        {
            "lib/Alaris.Lean/Data",
            "../lib/Alaris.Lean/Data",
            "../../lib/Alaris.Lean/Data",
            "Alaris.Lean/Data",
            "../Alaris.Lean/Data",
            "../../Alaris.Lean/Data"
        };

        foreach (string candidate in candidates)
        {
            string fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        string[] files = Directory.GetFiles(sourceDir);
        foreach (string file in files)
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        string[] subDirs = Directory.GetDirectories(sourceDir);
        foreach (string subDir in subDirs)
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }
}

/// <summary>
/// Report of bootstrap operation results.
/// </summary>
public sealed class BootstrapReport
{
    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; set; }
    public string SessionDataPath { get; init; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    public int PricesDownloaded { get; set; }
    public int EarningsDaysDownloaded { get; set; }
    public int OptionsRequiredDatesComputed { get; set; }
    public int OptionsDownloaded { get; set; }
    public bool InterestRatesDownloaded { get; set; }
    
    public TimeSpan Duration => CompletedAt - StartedAt;
    
    public string GetSummary() =>
        $"Bootstrap {(Success ? "succeeded" : "failed")} in {Duration.TotalSeconds:F1}s: " +
        $"Prices={PricesDownloaded}, Earnings={EarningsDaysDownloaded} days, " +
        $"Options={OptionsDownloaded} (from {OptionsRequiredDatesComputed} dates)";
}
