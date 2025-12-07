// =============================================================================
// APcm003A.cs - Data Command
// Component: APcm003A | Category: Commands | Variant: A (Primary)
// =============================================================================
// Implements 'alaris data download' command for market data management.
// Uses LEAN ToolBox for data download and conversion.
// =============================================================================

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Alaris.Application.Commands;

/// <summary>
/// Data Command settings.
/// </summary>
public sealed class DataSettings : CommandSettings
{
    [CommandArgument(0, "[action]")]
    [Description("Action: download, list, convert")]
    [DefaultValue("list")]
    public string Action { get; init; } = "list";

    [CommandOption("-t|--ticker <TICKER>")]
    [Description("Ticker symbol (e.g., AAPL)")]
    public string? Ticker { get; init; }

    [CommandOption("--tickers <TICKERS>")]
    [Description("Multiple tickers (comma-separated: AAPL,MSFT,GOOGL)")]
    public string? Tickers { get; init; }

    [CommandOption("--source <SOURCE>")]
    [Description("Data source: polygon, yahoo")]
    [DefaultValue("polygon")]
    public string Source { get; init; } = "polygon";

    [CommandOption("--type <TYPE>")]
    [Description("Data type: equity, option")]
    [DefaultValue("equity")]
    public string DataType { get; init; } = "equity";

    [CommandOption("--resolution <RESOLUTION>")]
    [Description("Resolution: minute, hour, daily")]
    [DefaultValue("minute")]
    public string Resolution { get; init; } = "minute";

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

        AnsiConsole.MarkupLine("[bold blue]Data Download Configuration[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("[grey]Parameter[/]");
        table.AddColumn("[white]Value[/]");
        table.AddRow("Source", settings.Source);
        table.AddRow("Type", settings.DataType);
        table.AddRow("Resolution", settings.Resolution);
        table.AddRow("Tickers", tickers);
        table.AddRow("From", fromDate);
        table.AddRow("To", toDate);
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Check for Polygon API key in config
        var configPath = FindConfigPath();
        if (configPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find config.json[/]");
            return 1;
        }

        if (settings.Source.Equals("polygon", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = ReadPolygonApiKey(configPath);
            if (string.IsNullOrEmpty(apiKey))
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Polygon API key not configured in config.json");
                AnsiConsole.MarkupLine("[grey]Add 'polygon-api-key': 'YOUR_API_KEY' to config.json[/]");
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.MarkupLine("[green]Polygon API key found[/]");
                AnsiConsole.WriteLine();
            }
        }

        // Execute data download via LEAN ToolBox
        return ExecuteToolBox(settings, tickers, fromDate, toDate, configPath);
    }

    private static int ExecuteToolBox(
        DataSettings settings, 
        string tickers, 
        string fromDate, 
        string toDate,
        string configPath)
    {
        var toolBoxPath = FindToolBoxPath();
        if (toolBoxPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find LEAN ToolBox[/]");
            return 1;
        }

        // Build ToolBox arguments based on data source
        var app = settings.Source.ToLowerInvariant() switch
        {
            "polygon" => "PDLD",  // Polygon Data Downloader
            "yahoo" => "YDC",     // Yahoo Data Converter  
            _ => "PDLD"
        };

        // Build command arguments
        var args = $"--app={app} --tickers={tickers} --from-date={fromDate} --to-date={toDate}";
        
        if (!string.IsNullOrEmpty(settings.Resolution))
        {
            args += $" --resolution={settings.Resolution}";
        }

        if (settings.DataType.Equals("option", StringComparison.OrdinalIgnoreCase))
        {
            args += " --security-type=Option";
        }

        AnsiConsole.MarkupLine($"[grey]ToolBox: {toolBoxPath}[/]");
        AnsiConsole.MarkupLine($"[grey]Arguments: {args}[/]");
        AnsiConsole.WriteLine();

        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("[yellow]Downloading market data...[/]", ctx =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{toolBoxPath}\" -- {args}",
                    WorkingDirectory = System.IO.Path.GetDirectoryName(configPath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    AnsiConsole.MarkupLine("[red]Failed to start ToolBox[/]");
                    return 1;
                }

                // Read output
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                // Display results
                if (!string.IsNullOrWhiteSpace(output))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[blue]ToolBox Output:[/]");
                    foreach (var line in output.Split('\n').Take(30))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            Console.WriteLine(line);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[yellow]Messages:[/]");
                    foreach (var line in error.Split('\n').Take(20))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var escaped = Markup.Escape(line);
                            AnsiConsole.MarkupLine($"[grey]{escaped}[/]");
                        }
                    }
                }

                if (process.ExitCode == 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[green]Download completed successfully[/]");
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[yellow]ToolBox exited with code {process.ExitCode}[/]");
                }

                return process.ExitCode;
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

        if (!Directory.Exists(dataPath))
        {
            AnsiConsole.MarkupLine("[grey]No data folder exists yet[/]");
            return 0;
        }

        // Show data categories with file counts
        var table = new Table();
        table.AddColumn("[grey]Category[/]");
        table.AddColumn("[grey]Files[/]");
        table.AddColumn("[grey]Size[/]");

        var dirs = Directory.GetDirectories(dataPath);
        foreach (var dir in dirs.OrderBy(d => d))
        {
            var name = System.IO.Path.GetFileName(dir);
            var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            var size = files.Sum(f => new FileInfo(f).Length);
            var sizeStr = FormatSize(size);
            table.AddRow($"[blue]{name}/[/]", files.Length.ToString(), sizeStr);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static int ShowDataStatus()
    {
        var configPath = FindConfigPath();
        if (configPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find config.json[/]");
            return 1;
        }

        var dataPath = FindDataPath();

        AnsiConsole.MarkupLine("[bold blue]Data Status[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("[grey]Setting[/]");
        table.AddColumn("[white]Value[/]");

        table.AddRow("Config", configPath);
        table.AddRow("Data Folder", dataPath ?? "(not found)");

        var polygonKey = ReadPolygonApiKey(configPath);
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
            if (Directory.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }

        // Create default location if none exists
        var defaultPath = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Alaris.Lean", "Data");
        return System.IO.Path.GetFullPath(defaultPath);
    }

    private static string? FindConfigPath()
    {
        var paths = new[]
        {
            "config.json",
            "../config.json",
            "../../config.json"
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }

        return null;
    }

    private static string? FindToolBoxPath()
    {
        var paths = new[]
        {
            "Alaris.Lean/ToolBox/QuantConnect.ToolBox.csproj",
            "../Alaris.Lean/ToolBox/QuantConnect.ToolBox.csproj",
            "../../Alaris.Lean/ToolBox/QuantConnect.ToolBox.csproj"
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }

        return null;
    }

    private static string? ReadPolygonApiKey(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            // Simple extraction - look for polygon-api-key
            var lines = json.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("polygon-api-key") && !line.TrimStart().StartsWith("//"))
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 2)
                    {
                        var value = string.Join(":", parts.Skip(1))
                            .Trim()
                            .Trim(',', '"', ' ');
                        if (!string.IsNullOrEmpty(value) && !value.StartsWith("//"))
                        {
                            return value;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors
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
