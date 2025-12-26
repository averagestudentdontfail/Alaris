// APcm006A.cs - Bootstrap earnings calendar command

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Alaris.Infrastructure.Data.Provider.Polygon;
using Alaris.Infrastructure.Data.Provider.Nasdaq;
using Alaris.Infrastructure.Data.Provider.Treasury;

namespace Alaris.Host.Application.Command;

/// <summary>
/// Settings for bootstrap earnings command.
/// </summary>
public sealed class BootstrapEarningsSettings : CommandSettings
{
    [Description("Start date (YYYY-MM-DD)")]
    [CommandOption("--start|-s")]
    public string? StartDate { get; set; }

    [Description("End date (YYYY-MM-DD)")]
    [CommandOption("--end|-e")]
    public string? EndDate { get; set; }

    [Description("Output directory for cached files")]
    [CommandOption("--output|-o")]
    public string? OutputPath { get; set; }

    [Description("Number of years to download (default: 2)")]
    [CommandOption("--years|-y")]
    public int Years { get; set; } = 2;
}

/// <summary>
/// Downloads earnings calendar to cache files for backtesting.
/// Component ID: APcm006A
/// </summary>
public sealed class BootstrapEarningsCommand : AsyncCommand<BootstrapEarningsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BootstrapEarningsSettings settings)
    {
        AnsiConsole.MarkupLine("[bold blue]Bootstrap Earnings Calendar[/]");
        AnsiConsole.WriteLine();

        // Resolve date range
        DateTime endDate = string.IsNullOrEmpty(settings.EndDate)
            ? DateTime.Today.AddDays(-1)
            : DateTime.Parse(settings.EndDate);
        
        DateTime startDate = string.IsNullOrEmpty(settings.StartDate)
            ? endDate.AddYears(-settings.Years)
            : DateTime.Parse(settings.StartDate);

        // Resolve output path
        string outputDir = settings.OutputPath
            ?? Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA")
            ?? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".project", "Alaris", "Alaris.Sessions", "earnings-cache");

        AnsiConsole.MarkupLine($"[grey]Date range: {startDate:yyyy-MM-dd} → {endDate:yyyy-MM-dd}[/]");
        AnsiConsole.MarkupLine($"[grey]Output: {outputDir}[/]");
        AnsiConsole.WriteLine();

        try
        {
            using var service = DependencyFactory.CreateAPsv002A();
            
            int downloaded = await service.BootstrapEarningsCalendarAsync(
                startDate,
                endDate,
                outputDir,
                CancellationToken.None);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]✓ Downloaded {downloaded} days of earnings data[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
