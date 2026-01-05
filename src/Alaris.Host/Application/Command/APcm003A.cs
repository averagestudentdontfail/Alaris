// APcm003A.cs - 'alaris data download' command for Polygon data

using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Alaris.Host.Application.Command;

/// <summary>
/// Data Command settings.
/// </summary>
public sealed class DataSettings : CommandSettings
{
    [CommandArgument(0, "[action]")]
    [Description("Action: download, list, status")]
    [DefaultValue("list")]
    public string Action { get; init; } = "list";

    [CommandOption("-t|--ticker <TICKER>")]
    [Description("Ticker symbol (e.g., AAPL)")]
    public string? Ticker { get; init; }

    [CommandOption("--tickers <TICKERS>")]
    [Description("Multiple tickers (comma-separated: AAPL,MSFT,GOOGL)")]
    public string? Tickers { get; init; }

    [CommandOption("--source <SOURCE>")]
    [Description("Data source: polygon")]
    [DefaultValue("polygon")]
    public string Source { get; init; } = "polygon";

    [CommandOption("--type <TYPE>")]
    [Description("Data type: equity, option")]
    [DefaultValue("equity")]
    public string DataType { get; init; } = "equity";

    [CommandOption("--resolution <RESOLUTION>")]
    [Description("Resolution: minute, hour, daily")]
    [DefaultValue("daily")]
    public string Resolution { get; init; } = "daily";

    [CommandOption("--from <DATE>")]
    [Description("Start date (YYYYMMDD format)")]
    public string? FromDate { get; init; }

