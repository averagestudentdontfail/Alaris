// APcm004A.cs - 'alaris universe generate' command for Polygon universe files

using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Configuration;

namespace Alaris.Host.Application.Command;

/// <summary>
/// Universe command settings.
/// </summary>
public sealed class UniverseSettings : CommandSettings
{
    [CommandArgument(0, "[action]")]
    [Description("Action: generate, list, info")]
    [DefaultValue("list")]
    public string Action { get; init; } = "list";

    [CommandOption("--from <DATE>")]
    [Description("Start date (YYYYMMDD format)")]
    public string? FromDate { get; init; }

    [CommandOption("--to <DATE>")]
    [Description("End date (YYYYMMDD format)")]
    public string? ToDate { get; init; }

    [CommandOption("--min-volume <VOLUME>")]
    [Description("Minimum dollar volume (default: 1500000)")]
    [DefaultValue(1_500_000)]
    public decimal MinDollarVolume { get; init; } = 1_500_000m;

    [CommandOption("--min-price <PRICE>")]
    [Description("Minimum stock price (default: 5)")]
    [DefaultValue(5.0)]
    public decimal MinPrice { get; init; } = 5.00m;
}

/// <summary>
/// Alaris Universe Command - generate and manage universe files.
/// Component ID: APcm004A
/// </summary>
public sealed class APcm004A : Command<UniverseSettings>
{
    public override int Execute(CommandContext context, UniverseSettings settings)
    {
        return settings.Action.ToLowerInvariant() switch
        {
            "generate" => GenerateUniverse(settings),
            "list" => ListUniverseFiles(),
            "info" => ShowUniverseInfo(),
            _ => InvalidAction(settings.Action)
        };
    }

    private static int GenerateUniverse(UniverseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MinPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MinDollarVolume);

        // Validate dates
        string fromDate = settings.FromDate ?? DateTime.Now.AddMonths(-3).ToString("yyyyMMdd");
        string toDate = settings.ToDate ?? DateTime.Now.ToString("yyyyMMdd");

        if (!DateTime.TryParseExact(fromDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startDate))
        {
            AnsiConsole.MarkupLine($"[red]Invalid from date: {Markup.Escape(fromDate)}. Use YYYYMMDD format.[/]");
            return 1;
        }

