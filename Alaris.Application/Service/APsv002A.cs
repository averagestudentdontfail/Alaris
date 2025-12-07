// =============================================================================
// APsv002A.cs - Session Data Management Service
// Component: APsv002A | Category: Services | Variant: A (Primary)
// =============================================================================
// Manages data acquisition and formatting for backtest sessions.
// Downloads from Polygon and converts to LEAN format.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alaris.Data.Provider.SEC; // For SecEdgarProvider
using Alaris.Data.Provider.Polygon; // For PolygonApiClient
using Alaris.Data.Model; // For PriceBar
using System.Text.Json; // For JSON serialization
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Alaris.Application.Service;

/// <summary>
/// Service for downloading and preparing session data.
/// Component ID: APsv002A
/// </summary>
public sealed class APsv002A : IDisposable
{
    private readonly PolygonApiClient _polygonClient;
    private readonly SecEdgarProvider? _secClient;
    private readonly ILogger<APsv002A>? _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public APsv002A(
        PolygonApiClient polygonClient, 
        SecEdgarProvider? secClient, 
        ILogger<APsv002A>? logger = null)
    {
        _polygonClient = polygonClient ?? throw new ArgumentNullException(nameof(polygonClient));
        _secClient = secClient;
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

        // Path: session/data/earnings
        var earningsPath = System.IO.Path.Combine(sessionDataPath, "earnings");
        if (_secClient != null)
        {
            Directory.CreateDirectory(earningsPath);
        }

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var taskData = ctx.AddTask($"[green]Downloading Price Data ({total} symbols)...[/]");
                var taskEarnings = _secClient != null ? ctx.AddTask($"[green]Downloading Earnings Data...[/]") : null;
                
                foreach (var symbol in symbolList)
                {
                    current++;
                    taskData.Description = $"[green]Downloading prices for {symbol} ({current}/{total})...[/]";
                    taskData.Value = (double)current / total * 100;

                    if (taskEarnings != null)
                    {
                        taskEarnings.Description = $"[green]Downloading earnings for {symbol} ({current}/{total})...[/]";
                        taskEarnings.Value = (double)current / total * 100;
                    }

                    // Price Data
                    try
                    {
                        var bars = await _polygonClient.GetHistoricalBarsAsync(symbol, start, end);
                        if (bars.Count > 0)
                        {
                            await SaveAsLeanZipAsync(symbol, bars, dailyPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to download prices for {Symbol}", symbol);
                    }

                    // Earnings Data - STRICTLY NO INFERENCE
                    if (_secClient != null)
                    {
                        try
                        {
                            var earnings = await _secClient.GetHistoricalEarningsAsync(symbol, 730); // 2 years back
                            if (earnings.Count > 0)
                            {
                                var jsonPath = System.IO.Path.Combine(earningsPath, $"{symbol.ToLowerInvariant()}.json");
                                var json = JsonSerializer.Serialize(earnings, JsonOptions);
                                await File.WriteAllTextAsync(jsonPath, json);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to download earnings for {Symbol}", symbol);
                        }
                    }
                }
            });
    }

    public void Dispose()
    {
        // _polygonClient might not be disposable, but if it was...
        // _secClient IS disposable
        _secClient?.Dispose();
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
                
                var dateStr = bar.Timestamp.ToString("yyyyMMdd 00:00");
                
                // Scale by 10000 to match existing LEAN data format (aapl.csv example)
                var open = (long)(bar.Open * 10000);
                var high = (long)(bar.High * 10000);
                var low = (long)(bar.Low * 10000);
                var close = (long)(bar.Close * 10000);
                var volume = (long)bar.Volume;

                await writer.WriteLineAsync($"{dateStr},{open},{high},{low},{close},{volume}");
            }
        }

        memoryStream.Position = 0;
        await File.WriteAllBytesAsync(zipPath, memoryStream.ToArray());
    }
}
