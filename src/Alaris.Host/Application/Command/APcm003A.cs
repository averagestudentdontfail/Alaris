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
        string? tickers = settings.Tickers ?? settings.Ticker;
        if (string.IsNullOrWhiteSpace(tickers))
        {
            AnsiConsole.MarkupLine("[red]Ticker required. Use --ticker AAPL or --tickers AAPL,MSFT[/]");
            return 1;
        }

        // Validate dates
        string fromDate = settings.FromDate ?? DateTime.Now.AddYears(-1).ToString("yyyyMMdd");
        string toDate = settings.ToDate ?? DateTime.Now.ToString("yyyyMMdd");

        // Parse dates
        if (!DateTime.TryParseExact(fromDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startDate))
        {
            AnsiConsole.MarkupLine($"[red]Invalid from date: {fromDate}. Use YYYYMMDD format.[/]");
            return 1;
        }

        if (!DateTime.TryParseExact(toDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endDate))
        {
            AnsiConsole.MarkupLine($"[red]Invalid to date: {toDate}. Use YYYYMMDD format.[/]");
            return 1;
        }
        if (endDate < startDate)
        {
            AnsiConsole.MarkupLine("[red]End date must be on or after start date.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[bold blue]Polygon Data Download[/]");
        AnsiConsole.WriteLine();

        Table table = new Table();
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
        string? apiKey = GetPolygonApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]Polygon API key not found![/]");
            AnsiConsole.MarkupLine("[grey]Add 'Polygon.ApiKey' to appsettings.local.jsonc[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Polygon API key found[/]");
        AnsiConsole.WriteLine();

        // Download data for each ticker with rate limiting
        string[] tickerList = tickers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tickerList.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]No valid tickers provided.[/]");
            return 1;
        }
        string dataPath = FindOrCreateDataPath();
        int totalBars = 0;
        int tickerCount = 0;

        AnsiConsole.MarkupLine($"[grey]Downloading {tickerList.Length} tickers (with rate limiting)...[/]");
        AnsiConsole.WriteLine();

        foreach (string ticker in tickerList)
        {
            tickerCount++;
            int bars = DownloadTickerData(ticker.Trim().ToUpperInvariant(), startDate, endDate, settings.Resolution, apiKey, dataPath);
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
                    using HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

                    // Map resolution to Polygon timespan
                    string resolutionKey = resolution.ToLowerInvariant();
                    int multiplier;
                    string timespan;
                    switch (resolutionKey)
                    {
                        case "minute":
                            multiplier = 1;
                            timespan = "minute";
                            break;
                        case "hour":
                            multiplier = 1;
                            timespan = "hour";
                            break;
                        case "daily":
                            multiplier = 1;
                            timespan = "day";
                            break;
                        default:
                            multiplier = 1;
                            timespan = "day";
                            break;
                    }

                    // Polygon aggregates endpoint
                    string url = $"https://api.polygon.io/v2/aggs/ticker/{ticker}/range/{multiplier}/{timespan}/{startDate:yyyy-MM-dd}/{endDate:yyyy-MM-dd}?adjusted=true&sort=asc&limit=50000&apiKey={apiKey}";

                    PolygonAggregatesResponse? response = httpClient.GetFromJsonAsync<PolygonAggregatesResponse>(url).GetAwaiter().GetResult();

                    if (response?.Results == null || response.Results.Length == 0)
                    {
                        AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(ticker)}: No data returned[/]");
                        return 0;
                    }

                    // Create output directory
                    string securityType = "equity";
                    string market = "usa";
                    string leanResolution;
                    switch (resolutionKey)
                    {
                        case "minute":
                            leanResolution = "minute";
                            break;
                        case "hour":
                            leanResolution = "hour";
                            break;
                        case "daily":
                            leanResolution = "daily";
                            break;
                        default:
                            leanResolution = "daily";
                            break;
                    }

                    string outputDir = System.IO.Path.Combine(dataPath, securityType, market, leanResolution, ticker.ToLowerInvariant());
                    System.IO.Directory.CreateDirectory(outputDir);

                    // Write data in LEAN format
                    int barCount = 0;
                    if (leanResolution == "daily")
                    {
                        // Daily format: single CSV file
                        string filePath = System.IO.Path.Combine(outputDir, $"{ticker.ToLowerInvariant()}.csv");
                        using System.IO.StreamWriter writer = new System.IO.StreamWriter(filePath);

                        foreach (PolygonBar bar in response.Results)
                        {
                            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp).UtcDateTime;
                            // LEAN daily format: Date,Open,High,Low,Close,Volume (all prices * 10000)
                            string line = $"{date:yyyyMMdd 00:00},{bar.Open * 10000m:F0},{bar.High * 10000m:F0},{bar.Low * 10000m:F0},{bar.Close * 10000m:F0},{bar.Volume}";
                            writer.WriteLine(line);
                            barCount++;
                        }
                    }
                    else
                    {
                        // Minute/Hour format: files by date
                        Dictionary<DateTime, List<PolygonBar>> barsByDate = new Dictionary<DateTime, List<PolygonBar>>();
                        foreach (PolygonBar bar in response.Results)
                        {
                            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp).UtcDateTime.Date;
                            if (!barsByDate.TryGetValue(date, out List<PolygonBar>? dayBars))
                            {
                                dayBars = new List<PolygonBar>();
                                barsByDate.Add(date, dayBars);
                            }
                            dayBars.Add(bar);
                        }

                        List<DateTime> dateKeys = new List<DateTime>(barsByDate.Keys);
                        dateKeys.Sort();

                        foreach (DateTime dateKey in dateKeys)
                        {
                            List<PolygonBar> dayBars = barsByDate[dateKey];
                            dayBars.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));

                            string fileName = $"{dateKey:yyyyMMdd}_{ticker.ToLowerInvariant()}_trade.csv";
                            string filePath = System.IO.Path.Combine(outputDir, fileName);
                            using System.IO.StreamWriter writer = new System.IO.StreamWriter(filePath);

                            foreach (PolygonBar bar in dayBars)
                            {
                                DateTime time = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp).UtcDateTime;
                                // LEAN minute format: Milliseconds,Open,High,Low,Close,Volume
                                long ms = (long)(time - dateKey).TotalMilliseconds;
                                string line = $"{ms},{bar.Open * 10000m:F0},{bar.High * 10000m:F0},{bar.Low * 10000m:F0},{bar.Close * 10000m:F0},{bar.Volume}";
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
        string? dataPath = FindDataPath();
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
        Table table = new Table();
        table.AddColumn("[grey]Category[/]");
        table.AddColumn("[grey]Files[/]");
        table.AddColumn("[grey]Size[/]");

        string[] dirs = System.IO.Directory.GetDirectories(dataPath);
        Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
        foreach (string dir in dirs)
        {
            string name = System.IO.Path.GetFileName(dir);
            string[] files = System.IO.Directory.GetFiles(dir, "*", System.IO.SearchOption.AllDirectories);
            long size = 0;
            foreach (string file in files)
            {
                size += new System.IO.FileInfo(file).Length;
            }
            string sizeStr = FormatSize(size);
            table.AddRow($"[blue]{name}/[/]", files.Length.ToString(), sizeStr);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static int ShowDataStatus()
    {
        string? dataPath = FindDataPath();

        AnsiConsole.MarkupLine("[bold blue]Data Status[/]");
        AnsiConsole.WriteLine();

        Table table = new Table();
        table.AddColumn("[grey]Setting[/]");
        table.AddColumn("[white]Value[/]");

        table.AddRow("Data Folder", dataPath ?? "(not found)");

        string? polygonKey = GetPolygonApiKey();
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
        string[] paths = new[]
        {
            "Alaris.Lean/Data",
            "../Alaris.Lean/Data",
            "../../Alaris.Lean/Data"
        };

        foreach (string path in paths)
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
        string? existing = FindDataPath();
        if (existing != null)
        {
            return existing;
        }

        // Create default data path
        string defaultPath = "Alaris.Lean/Data";
        string[] paths = new[] { defaultPath, "../Alaris.Lean/Data", "../../Alaris.Lean/Data" };

        foreach (string path in paths)
        {
            string? parentDir = System.IO.Path.GetDirectoryName(path);
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
        string[] paths = new[]
        {
            "appsettings.local.jsonc",
            "../appsettings.local.jsonc",
            "../../appsettings.local.jsonc"
        };

        foreach (string path in paths)
        {
            if (System.IO.File.Exists(path))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(path);
                    // Simple extraction - look for "ApiKey" in Polygon section
                    string[] lines = json.Split('\n');
                    bool inPolygon = false;
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("//")) continue;

                        if (trimmed.Contains("\"Polygon\""))
                        {
                            inPolygon = true;
                        }
                        else if (inPolygon && trimmed.Contains("\"ApiKey\""))
                        {
                            int colonIdx = trimmed.IndexOf(':');
                            if (colonIdx > 0)
                            {
                                string value = trimmed[(colonIdx + 1)..].Trim().Trim(',', '"', ' ');
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