    [CommandOption("--to <DATE>")]
    [Description("End date (YYYYMMDD format)")]
    public string? ToDate { get; init; }
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
            "status" => ShowDataStatus(),
            _ => InvalidAction(settings.Action)
        };
    }

    private static int DownloadData(DataSettings settings)
    {
        var tickers = settings.Tickers ?? settings.Ticker;
        if (tickers is null)
        {
            AnsiConsole.MarkupLine("[red]Ticker required. Use --ticker AAPL or --tickers AAPL,MSFT[/]");
            return 1;
        }

        // Validate dates
        var fromDate = settings.FromDate ?? DateTime.Now.AddYears(-1).ToString("yyyyMMdd");
        var toDate = settings.ToDate ?? DateTime.Now.ToString("yyyyMMdd");

        // Parse dates
        if (!DateTime.TryParseExact(fromDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
        {
            AnsiConsole.MarkupLine($"[red]Invalid from date: {fromDate}. Use YYYYMMDD format.[/]");
            return 1;
        }

        if (!DateTime.TryParseExact(toDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            AnsiConsole.MarkupLine($"[red]Invalid to date: {toDate}. Use YYYYMMDD format.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[bold blue]Polygon Data Download[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("[grey]Parameter[/]");
        table.AddColumn("[white]Value[/]");
        table.AddRow("Source", settings.Source);
        table.AddRow("Type", settings.DataType);
        table.AddRow("Resolution", settings.Resolution);
        table.AddRow("Tickers", tickers);
        table.AddRow("From", startDate.ToString("yyyy-MM-dd"));
        table.AddRow("To", endDate.ToString("yyyy-MM-dd"));
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Get Polygon API key
        var apiKey = GetPolygonApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]Polygon API key not found![/]");
            AnsiConsole.MarkupLine("[grey]Add 'Polygon.ApiKey' to appsettings.local.jsonc[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Polygon API key found[/]");
        AnsiConsole.WriteLine();

        // Download data for each ticker with rate limiting
        var tickerList = tickers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var dataPath = FindOrCreateDataPath();
        var totalBars = 0;
        var tickerCount = 0;

        AnsiConsole.MarkupLine($"[grey]Downloading {tickerList.Length} tickers (with rate limiting)...[/]");
        AnsiConsole.WriteLine();

        foreach (var ticker in tickerList)
        {
            tickerCount++;
            var bars = DownloadTickerData(ticker.Trim().ToUpperInvariant(), startDate, endDate, settings.Resolution, apiKey, dataPath);
            totalBars += bars;

            // Rate limiting: wait 12 seconds between requests for free Polygon tier (5 calls/min)
            if (tickerCount < tickerList.Length)
            {
                AnsiConsole.MarkupLine("[grey]  Waiting 12s for rate limit...[/]");
                System.Threading.Thread.Sleep(12000);
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Download complete! {totalBars:N0} bars saved to {Markup.Escape(dataPath)}[/]");
        return 0;
    }

    private static int DownloadTickerData(string ticker, DateTime startDate, DateTime endDate, string resolution, string apiKey, string dataPath)
    {
        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start($"[yellow]Downloading {Markup.Escape(ticker)}...[/]", ctx =>
            {
                try
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

                    // Map resolution to Polygon timespan
                    var (multiplier, timespan) = resolution.ToLowerInvariant() switch
                    {
                        "minute" => (1, "minute"),
                        "hour" => (1, "hour"),
                        "daily" => (1, "day"),
                        _ => (1, "day")
                    };

                    // Polygon aggregates endpoint
                    var url = $"https://api.polygon.io/v2/aggs/ticker/{ticker}/range/{multiplier}/{timespan}/{startDate:yyyy-MM-dd}/{endDate:yyyy-MM-dd}?adjusted=true&sort=asc&limit=50000&apiKey={apiKey}";

                    var response = httpClient.GetFromJsonAsync<PolygonAggregatesResponse>(url).GetAwaiter().GetResult();

                    if (response?.Results == null || response.Results.Length == 0)
                    {
                        AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(ticker)}: No data returned[/]");
                        return 0;
                    }

                    // Create output directory
                    var securityType = "equity";
                    var market = "usa";
                    var leanResolution = resolution.ToLowerInvariant() switch
                    {
                        "minute" => "minute",
                        "hour" => "hour",
                        "daily" => "daily",
                        _ => "daily"
                    };

                    var outputDir = System.IO.Path.Combine(dataPath, securityType, market, leanResolution, ticker.ToLowerInvariant());
                    System.IO.Directory.CreateDirectory(outputDir);

                    // Write data in LEAN format
                    var barCount = 0;
                    if (leanResolution == "daily")
                    {
                        // Daily format: single CSV file
                        var filePath = System.IO.Path.Combine(outputDir, $"{ticker.ToLowerInvariant()}.csv");
                        using var writer = new System.IO.StreamWriter(filePath);

                        foreach (var bar in response.Results)
                        {
                            var date = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp).UtcDateTime;
                            // LEAN daily format: Date,Open,High,Low,Close,Volume (all prices * 10000)
                            var line = $"{date:yyyyMMdd 00:00},{bar.Open * 10000m:F0},{bar.High * 10000m:F0},{bar.Low * 10000m:F0},{bar.Close * 10000m:F0},{bar.Volume}";
                            writer.WriteLine(line);
                            barCount++;
                        }
                    }
                    else
                    {
                        // Minute/Hour format: files by date
                        var barsByDate = response.Results.GroupBy(b =>
                            DateTimeOffset.FromUnixTimeMilliseconds(b.Timestamp).UtcDateTime.Date);

                        foreach (var dateGroup in barsByDate)
                        {
                            var fileName = $"{dateGroup.Key:yyyyMMdd}_{ticker.ToLowerInvariant()}_trade.csv";
                            var filePath = System.IO.Path.Combine(outputDir, fileName);

                            using var writer = new System.IO.StreamWriter(filePath);

                            foreach (var bar in dateGroup.OrderBy(b => b.Timestamp))
                            {
                                var time = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp).UtcDateTime;
                                // LEAN minute format: Milliseconds,Open,High,Low,Close,Volume
                                var ms = (long)(time - dateGroup.Key).TotalMilliseconds;
                                var line = $"{ms},{bar.Open * 10000m:F0},{bar.High * 10000m:F0},{bar.Low * 10000m:F0},{bar.Close * 10000m:F0},{bar.Volume}";
                                writer.WriteLine(line);
                                barCount++;
                            }
                        }
                    }

                    AnsiConsole.MarkupLine($"  [green]{Markup.Escape(ticker)}:[/] {barCount:N0} bars saved");
                    return barCount;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(ticker)}: Error - {Markup.Escape(ex.Message)}[/]");
                    return 0;
                }
            });
    }

    private static int ListData()
    {
        var dataPath = FindDataPath();
        if (dataPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find data folder[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[bold blue]Data Folder:[/] [grey]{dataPath}[/]");
        AnsiConsole.WriteLine();

        if (!System.IO.Directory.Exists(dataPath))
        {
            AnsiConsole.MarkupLine("[grey]No data folder exists yet[/]");
            return 0;
        }

        // Show data categories with file counts
        var table = new Table();
        table.AddColumn("[grey]Category[/]");
        table.AddColumn("[grey]Files[/]");
        table.AddColumn("[grey]Size[/]");

        var dirs = System.IO.Directory.GetDirectories(dataPath);
        foreach (var dir in dirs.OrderBy(d => d))
        {
            var name = System.IO.Path.GetFileName(dir);
            var files = System.IO.Directory.GetFiles(dir, "*", System.IO.SearchOption.AllDirectories);
            var size = files.Sum(f => new System.IO.FileInfo(f).Length);
            var sizeStr = FormatSize(size);
            table.AddRow($"[blue]{name}/[/]", files.Length.ToString(), sizeStr);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static int ShowDataStatus()
    {
        var dataPath = FindDataPath();

        AnsiConsole.MarkupLine("[bold blue]Data Status[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("[grey]Setting[/]");
        table.AddColumn("[white]Value[/]");

        table.AddRow("Data Folder", dataPath ?? "(not found)");

        var polygonKey = GetPolygonApiKey();
        table.AddRow("Polygon API Key", string.IsNullOrEmpty(polygonKey) ? "[red]Not configured[/]" : "[green]Configured[/]");

        AnsiConsole.Write(table);
        return 0;
    }

    private static int InvalidAction(string action)
    {
        AnsiConsole.MarkupLine($"[red]Unknown action: {action}[/]");
        AnsiConsole.MarkupLine("[grey]Available actions: download, list, status[/]");
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
            if (System.IO.Directory.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }

        return null;
    }

    private static string FindOrCreateDataPath()
    {
        var existing = FindDataPath();
        if (existing != null) return existing;

        // Create default data path
        var defaultPath = "Alaris.Lean/Data";
        var paths = new[] { defaultPath, "../Alaris.Lean/Data", "../../Alaris.Lean/Data" };

        foreach (var path in paths)
        {
            var parentDir = System.IO.Path.GetDirectoryName(path);
            if (parentDir != null && System.IO.Directory.Exists(parentDir))
            {
                System.IO.Directory.CreateDirectory(path);
                return System.IO.Path.GetFullPath(path);
            }
        }

        return System.IO.Path.GetFullPath(defaultPath);
    }

    private static string? GetPolygonApiKey()
    {
        // Try to find and read appsettings.local.jsonc
        var paths = new[]
        {
            "appsettings.local.jsonc",
            "../appsettings.local.jsonc",
            "../../appsettings.local.jsonc"
        };

        foreach (var path in paths)
        {
            if (System.IO.File.Exists(path))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(path);
                    // Simple extraction - look for "ApiKey" in Polygon section
                    var lines = json.Split('\n');
                    var inPolygon = false;
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("//")) continue;

                        if (trimmed.Contains("\"Polygon\""))
                        {
                            inPolygon = true;
                        }
                        else if (inPolygon && trimmed.Contains("\"ApiKey\""))
                        {
                            var colonIdx = trimmed.IndexOf(':');
                            if (colonIdx > 0)
                            {
                                var value = trimmed[(colonIdx + 1)..].Trim().Trim(',', '"', ' ');
                                if (!string.IsNullOrEmpty(value))
                                    return value;
                            }
                        }
                        else if (inPolygon && trimmed.StartsWith("}"))
                        {
                            inPolygon = false;
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }
            }
        }

        return null;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

file sealed class PolygonAggregatesResponse
{
    [JsonPropertyName("results")]
    public PolygonBar[]? Results { get; init; }

    [JsonPropertyName("resultsCount")]
    public int ResultsCount { get; init; }
}

file sealed class PolygonBar
{
    [JsonPropertyName("t")]
    public long Timestamp { get; init; }

    [JsonPropertyName("o")]
    public decimal Open { get; init; }

    [JsonPropertyName("h")]
    public decimal High { get; init; }

    [JsonPropertyName("l")]
    public decimal Low { get; init; }

    [JsonPropertyName("c")]
    public decimal Close { get; init; }

    [JsonPropertyName("v")]
    public double Volume { get; init; }
}
