// CLdt003A.cs - Data validate command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;
using Alaris.Host.Application.Service;

namespace Alaris.Host.Application.Cli.Commands.Data;

/// <summary>
/// Validates data quality for a session.
/// Component ID: CLdt003A
/// </summary>
public sealed class CLdt003A : AsyncCommand<DataValidateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DataValidateSettings settings)
    {
        CLif003A.Info($"Validating data for session: {settings.SessionId}");
        AnsiConsole.WriteLine();

        var sessionService = new APsv001A();
        var session = await sessionService.GetAsync(settings.SessionId);

        if (session == null)
        {
            CLif003A.Error($"Session not found: {settings.SessionId}");
            return 1;
        }

        string dataPath = sessionService.GetDataPath(session.SessionId);
        int errors = 0;
        int warnings = 0;

        await CLif003A.WithProgressAsync("Validating", async ctx =>
        {
            var task = ctx.AddTask("[blue]Checking data integrity[/]", maxValue: 4);

            // 1. Check price data
            task.Description = "[blue]Checking price data...[/]";
            string pricesPath = System.IO.Path.Combine(dataPath, "equity", "usa", "daily");
            if (Directory.Exists(pricesPath))
            {
                var zipFiles = Directory.GetFiles(pricesPath, "*.zip");
                foreach (var zip in zipFiles)
                {
                    try
                    {
                        using var archive = System.IO.Compression.ZipFile.OpenRead(zip);
                        if (archive.Entries.Count == 0)
                        {
                            warnings++;
                            if (settings.Verbose)
                                CLif003A.Warning($"Empty ZIP: {System.IO.Path.GetFileName(zip)}");
                        }
                    }
                    catch (Exception)
                    {
                        errors++;
                        CLif003A.Error($"Corrupt ZIP: {System.IO.Path.GetFileName(zip)}");
                    }
                }
            }
            else
            {
                errors++;
                CLif003A.Error("Price data directory missing");
            }
            task.Increment(1);

            // 2. Check options data
            task.Description = "[blue]Checking options data...[/]";
            string optionsPath = System.IO.Path.Combine(dataPath, "options");
            if (Directory.Exists(optionsPath))
            {
                var jsonFiles = Directory.GetFiles(optionsPath, "*.json");
                foreach (var json in jsonFiles)
                {
                    try
                    {
                        string content = await File.ReadAllTextAsync(json);
                        if (content.Length < 10)
                        {
                            warnings++;
                            if (settings.Verbose)
                                CLif003A.Warning($"Empty options: {System.IO.Path.GetFileName(json)}");
                        }
                    }
                    catch (Exception)
                    {
                        errors++;
                        CLif003A.Error($"Invalid JSON: {System.IO.Path.GetFileName(json)}");
                    }
                }
            }
            task.Increment(1);

            // 3. Check system files
            task.Description = "[blue]Checking system files...[/]";
            string[] requiredDirs = { "market-hours", "symbol-properties" };
            foreach (var dir in requiredDirs)
            {
                string path = System.IO.Path.Combine(dataPath, dir);
                if (!Directory.Exists(path))
                {
                    errors++;
                    CLif003A.Error($"Missing system directory: {dir}");
                }
            }
            task.Increment(1);

            // 4. Check map/factor files
            task.Description = "[blue]Checking LEAN auxiliary files...[/]";
            string mapPath = System.IO.Path.Combine(dataPath, "equity", "usa", "map_files");
            string factorPath = System.IO.Path.Combine(dataPath, "equity", "usa", "factor_files");
            if (!Directory.Exists(mapPath)) warnings++;
            if (!Directory.Exists(factorPath)) warnings++;
            task.Increment(1);

            task.Description = "[green]Validation complete[/]";
        });

        AnsiConsole.WriteLine();

        // Summary
        if (errors == 0 && warnings == 0)
        {
            CLif003A.Success("All checks passed.");
        }
        else
        {
            CLif003A.WriteKeyValueTable("Validation Results", new[]
            {
                ("Errors", errors > 0 ? $"[red]{errors}[/]" : "[green]0[/]"),
                ("Warnings", warnings > 0 ? $"[yellow]{warnings}[/]" : "[green]0[/]")
            });
        }

        return errors > 0 ? 1 : 0;
    }
}