        if (!DateTime.TryParseExact(toDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endDate))
        {
            AnsiConsole.MarkupLine($"[red]Invalid to date: {Markup.Escape(toDate)}. Use YYYYMMDD format.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[bold blue]Polygon Universe Generator[/]");
        AnsiConsole.WriteLine();

        Table table = new Table();
        table.AddColumn("[grey]Parameter[/]");
        table.AddColumn("[white]Value[/]");
        table.AddRow("From", startDate.ToString("yyyy-MM-dd"));
        table.AddRow("To", endDate.ToString("yyyy-MM-dd"));
        table.AddRow("Min Dollar Volume", settings.MinDollarVolume.ToString("C0"));
        table.AddRow("Min Price", settings.MinPrice.ToString("C2"));
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

        // Calculate trading days
        List<DateTime> tradingDays = GetTradingDays(startDate, endDate);
        AnsiConsole.MarkupLine($"[grey]Processing {tradingDays.Count} trading days...[/]");
        AnsiConsole.WriteLine();

        // Create output directory
        string dataPath = FindOrCreateDataPath();
        string universeDir = System.IO.Path.Combine(dataPath, "equity", "usa", "fundamental", "coarse");
        System.IO.Directory.CreateDirectory(universeDir);

        int processedDays = 0;
        int totalStocks = 0;

        for (int i = 0; i < tradingDays.Count; i++)
        {
            DateTime date = tradingDays[i];
            (bool success, int stockCount, string? error) = GenerateUniverseForDate(
                date,
                apiKey,
                universeDir,
                settings.MinDollarVolume,
                settings.MinPrice);
            
            if (success)
            {
                processedDays++;
                totalStocks += stockCount;
                AnsiConsole.MarkupLine($"  [green]{date:yyyy-MM-dd}:[/] {stockCount:N0} stocks");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [yellow]{date:yyyy-MM-dd}:[/] {Markup.Escape(error ?? "Unknown error")}");
            }

            // Rate limiting: 5 calls per minute for free tier
            if (i < tradingDays.Count - 1)
            {
                System.Threading.Thread.Sleep(12000);
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Universe generation complete![/]");
        AnsiConsole.MarkupLine($"  Days processed: {processedDays}");
        AnsiConsole.MarkupLine($"  Average stocks/day: {(processedDays > 0 ? totalStocks / processedDays : 0):N0}");
        AnsiConsole.MarkupLine($"  Output: {Markup.Escape(universeDir)}");
        
        return 0;
    }

    private static (bool success, int stockCount, string? error) GenerateUniverseForDate(
        DateTime date,
        string apiKey,
        string outputDir,
        decimal minDollarVolume,
        decimal minPrice)
    {
        try
        {
            using HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

            string dateStr = date.ToString("yyyy-MM-dd");
            string url = $"https://api.polygon.io/v2/aggs/grouped/locale/us/market/stocks/{dateStr}?adjusted=true&apiKey={apiKey}";

            // First get the raw response to check for errors
            HttpResponseMessage httpResponse = httpClient.GetAsync(url).GetAwaiter().GetResult();
            string content = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            
            if (!httpResponse.IsSuccessStatusCode)
            {
                return (false, 0, $"HTTP {(int)httpResponse.StatusCode}: {content.Substring(0, Math.Min(100, content.Length))}");
            }

            PolygonGroupedResponse? response = System.Text.Json.JsonSerializer.Deserialize<PolygonGroupedResponse>(content);

            if (response == null)
            {
                return (false, 0, "Failed to parse response");
            }

            if (response.Status != null && response.Status != "OK")
            {
                return (false, 0, $"API status: {response.Status}");
            }

            if (response.Results == null || response.Results.Length == 0)
            {
                return (false, 0, "No results in response");
            }

            // Filter stocks
            List<PolygonGroupedResult> filteredStocks = new List<PolygonGroupedResult>(response.Results.Length);
            foreach (PolygonGroupedResult result in response.Results)
            {
                if (string.IsNullOrEmpty(result.Ticker))
                {
                    continue;
                }

                decimal close = result.Close;
                if (close < minPrice)
                {
                    continue;
                }

                decimal dollarVolume = close * (decimal)result.Volume;
                if (dollarVolume < minDollarVolume)
                {
                    continue;
                }

                filteredStocks.Add(result);
            }

            filteredStocks.Sort((left, right) =>
            {
                decimal leftVolume = left.Close * (decimal)left.Volume;
                decimal rightVolume = right.Close * (decimal)right.Volume;
                return rightVolume.CompareTo(leftVolume);
            });

            if (filteredStocks.Count > 3000)
            {
                filteredStocks.RemoveRange(3000, filteredStocks.Count - 3000);
            }

            if (filteredStocks.Count == 0)
            {
                return (false, 0, $"0 stocks after filtering (raw: {response.Results.Length})");
            }

            // Write coarse universe file in LEAN format
            // Format: sid,symbol,close,volume,dollar_volume
            string filePath = System.IO.Path.Combine(outputDir, $"{date:yyyyMMdd}.csv");
            using System.IO.StreamWriter writer = new System.IO.StreamWriter(filePath);

            foreach (PolygonGroupedResult stock in filteredStocks)
            {
                decimal dollarVolume = stock.Close * (decimal)stock.Volume;
                // LEAN coarse format: Symbol ID (hash), Symbol, Close Price, Volume, Dollar Volume
                string symbolId = GetSymbolId(stock.Ticker!);
                string line = $"{symbolId},{stock.Ticker!.ToLowerInvariant()},{stock.Close:F4},{stock.Volume:F0},{dollarVolume:F0}";
                writer.WriteLine(line);
            }

            return (true, filteredStocks.Count, null);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    private static string GetSymbolId(string ticker)
    {
        // Generate a consistent hash-based ID for the symbol
        // This is a simplified version - LEAN uses more complex SID generation
        int hash = ticker.GetHashCode();
        return $"{Math.Abs(hash):X8}";
    }

    private static List<DateTime> GetTradingDays(DateTime start, DateTime end)
    {
        List<DateTime> days = new List<DateTime>();
        DateTime current = start;

        while (current <= end)
        {
            // Skip weekends
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                days.Add(current);
            }
            current = current.AddDays(1);
        }

        return days;
    }

    private static int ListUniverseFiles()
    {
        string? dataPath = FindDataPath();
        if (dataPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find data folder[/]");
            return 1;
        }

        string universeDir = System.IO.Path.Combine(dataPath, "equity", "usa", "fundamental", "coarse");
        
        AnsiConsole.MarkupLine("[bold blue]Universe Files[/]");
        AnsiConsole.WriteLine();

        if (!System.IO.Directory.Exists(universeDir))
        {
            AnsiConsole.MarkupLine("[grey]No universe files found. Run 'alaris universe generate' first.[/]");
            return 0;
        }

        string[] files = System.IO.Directory.GetFiles(universeDir, "*.csv");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        
        AnsiConsole.MarkupLine($"[grey]Directory: {Markup.Escape(universeDir)}[/]");
        AnsiConsole.MarkupLine($"[grey]Files: {files.Length}[/]");
        AnsiConsole.WriteLine();

        if (files.Length > 0)
        {
            Table table = new Table();
            table.AddColumn("[grey]Date[/]");
            table.AddColumn("[grey]Stocks[/]");
            table.AddColumn("[grey]Size[/]");

            // Show first and last few files
            List<string> filesToShow = new List<string>();
            if (files.Length <= 10)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    filesToShow.Add(files[i]);
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    filesToShow.Add(files[i]);
                }

                for (int i = files.Length - 5; i < files.Length; i++)
                {
                    filesToShow.Add(files[i]);
                }
            }

            foreach (string file in filesToShow)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                int lineCount = CountLines(file);
                long size = new System.IO.FileInfo(file).Length;
                table.AddRow(fileName, lineCount.ToString("N0"), FormatSize(size));
            }

            if (files.Length > 10)
            {
                table.AddRow("[grey]...[/]", "[grey]...[/]", "[grey]...[/]");
            }

            AnsiConsole.Write(table);
        }

        return 0;
    }

