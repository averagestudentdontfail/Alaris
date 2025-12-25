// APcm001A.cs - 'alaris run' command wrapping LEAN engine

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Alaris.Host.Application.Command;

/// <summary>
/// Run Command settings.
/// </summary>
public sealed class RunSettings : CommandSettings
{
    [CommandOption("-m|--mode <MODE>")]
    [Description("Execution mode: backtest, paper, or live")]
    [DefaultValue("backtest")]
    public string Mode { get; init; } = "backtest";

    [CommandOption("-s|--start <DATE>")]
    [Description("Backtest start date (YYYY-MM-DD)")]
    public string? StartDate { get; init; }

    [CommandOption("-e|--end <DATE>")]
    [Description("Backtest end date (YYYY-MM-DD)")]
    public string? EndDate { get; init; }

    [CommandOption("-c|--config <PATH>")]
    [Description("Path to configuration file")]
    public string? ConfigPath { get; init; }
}

/// <summary>
/// Alaris Run Command - executes algorithm in specified mode.
/// Component ID: APcm001A
/// </summary>
public sealed class APcm001A : Command<RunSettings>
{
    public override int Execute(CommandContext context, RunSettings settings)
    {
        var mode = settings.Mode.ToLowerInvariant();

        // Validate mode
        if (mode is not ("backtest" or "paper" or "live"))
        {
            AnsiConsole.MarkupLine("[red]Invalid mode. Use: backtest, paper, or live[/]");
            return 1;
        }

        // Map mode to LEAN environment
        var environment = mode switch
        {
            "backtest" => "backtesting",
            "paper" => "live-paper",
            "live" => "live-interactive",
            _ => "backtesting"
        };

        AnsiConsole.MarkupLine($"[blue]Mode:[/] {mode}");
        AnsiConsole.MarkupLine($"[blue]Environment:[/] {environment}");

        if (settings.StartDate is not null)
        {
            AnsiConsole.MarkupLine($"[blue]Start Date:[/] {settings.StartDate}");
        }

        if (settings.EndDate is not null)
        {
            AnsiConsole.MarkupLine($"[blue]End Date:[/] {settings.EndDate}");
        }

        AnsiConsole.WriteLine();

        // Build configuration
        var configPath = settings.ConfigPath ?? FindConfigPath();
        if (configPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find config.json[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Using config: {configPath}[/]");

        // Execute LEAN
        return ExecuteLean(environment, configPath, settings);
    }

    private static string? FindConfigPath()
    {
        var paths = new[]
        {
            "config.json",
            "../config.json",
            "../../config.json",
            System.IO.Path.Combine(AppContext.BaseDirectory, "config.json")
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

    private static int ExecuteLean(string environment, string configPath, RunSettings settings)
    {
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("[yellow]Starting LEAN engine...[/]", ctx =>
            {
                // Set environment variables for LEAN
                Environment.SetEnvironmentVariable("QC_CONFIG", configPath);
                Environment.SetEnvironmentVariable("QC_ENVIRONMENT", environment);

                ctx.Status("[green]LEAN engine running...[/]");
            });

        // Find LEAN launcher
        var launcherPath = FindLeanLauncher();
        if (launcherPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find LEAN Launcher[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Launcher: {launcherPath}[/]");

        // Start LEAN process
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{launcherPath}\"",
            WorkingDirectory = System.IO.Path.GetDirectoryName(configPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Override environment in config
        psi.Environment["QC_ENVIRONMENT"] = environment;
        
        // Pass backtest dates to algorithm via environment variables
        if (settings.StartDate is not null)
        {
            psi.Environment["ALARIS_BACKTEST_STARTDATE"] = settings.StartDate;
        }
        if (settings.EndDate is not null)
        {
            psi.Environment["ALARIS_BACKTEST_ENDDATE"] = settings.EndDate;
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            AnsiConsole.MarkupLine("[red]Failed to start LEAN process[/]");
            return 1;
        }

        // Stream output
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                // Escape markup characters to prevent Spectre.Console parsing errors
                // LEAN output often contains <TypeName> patterns that break markup
                var escaped = Markup.Escape(e.Data);
                AnsiConsole.MarkupLine($"[red]{escaped}[/]");
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();
        return process.ExitCode;
    }

    private static string? FindLeanLauncher()
    {
        var paths = new[]
        {
            "Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj",
            "../Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj",
            "../../Alaris.Lean/Launcher/QuantConnect.Lean.Launcher.csproj"
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
}
