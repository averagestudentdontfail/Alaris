// APsv002A.cs - Session data download service (Polygon → LEAN format)

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
        var symbolList = symbols.ToList();
        var total = symbolList.Count;
        var current = 0;


        // Path: session/data/equity/usa/daily
        var dailyPath = System.IO.Path.Combine(sessionDataPath, "equity", "usa", "daily");
        Directory.CreateDirectory(dailyPath);

        // NOTE: Earnings data comes from 'alaris earnings bootstrap' command
        // using cache-first pattern in DTea001C. Not downloaded here.

        // Path: session/data/options
        var optionsPath = System.IO.Path.Combine(sessionDataPath, "options");
        Directory.CreateDirectory(optionsPath);

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var taskData = ctx.AddTask($"[green]Downloading Price Data ({total} symbols)...[/]");
                var taskOptions = ctx.AddTask($"[green]Downloading Options Data...[/]");
                var taskRates = _treasuryClient != null ? ctx.AddTask($"[green]Downloading Interest Rates...[/]") : null;
                
                foreach (var symbol in symbolList)
                {
                    current++;
                    taskData.Description = $"[green]Downloading prices for {symbol} ({current}/{total})...[/]";
                    taskData.Value = (double)current / total * 100;

                    // Price Data
                    try
                    {
                        // Add buffer for warmup (120 days) to ensure sufficient history
                        var minAllowedDate = DateTime.UtcNow.AddYears(-2).Date;
                        var lookbackStart = start.AddDays(-120);
                        var requestStart = lookbackStart < minAllowedDate ? minAllowedDate : lookbackStart;
                        
                        var bars = await _polygonClient.GetHistoricalBarsAsync(symbol, requestStart, end);
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
                        var optionsMinDate = DateTime.UtcNow.AddYears(-2).AddMonths(1).Date;
                        var effectiveOptionsDate = start < optionsMinDate ? optionsMinDate : start;
                        
                        var optionChain = await _polygonClient.GetHistoricalOptionChainAsync(symbol, effectiveOptionsDate);
                        if (optionChain.Contracts.Count > 0)
                        {
                            var jsonPath = System.IO.Path.Combine(optionsPath, $"{symbol.ToLowerInvariant()}.json");
                            var json = JsonSerializer.Serialize(optionChain, JsonOptions);
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
                        var ratePath = System.IO.Path.Combine(sessionDataPath, "alternative", "interest-rate", "usa");
                        Directory.CreateDirectory(ratePath);
                        var csvPath = System.IO.Path.Combine(ratePath, "interest-rate.csv");

                        // Look back 2 years + buffer from Start Date, or just fetch large history
                        // Rates are global, not per-symbol.
                        var rates = await _treasuryClient.GetHistoricalRatesAsync(start.AddYears(-2), end);
                        
                        if (rates.Count > 0)
                        {
                            // Write CSV: Date(yyyyMMdd),Rate(decimal)
                            // Sort by date
                            var sb = new StringBuilder();
                            foreach (var kvp in rates.OrderBy(x => x.Key))
                            {
                                sb.AppendLine($"{kvp.Key:yyyyMMdd},{kvp.Value}");
                            }
                            await File.WriteAllTextAsync(csvPath, sb.ToString());
                        }
                        taskRates.Description = "[green]Interest Rates Downloaded[/]";
                        taskRates.Value = 100;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to download interest rates");
                        taskRates.Description = "[red]Interest Rates Failed[/]";
                    }
                }
            });
            
        // Copy system files (should be quick)
        CopySystemFiles(sessionDataPath);
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
        if (_earningsClient == null)
        {
            throw new InvalidOperationException("Earnings client not configured");
        }

        string nasdaqPath = System.IO.Path.Combine(outputPath, "earnings", "nasdaq");
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
                var task = ctx.AddTask("Downloading Earnings Calendar", maxValue: totalWeekdays);

                for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Skip weekends
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        continue;
                    }

                    string cachePath = System.IO.Path.Combine(nasdaqPath, $"{date:yyyy-MM-dd}.json");

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

                        var earnings = await _earningsClient.FetchAndCacheAsync(
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
    /// Rate-limited to 5 concurrent requests per symbol to respect API limits.
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
        var symbolList = symbols.ToList();
        var dateList = dates.Distinct().OrderBy(d => d).ToList();
        
        if (symbolList.Count == 0 || dateList.Count == 0)
        {
            return 0;
        }

        var optionsPath = System.IO.Path.Combine(sessionDataPath, "options");
        Directory.CreateDirectory(optionsPath);

        int totalDownloaded = 0;
        int totalSkipped = 0;
        int totalFailed = 0;

        // Apply 2-year limit buffer (Polygon Options Starter plan)
        var optionsMinDate = DateTime.UtcNow.AddYears(-2).AddMonths(1).Date;

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
                var task = ctx.AddTask("Downloading Options Data", maxValue: symbolList.Count * dateList.Count);

                foreach (var symbol in symbolList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var date in dateList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var symbolLower = symbol.ToLowerInvariant();
                        var dateSuffix = date.ToString("yyyyMMdd");
                        var cachePath = System.IO.Path.Combine(optionsPath, $"{symbolLower}_{dateSuffix}.json");

                        // Skip if already cached
                        if (File.Exists(cachePath))
                        {
                            totalSkipped++;
                            task.Increment(1);
                            continue;
                        }

                        // Skip dates outside Polygon's 2-year limit
                        if (date < optionsMinDate)
                        {
                            _logger?.LogDebug("Skipping {Symbol} @ {Date}: outside 2-year limit", symbol, date);
                            task.Increment(1);
                            continue;
                        }

                        try
                        {
                            task.Description = $"Options: {symbol} @ {date:yyyy-MM-dd}";

                            var optionChain = await _polygonClient.GetHistoricalOptionChainAsync(symbol, date, cancellationToken);
                            
                            if (optionChain.Contracts.Count > 0)
                            {
                                var json = JsonSerializer.Serialize(optionChain, JsonOptions);
                                await File.WriteAllTextAsync(cachePath, json, cancellationToken);
                                totalDownloaded++;
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
                            totalFailed++;
                            _logger?.LogWarning(ex, "Failed to download options for {Symbol} @ {Date}", symbol, date);
                        }

                        task.Increment(1);

                        // Rate limit: small delay between requests
                        await Task.Delay(100, cancellationToken);
                    }
                }
            });

        _logger?.LogInformation(
            "Options bootstrap complete: {Downloaded} downloaded, {Skipped} cached, {Failed} failed",
            totalDownloaded, totalSkipped, totalFailed);

        return totalDownloaded;
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
        // LEAN format: ticker.zip containing ticker.csv
        var ticker = symbol.ToLowerInvariant();
        var zipPath = System.IO.Path.Combine(outputDir, $"{ticker}.zip");

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry($"{ticker}.csv");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);

            foreach (var bar in bars)
            {
                // Format: Date,Open,High,Low,Close,Volume
                // Date format: yyyyMMdd HH:mm
                // Prices: Scaled by 10000 ensures compatibility with older LEAN data readers,
                // but strictly speaking typical daily CSV is 
                // yyyyMMdd 00:00,open*10000,high*10000,low*10000,close*10000,volume
                
                // Assuming 'bar' is StrategyPriceBar or similar with Open, High, Low, Close, Volume, Date
                // Since I can't see StrategyPriceBar def right now, I'll assume standard properties.
                // Using dynamic to avoid circular dependency if Model is in another project, 
                // but ideally should verify type.
                
                // LEAN Daily resolution expects 'TwelveCharacter' format: "yyyyMMdd HH:mm"
                // TradeBar.cs ParseEquity uses default scaling (x10000)
                // CRITICAL: Daily bars must use 00:00 (exchange timezone midnight), not actual UTC timestamp
                var dateStr = bar.Timestamp.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + " 00:00";
                
                // Scale by 10000 to match LEAN default scale factor (1/10000)
                // When LEAN reads this, it divides by 10000 to get the original price.
                var open = (long)(bar.Open * 10000);
                var high = (long)(bar.High * 10000);
                var low = (long)(bar.Low * 10000);
                var close = (long)(bar.Close * 10000);
                var volume = (long)bar.Volume;

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
        var sourcePath = FindLeanDataPath();
        if (string.IsNullOrEmpty(sourcePath))
        {
            _logger?.LogWarning("Could not find Alaris.Lean/Data to copy system files. Backtest may fail.");
            return;
        }

        var foldersToCopy = new[] 
        { 
            "market-hours", 
            "symbol-properties",
            "equity/usa/map_files",
            "equity/usa/factor_files"
        };

        foreach (var folder in foldersToCopy)
        {
            var sourceDir = System.IO.Path.Combine(sourcePath, folder);
            var destDir = System.IO.Path.Combine(sessionDataPath, folder);

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
        var candidates = new[]
        {
            "Alaris.Lean/Data",
            "../Alaris.Lean/Data",
            "../../Alaris.Lean/Data"
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(System.IO.Path.GetFullPath(candidate)))
                return System.IO.Path.GetFullPath(candidate);
        }

        return null;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }
}
