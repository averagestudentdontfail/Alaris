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
        DateTime endDate;
        if (string.IsNullOrEmpty(settings.EndDate))
        {
            endDate = DateTime.Today.AddDays(-1);
        }
        else if (!DateTime.TryParse(settings.EndDate, out endDate))
        {
            AnsiConsole.MarkupLine("[red]Invalid end date. Use YYYY-MM-DD format.[/]");
            return 1;
        }
        
        DateTime startDate;
        if (string.IsNullOrEmpty(settings.StartDate))
        {
            if (settings.Years <= 0)
            {
                AnsiConsole.MarkupLine("[red]Years must be greater than zero.[/]");
                return 1;
            }
            startDate = endDate.AddYears(-settings.Years);
        }
        else if (!DateTime.TryParse(settings.StartDate, out startDate))
        {
            AnsiConsole.MarkupLine("[red]Invalid start date. Use YYYY-MM-DD format.[/]");
            return 1;
        }

        if (endDate < startDate)
        {
            AnsiConsole.MarkupLine("[red]End date must be on or after start date.[/]");
            return 1;
        }

        // Resolve output path
        string outputDir = string.IsNullOrWhiteSpace(settings.OutputPath)
            ? (Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA")
               ?? System.IO.Path.Combine(
                   Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                   ".project", "Alaris", "ses", "earnings-cache"))
            : settings.OutputPath;

        AnsiConsole.MarkupLine($"[grey]Date range: {startDate:yyyy-MM-dd} → {endDate:yyyy-MM-dd}[/]");
        AnsiConsole.MarkupLine($"[grey]Output: {outputDir}[/]");
        AnsiConsole.WriteLine();

        try
        {
            using APsv002A service = DependencyFactory.CreateAPsv002A();
            
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