    private static int ShowUniverseInfo()
    {
        AnsiConsole.MarkupLine("[bold blue]Universe Configuration[/]");
        AnsiConsole.WriteLine();

        Table table = new Table();
        table.AddColumn("[grey]Setting[/]");
        table.AddColumn("[white]Value[/]");

        string? dataPath = FindDataPath();
        table.AddRow("Data Folder", dataPath ?? "(not found)");

        string? polygonKey = GetPolygonApiKey();
        table.AddRow("Polygon API Key", string.IsNullOrEmpty(polygonKey) ? "[red]Not configured[/]" : "[green]Configured[/]");

        if (dataPath != null)
        {
            string universeDir = System.IO.Path.Combine(dataPath, "equity", "usa", "fundamental", "coarse");
            int fileCount = System.IO.Directory.Exists(universeDir) 
                ? System.IO.Directory.GetFiles(universeDir, "*.csv").Length 
                : 0;
            table.AddRow("Universe Files", fileCount.ToString("N0"));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static int InvalidAction(string action)
    {
        AnsiConsole.MarkupLine($"[red]Unknown action: {Markup.Escape(action)}[/]");
        AnsiConsole.MarkupLine("[grey]Available actions: generate, list, info[/]");
        return 1;
    }

    private static string? FindDataPath()
    {
        string[] paths = new[] 
        { 
            "lib/Alaris.Lean/Data", 
            "../lib/Alaris.Lean/Data", 
            "../../lib/Alaris.Lean/Data",
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

        string[] paths = new[] 
        { 
            "lib/Alaris.Lean/Data", 
            "../lib/Alaris.Lean/Data", 
            "../../lib/Alaris.Lean/Data",
            "Alaris.Lean/Data", 
            "../Alaris.Lean/Data", 
            "../../Alaris.Lean/Data" 
        };
        foreach (string path in paths)
        {
            string? parentDir = System.IO.Path.GetDirectoryName(path);
            if (parentDir != null && System.IO.Directory.Exists(parentDir))
            {
                System.IO.Directory.CreateDirectory(path);
                return System.IO.Path.GetFullPath(path);
            }
        }
        return System.IO.Path.GetFullPath("lib/Alaris.Lean/Data");
    }

    private static string? GetPolygonApiKey()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(System.IO.Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("config.json", optional: true)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddUserSecrets<UniverseSettings>(optional: true)
            .AddEnvironmentVariables("ALARIS_")
            .Build();

        string? apiKey = config["Polygon:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        string[] paths = new[] { "appsettings.local.jsonc", "../appsettings.local.jsonc", "../../appsettings.local.jsonc" };
        foreach (string path in paths)
        {
            if (System.IO.File.Exists(path))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(path);
                    string[] lines = json.Split('\n');
                    bool inPolygon = false;
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("//")) continue;
                        if (trimmed.Contains("\"Polygon\"")) inPolygon = true;
                        else if (inPolygon && trimmed.Contains("\"ApiKey\""))
                        {
                            int colonIdx = trimmed.IndexOf(':');
                            if (colonIdx > 0)
                            {
                                string value = trimmed[(colonIdx + 1)..].Trim().Trim(',', '"', ' ');
                                if (!string.IsNullOrEmpty(value)) return value;
                            }
                        }
                        else if (inPolygon && trimmed.StartsWith("}")) inPolygon = false;
                    }
                }
                catch { /* Ignore parsing errors */ }
            }
        }
        return null;
    }

    private static int CountLines(string filePath)
    {
        int count = 0;
        foreach (string _ in System.IO.File.ReadLines(filePath))
        {
            count++;
        }
        return count;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
        return $"{bytes / (1024 * 1024):F1} MB";
    }
}

file sealed class PolygonGroupedResponse
{
    [JsonPropertyName("results")]
    public PolygonGroupedResult[]? Results { get; init; }

    [JsonPropertyName("resultsCount")]
    public int ResultsCount { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

file sealed class PolygonGroupedResult
{
    [JsonPropertyName("T")]
    public string? Ticker { get; init; }

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
